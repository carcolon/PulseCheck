using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;
using System.Windows.Forms.Integration;
using System.Windows.Forms;

namespace PulseCheck.Agent;

public sealed class WindowsPromptService : ICampaignPromptService
{
    public Task<PromptResult> PromptAsync(
        AgentCampaignConfiguration campaign,
        bool forceResponseForPostponeLimit,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<PromptResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            using var form = new PulsePromptForm(campaign, forceResponseForPostponeLimit);
            form.FormClosed += (_, _) =>
            {
                if (!tcs.Task.IsCompleted)
                {
                    tcs.TrySetResult(new PromptResult(form.PromptAnswers, form.PostponeFor));
                }
            };

            cancellationToken.Register(() =>
            {
                if (form.IsHandleCreated)
                {
                    form.BeginInvoke(form.RequestSystemClose);
                }
            });

            Application.Run(form);
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        return tcs.Task;
    }

    private sealed class PulsePromptForm : Form
    {
        private const int ToastMinWidth = 480;
        private const int ToastMaxWidth = 620;
        private const int ChoiceToastMaxWidth = 760;
        private const int ToastMinHeight = 340;
        private const int ScreenMargin = 16;
        private const int ContentLeft = 20;
        private const string RequiredResponseNotice = "Respuesta obligatoria: completa la encuesta para continuar.";
        private const string PostponeLimitNotice = "Ya pospusiste esta encuesta dos veces. Esta vez la respuesta es obligatoria.";
        private static readonly Color SolvoDeep = Color.FromArgb(7, 53, 68);
        private static readonly Color SolvoTeal = Color.FromArgb(0, 117, 141);
        private static readonly Color SolvoBlue = Color.FromArgb(0, 138, 171);
        private static readonly Color SolvoOrange = Color.FromArgb(238, 118, 35);
        private static readonly Color Surface = Color.FromArgb(248, 252, 253);
        private static readonly Color Border = Color.FromArgb(183, 214, 224);
        private static readonly Color MutedText = Color.FromArgb(95, 119, 130);
        private readonly Icon windowIcon;
        private readonly System.Windows.Forms.Timer entranceTimer = new() { Interval = 12 };
        private readonly System.Windows.Forms.Timer visibilityGuardTimer = new() { Interval = 1500 };
        private readonly Label campaignLabel;
        private readonly Label progressLabel;
        private readonly Label questionLabel;
        private readonly Panel answerPanel;
        private readonly Button dismissButton;
        private readonly Button continueButton;
        private readonly bool requireResponse;
        private readonly bool allowDismiss;
        private readonly List<AgentQuestion> questions;
        private readonly List<PromptAnswer> answers = [];
        private readonly Point targetLocation;
        private EventHandler? continueHandler;
        private int currentQuestionIndex;
        private bool allowSystemClose;

        public IReadOnlyList<PromptAnswer>? PromptAnswers { get; private set; }

        public TimeSpan? PostponeFor { get; private set; }

        public PulsePromptForm(AgentCampaignConfiguration campaign, bool forceResponseForPostponeLimit)
        {
            questions = campaign.Questions
                .Where(item => !string.IsNullOrWhiteSpace(item.Text))
                .ToList();
            requireResponse = forceResponseForPostponeLimit ||
                              HasMetadataFlag(campaign.ScheduleRule, "force-response") ||
                              HasMetadataFlag(campaign.ScheduleRule, "no-dismiss");
            allowDismiss = !requireResponse;

            Text = "PulseCheck by Solvo";
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = true;
            ShowIcon = true;
            Width = CalculatePromptWidth(campaign);
            Height = ToastMinHeight;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Surface;
            ForeColor = SolvoDeep;
            Padding = new Padding(18, 18, 18, 16);
            Opacity = 0;
            DoubleBuffered = true;
            windowIcon = AgentIconProvider.CreateIcon();
            Icon = windowIcon;

            var contentLeft = ContentLeft;
            var contentTop = 16;
            var contentWidth = Width - (contentLeft * 2);
            var headingFont = new Font("Segoe UI", 14, FontStyle.Bold);
            var bodyFont = new Font("Segoe UI", 10, FontStyle.Regular);
            var helperFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;

            targetLocation = new Point(
                workingArea.Right - Width - ScreenMargin,
                workingArea.Bottom - Height - ScreenMargin);
            Location = new Point(targetLocation.X, workingArea.Bottom + 12);

            Load += (_, _) => Region = BuildRoundedRegion(ClientRectangle, 24);
            Shown += (_, _) =>
            {
                StartEntranceAnimation();
                if (requireResponse)
                {
                    visibilityGuardTimer.Tick += OnVisibilityGuardTick;
                    visibilityGuardTimer.Start();
                }

                if (questions.Count == 0)
                {
                    CloseInternal();
                    return;
                }

                PlayCampaignNotificationSound();
                RenderCurrentQuestion();
            };
            FormClosing += OnFormClosingGuard;
            Deactivate += (_, _) =>
            {
                if (requireResponse)
                {
                    BeginInvoke(EnforcePromptVisibility);
                }
            };
            FormClosed += (_, _) =>
            {
                entranceTimer.Dispose();
                visibilityGuardTimer.Dispose();
                windowIcon.Dispose();
            };
            Paint += (_, eventArgs) =>
            {
                using var borderPen = new Pen(Color.FromArgb(140, Border), 1.2f);
                eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                eventArgs.Graphics.DrawPath(borderPen, BuildRoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), 24));
            };

            var accent = new Panel
            {
                Left = 0,
                Top = 0,
                Width = 6,
                Height = Height,
                BackColor = SolvoTeal,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
            };

            var title = new Label
            {
                Text = "PulseCheck by Solvo",
                AutoSize = false,
                Width = contentWidth,
                Height = 24,
                Left = contentLeft,
                Top = contentTop,
                Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold),
                ForeColor = SolvoTeal
            };

            campaignLabel = new Label
            {
                Text = campaign.Name,
                AutoSize = false,
                Width = contentWidth,
                Height = MeasureMultilineHeight(campaign.Name, headingFont, contentWidth),
                Left = contentLeft,
                Top = title.Bottom + 6,
                Font = headingFont,
                ForeColor = SolvoDeep,
                UseCompatibleTextRendering = true
            };

            progressLabel = new Label
            {
                Text = forceResponseForPostponeLimit ? PostponeLimitNotice : string.Empty,
                AutoSize = false,
                Width = contentWidth,
                Height = 18,
                Left = contentLeft,
                Top = campaignLabel.Bottom + 4,
                Font = helperFont,
                ForeColor = MutedText
            };

            questionLabel = new Label
            {
                Text = string.Empty,
                AutoSize = false,
                Width = contentWidth,
                Height = 48,
                Left = contentLeft,
                Top = progressLabel.Bottom + 6,
                Font = bodyFont,
                ForeColor = Color.FromArgb(26, 61, 75),
                UseCompatibleTextRendering = true
            };

            answerPanel = new Panel
            {
                Left = contentLeft,
                Top = questionLabel.Bottom + 8,
                Width = contentWidth,
                Height = 96,
                BackColor = Color.Transparent
            };

            continueButton = new Button
            {
                Text = "Siguiente",
                Width = 112,
                Height = 34,
                Left = contentLeft,
                Top = answerPanel.Bottom + 10,
                BackColor = SolvoOrange,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Visible = false
            };
            continueButton.FlatAppearance.BorderSize = 0;
            continueButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 159, 84);
            continueButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(209, 94, 20);

            dismissButton = new Button
            {
                Text = "Posponer",
                Width = 112,
                Height = 34,
                Left = contentLeft + contentWidth - 112,
                Top = answerPanel.Bottom + 10,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(55, 82, 98),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Visible = allowDismiss,
                Enabled = allowDismiss
            };
            dismissButton.FlatAppearance.BorderColor = Color.FromArgb(197, 217, 225);
            dismissButton.Click += (_, _) => PostponeDefault();

            Controls.Add(accent);
            Controls.Add(title);
            Controls.Add(campaignLabel);
            Controls.Add(progressLabel);
            Controls.Add(questionLabel);
            Controls.Add(answerPanel);
            Controls.Add(continueButton);
            Controls.Add(dismissButton);
        }

        private void RenderCurrentQuestion()
        {
            if (currentQuestionIndex < 0 || currentQuestionIndex >= questions.Count)
            {
                PromptAnswers = answers.ToArray();
                CloseInternal();
                return;
            }

            var question = questions[currentQuestionIndex];
            var isChoiceQuestion = string.Equals(question.Type, "Choice", StringComparison.OrdinalIgnoreCase);

            ResizePromptForQuestion(question, isChoiceQuestion);

            progressLabel.Text = $"Pregunta {currentQuestionIndex + 1} de {questions.Count}";
            progressLabel.ForeColor = MutedText;
            questionLabel.Text = question.Text;
            questionLabel.Height = MeasureMultilineHeight(question.Text, questionLabel.Font, questionLabel.Width);

            answerPanel.Top = questionLabel.Bottom + 10;
            answerPanel.Controls.Clear();
            answerPanel.AutoScroll = false;
            answerPanel.Height = 96;
            continueButton.Visible = false;
            if (continueHandler is not null)
            {
                continueButton.Click -= continueHandler;
                continueHandler = null;
            }
            continueButton.Enabled = true;

            if (string.Equals(question.Type, "Text", StringComparison.OrdinalIgnoreCase))
            {
                RenderTextQuestion(question);
            }
            else if (string.Equals(question.Type, "YesNo", StringComparison.OrdinalIgnoreCase))
            {
                RenderYesNoQuestion(question);
            }
            else if (isChoiceQuestion)
            {
                RenderChoiceQuestion(question);
            }
            else
            {
                RenderScaleQuestion(question);
            }

            dismissButton.Top = answerPanel.Bottom + 10;
            if (allowDismiss)
            {
                dismissButton.Visible = true;
                continueButton.Top = dismissButton.Top;
                continueButton.Left = answerPanel.Left;
                Height = Math.Max(ToastMinHeight, dismissButton.Bottom + 16);
            }
            else
            {
                dismissButton.Visible = false;
                if (continueButton.Visible)
                {
                    continueButton.Top = answerPanel.Bottom + 10;
                    continueButton.Left = answerPanel.Left + answerPanel.Width - continueButton.Width;
                    Height = Math.Max(ToastMinHeight, continueButton.Bottom + 16);
                }
                else
                {
                    Height = Math.Max(ToastMinHeight, answerPanel.Bottom + 24);
                }
            }

            var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            var maxFormHeight = workingArea.Height - (ScreenMargin * 2);
            if (Height > maxFormHeight)
            {
                Height = maxFormHeight;
            }

            var newTargetLocation = new Point(
                workingArea.Right - Width - ScreenMargin,
                workingArea.Bottom - Height - ScreenMargin);
            Location = newTargetLocation;
            RefreshWindowShape();
        }

        private static void PlayCampaignNotificationSound()
        {
            try
            {
                var soundPath = Path.Combine(AppContext.BaseDirectory, "Assets", "CampaignNotification.wav");
                if (!File.Exists(soundPath))
                {
                    return;
                }

                _ = Task.Run(() =>
                {
                    using var player = new System.Media.SoundPlayer(soundPath);
                    player.PlaySync();
                });
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write($"Campaign notification sound failed: {ex.Message}");
            }
        }

        private void ResizePromptForQuestion(AgentQuestion question, bool isChoiceQuestion)
        {
            var targetWidth = isChoiceQuestion
                ? CalculateChoicePromptWidth(question)
                : Width;

            if (targetWidth != Width)
            {
                Width = targetWidth;
            }

            var contentWidth = Width - (ContentLeft * 2);
            foreach (var label in Controls.OfType<Label>())
            {
                label.Left = ContentLeft;
                label.Width = contentWidth;
            }

            campaignLabel.Height = MeasureMultilineHeight(campaignLabel.Text, campaignLabel.Font, campaignLabel.Width);
            progressLabel.Top = campaignLabel.Bottom + 4;
            questionLabel.Top = progressLabel.Bottom + 6;
            answerPanel.Left = ContentLeft;
            answerPanel.Width = contentWidth;
            continueButton.Left = ContentLeft;
            dismissButton.Left = ContentLeft + contentWidth - dismissButton.Width;
        }

        private void RenderTextQuestion(AgentQuestion question)
        {
            var input = new TextBox
            {
                Left = 0,
                Top = 0,
                Width = answerPanel.Width,
                Height = 72,
                Multiline = true,
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                BackColor = Color.White,
                ForeColor = SolvoDeep,
                BorderStyle = BorderStyle.FixedSingle
            };

            if (!string.IsNullOrWhiteSpace(question.Placeholder))
            {
                input.Text = string.Empty;
            }

            continueButton.Visible = true;
            continueButton.Text = currentQuestionIndex == questions.Count - 1 ? "Enviar" : "Siguiente";
            continueButton.Enabled = false;
            continueHandler = OnContinueClick;
            continueButton.Click += continueHandler;

            input.TextChanged += (_, _) => continueButton.Enabled = !string.IsNullOrWhiteSpace(input.Text);

            answerPanel.Controls.Add(input);

            void OnContinueClick(object? sender, EventArgs args)
            {
                var text = input.Text.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                SaveAnswerAndAdvance(new PromptAnswer(
                    question.Id,
                    question.Text,
                    question.Type,
                    null,
                    text));
            }
        }

        private void RenderScaleQuestion(AgentQuestion question)
        {
            var min = question.MinValue ?? 1;
            var max = question.MaxValue ?? 5;
            if (max < min)
            {
                (min, max) = (max, min);
            }

            var totalOptions = max - min + 1;
            if (totalOptions <= 10)
            {
                const int buttonGap = 6;
                var flow = new FlowLayoutPanel
                {
                    Left = 0,
                    Top = 0,
                    Width = answerPanel.Width,
                    Height = 52,
                    BackColor = Color.Transparent,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false
                };

                var totalGap = buttonGap * Math.Max(0, totalOptions - 1);
                var buttonWidth = Math.Max(34, (answerPanel.Width - totalGap) / Math.Max(1, totalOptions));
                for (var value = min; value <= max; value++)
                {
                    var selectedValue = value;
                    var button = new Button
                    {
                        Text = selectedValue.ToString(),
                        Width = buttonWidth,
                        Height = 40,
                        Margin = new Padding(0, 0, selectedValue < max ? buttonGap : 0, 0),
                        BackColor = SolvoBlue,
                        ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat,
                        Font = new Font("Segoe UI", 10, FontStyle.Bold),
                        Cursor = Cursors.Hand,
                        UseVisualStyleBackColor = false,
                        TabStop = false
                    };

                    button.FlatAppearance.BorderSize = 0;
                    button.FlatAppearance.MouseOverBackColor = SolvoTeal;
                    button.FlatAppearance.MouseDownBackColor = SolvoDeep;
                    button.Click += (_, _) => SaveAnswerAndAdvance(new PromptAnswer(
                        question.Id,
                        question.Text,
                        question.Type,
                        selectedValue,
                        null));

                    flow.Controls.Add(button);
                }

                answerPanel.Controls.Add(flow);
            }
            else
            {
                var hint = new Label
                {
                    Text = $"Selecciona un valor entre {min} y {max}.",
                    AutoSize = false,
                    Width = answerPanel.Width,
                    Height = 20,
                    Left = 0,
                    Top = 0,
                    Font = new Font("Segoe UI", 9, FontStyle.Regular),
                    ForeColor = MutedText
                };

                var input = new NumericUpDown
                {
                    Left = 0,
                    Top = hint.Bottom + 6,
                    Width = 160,
                    Height = 34,
                    Minimum = min,
                    Maximum = max,
                    Value = min,
                    Font = new Font("Segoe UI", 10, FontStyle.Regular),
                    BackColor = Color.White,
                    ForeColor = SolvoDeep
                };

                continueButton.Visible = true;
                continueButton.Text = currentQuestionIndex == questions.Count - 1 ? "Enviar" : "Siguiente";
                continueHandler = OnContinueClick;
                continueButton.Click += continueHandler;

                answerPanel.Controls.Add(hint);
                answerPanel.Controls.Add(input);

                void OnContinueClick(object? sender, EventArgs args)
                {
                    SaveAnswerAndAdvance(new PromptAnswer(
                        question.Id,
                        question.Text,
                        question.Type,
                        decimal.ToInt32(input.Value),
                        null));
                }
            }
        }

        private void RenderYesNoQuestion(AgentQuestion question)
        {
            const int buttonGap = 10;
            var buttonWidth = (answerPanel.Width - buttonGap) / 2;

            var noButton = new Button
            {
                Text = "No",
                Left = 0,
                Top = 0,
                Width = buttonWidth,
                Height = 42,
                BackColor = Color.White,
                ForeColor = SolvoDeep,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                TabStop = false
            };
            noButton.FlatAppearance.BorderColor = Border;
            noButton.FlatAppearance.BorderSize = 1;
            noButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 247, 250);

            var yesButton = new Button
            {
                Text = "Sí",
                Left = buttonWidth + buttonGap,
                Top = 0,
                Width = buttonWidth,
                Height = 42,
                BackColor = SolvoBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                TabStop = false
            };
            yesButton.FlatAppearance.BorderSize = 0;
            yesButton.FlatAppearance.MouseOverBackColor = SolvoTeal;
            yesButton.FlatAppearance.MouseDownBackColor = SolvoDeep;

            noButton.Click += (_, _) => SaveAnswerAndAdvance(new PromptAnswer(
                question.Id,
                question.Text,
                question.Type,
                null,
                "No"));
            yesButton.Click += (_, _) => SaveAnswerAndAdvance(new PromptAnswer(
                question.Id,
                question.Text,
                question.Type,
                null,
                "Sí"));

            answerPanel.Controls.Add(noButton);
            answerPanel.Controls.Add(yesButton);
        }

        private void RenderChoiceQuestion(AgentQuestion question)
        {
            var options = question.Options?
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToArray() ?? [];

            if (options.Length < 2)
            {
                RenderTextQuestion(question);
                return;
            }

            const int cssButtonGap = 10;
            const int cssButtonHeight = 58;
            const int cssButtonFontSize = 22;
            const int cssPanelVerticalPadding = 8;
            const int webViewHeightBuffer = 18;
            var webViewScale = GetWebViewScale();
            var maxFormHeight = Screen.FromPoint(Cursor.Position).WorkingArea.Height - (ScreenMargin * 2);
            var footerHeight = allowDismiss
                ? dismissButton.Height + 26
                : continueButton.Visible ? continueButton.Height + 26 : 24;
            var availableAnswerHeight = Math.Max(120, maxFormHeight - answerPanel.Top - footerHeight);
            var cssOptionsHeight = options.Length * cssButtonHeight + Math.Max(0, options.Length - 1) * cssButtonGap + cssPanelVerticalPadding;
            var requiredAnswerHeight = ScaleCssPixels(cssOptionsHeight, webViewScale) + webViewHeightBuffer;
            var visibleAnswerHeight = Math.Min(requiredAnswerHeight, availableAnswerHeight);
            var flow = new FlowLayoutPanel
            {
                Left = 0,
                Top = 0,
                Width = answerPanel.Width,
                Height = requiredAnswerHeight,
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };

            answerPanel.AutoScroll = false;
            answerPanel.Height = visibleAnswerHeight;
            answerPanel.PerformLayout();

            var webView = CreateChoiceOptionsWebView(
                options,
                answerPanel.Width,
                answerPanel.Height,
                cssButtonHeight,
                cssButtonGap,
                cssPanelVerticalPadding,
                cssButtonFontSize,
                selectedOption => SaveAnswerAndAdvance(new PromptAnswer(
                    question.Id,
                    question.Text,
                    question.Type,
                    null,
                    selectedOption)));

            if (webView is not null)
            {
                answerPanel.Controls.Add(webView);
                return;
            }

            answerPanel.AutoScroll = visibleAnswerHeight < requiredAnswerHeight;
            foreach (var option in options)
            {
                var selectedOption = option;
                var host = CreateChoiceOptionHost(
                    selectedOption,
                    answerPanel.Width - (answerPanel.AutoScroll ? SystemInformation.VerticalScrollBarWidth : 0),
                    ScaleCssPixels(cssButtonHeight, webViewScale),
                    () => SaveAnswerAndAdvance(new PromptAnswer(
                    question.Id,
                    question.Text,
                    question.Type,
                    null,
                    selectedOption)));

                host.Margin = new Padding(0, 0, 0, ScaleCssPixels(cssButtonGap, webViewScale));
                flow.Controls.Add(host);
            }

            answerPanel.Controls.Add(flow);
        }

        private void SaveAnswerAndAdvance(PromptAnswer answer)
        {
            answers.Add(answer);
            if (currentQuestionIndex >= questions.Count - 1)
            {
                PromptAnswers = answers.ToArray();
                CloseInternal();
                return;
            }

            currentQuestionIndex++;
            RenderCurrentQuestion();
        }

        private void StartEntranceAnimation()
        {
            EnforcePromptVisibility();

            entranceTimer.Tick += AnimateEntrance;
            entranceTimer.Start();
        }

        private void AnimateEntrance(object? sender, EventArgs eventArgs)
        {
            var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            var expectedY = workingArea.Bottom - Height - ScreenMargin;
            var nextY = Math.Max(expectedY, Location.Y - 14);
            Location = new Point(Location.X, nextY);
            Opacity = Math.Min(1, Opacity + 0.14);

            if (nextY == expectedY && Opacity >= 0.98)
            {
                Opacity = 1;
                entranceTimer.Stop();
                entranceTimer.Tick -= AnimateEntrance;
            }
        }

        private void RefreshWindowShape()
        {
            if (IsHandleCreated)
            {
                Region?.Dispose();
                Region = BuildRoundedRegion(ClientRectangle, 24);
                Invalidate();
            }
        }

        private static Region BuildRoundedRegion(Rectangle bounds, int radius)
        {
            using var path = BuildRoundedPath(bounds, radius);
            return new Region(path);
        }

        private static GraphicsPath BuildRoundedPath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;

            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }

        private static int MeasureMultilineHeight(string text, Font font, int width)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return font.Height + 2;
            }

            using var bitmap = new Bitmap(1, 1);
            using var graphics = Graphics.FromImage(bitmap);
            var measured = graphics.MeasureString(text, font, width);
            return Math.Max(font.Height + 2, (int)Math.Ceiling(measured.Height) + 2);
        }

        private double GetWebViewScale()
            => Math.Max(1.0, DeviceDpi / 96.0);

        private static int ScaleCssPixels(int cssPixels, double scale)
            => (int)Math.Ceiling(cssPixels * scale) + 8;

        private static int CalculateChoicePromptWidth(AgentQuestion question)
        {
            var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            var maxWidth = Math.Min(ChoiceToastMaxWidth, workingArea.Width - (ScreenMargin * 2));
            var preferredWidth = ToastMinWidth;

            using var bitmap = new Bitmap(1, 1);
            using var graphics = Graphics.FromImage(bitmap);
            using var optionFont = new Font("Segoe UI Emoji", 21, FontStyle.Regular);
            using var questionFont = new Font("Segoe UI", 10, FontStyle.Regular);

            foreach (var option in question.Options ?? [])
            {
                if (string.IsNullOrWhiteSpace(option))
                {
                    continue;
                }

                var measured = graphics.MeasureString(option.Trim(), optionFont);
                preferredWidth = Math.Max(preferredWidth, (int)Math.Ceiling(measured.Width) + (ContentLeft * 2) + 96);
            }

            if (!string.IsNullOrWhiteSpace(question.Text))
            {
                var measuredQuestion = graphics.MeasureString(question.Text.Trim(), questionFont);
                preferredWidth = Math.Max(preferredWidth, (int)Math.Ceiling(measuredQuestion.Width) + (ContentLeft * 2) + 64);
            }

            return Math.Min(maxWidth, preferredWidth);
        }

        private static int CalculatePromptWidth(AgentCampaignConfiguration campaign)
        {
            var longestOptionLength = campaign.Questions
                .Where(item => string.Equals(item.Type, "Choice", StringComparison.OrdinalIgnoreCase))
                .SelectMany(item => item.Options ?? [])
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim().Length)
                .DefaultIfEmpty(0)
                .Max();

            var widestQuestionLength = campaign.Questions
                .Where(item => !string.IsNullOrWhiteSpace(item.Text))
                .Select(item => item.Text.Trim().Length)
                .DefaultIfEmpty(0)
                .Max();

            var preferredWidth = ToastMinWidth;
            if (longestOptionLength >= 16 || widestQuestionLength >= 64)
            {
                preferredWidth = 560;
            }

            if (longestOptionLength >= 28 || widestQuestionLength >= 96)
            {
                preferredWidth = ToastMaxWidth;
            }

            var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            return Math.Min(preferredWidth, workingArea.Width - (ScreenMargin * 2));
        }

        private static WebView2? CreateChoiceOptionsWebView(
            IReadOnlyList<string> options,
            int width,
            int height,
            int buttonHeight,
            int buttonGap,
            int panelVerticalPadding,
            int buttonFontSize,
            Action<string> onSelected)
        {
            try
            {
                var webView = new WebView2
                {
                    Left = 0,
                    Top = 0,
                    Width = width,
                    Height = height,
                    AllowExternalDrop = false,
                    DefaultBackgroundColor = Color.Transparent,
                    ZoomFactor = 1
                };

                webView.WebMessageReceived += (_, args) =>
                {
                    var selectedOption = args.TryGetWebMessageAsString();
                    if (!string.IsNullOrWhiteSpace(selectedOption) &&
                        options.Any(item => string.Equals(item, selectedOption, StringComparison.Ordinal)))
                    {
                        onSelected(selectedOption);
                    }
                };

                webView.CoreWebView2InitializationCompleted += (_, args) =>
                {
                    if (!args.IsSuccess || webView.CoreWebView2 is null)
                    {
                        return;
                    }

                    webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                    webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                    webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                    webView.NavigateToString(BuildChoiceOptionsHtml(options, buttonHeight, buttonGap, panelVerticalPadding, buttonFontSize));
                };

                _ = webView.EnsureCoreWebView2Async();
                return webView;
            }
            catch
            {
                return null;
            }
        }

        private static string BuildChoiceOptionsHtml(
            IReadOnlyList<string> options,
            int buttonHeight,
            int buttonGap,
            int panelVerticalPadding,
            int buttonFontSize)
        {
            var buttons = new StringBuilder();
            foreach (var option in options)
            {
                var htmlText = System.Net.WebUtility.HtmlEncode(option);
                var jsValue = JsonSerializer.Serialize(option);
                buttons.Append(CultureInfo.InvariantCulture, $"""
                    <button type="button" class="option" onclick='chrome.webview.postMessage({jsValue})'>{htmlText}</button>
                    """);
            }

            return $$"""
                <!doctype html>
                <html>
                <head>
                  <meta charset="utf-8" />
                  <style>
                    html,
                    body {
                      margin: 0;
                      padding: 0;
                      width: 100%;
                      height: 100%;
                      overflow-x: hidden;
                      overflow-y: auto;
                      background: transparent;
                      font-family: "Segoe UI", "Segoe UI Emoji", "Apple Color Emoji", "Noto Color Emoji", sans-serif;
                    }

                    body {
                      scrollbar-width: thin;
                      scrollbar-color: #8fc8d6 transparent;
                    }

                    body::-webkit-scrollbar {
                      width: 8px;
                    }

                    body::-webkit-scrollbar-track {
                      background: transparent;
                    }

                    body::-webkit-scrollbar-thumb {
                      background: #8fc8d6;
                      border-radius: 8px;
                    }

                    .options {
                      display: flex;
                      flex-direction: column;
                      gap: {{buttonGap}}px;
                      width: 100%;
                      min-height: 100%;
                      padding: 0 8px {{panelVerticalPadding}}px 0;
                      box-sizing: border-box;
                    }

                    .option {
                      width: 100%;
                      height: {{buttonHeight}}px;
                      min-height: {{buttonHeight}}px;
                      flex: 0 0 {{buttonHeight}}px;
                      border: 1px solid #b7d6e0;
                      background: #ffffff;
                      color: #073544;
                      border-radius: 0;
                      font-family: "Segoe UI", "Segoe UI Emoji", "Apple Color Emoji", "Noto Color Emoji", sans-serif;
                      font-size: {{buttonFontSize}}px;
                      font-weight: 400;
                      line-height: 1;
                      text-align: center;
                      cursor: pointer;
                      appearance: none;
                      box-sizing: border-box;
                      display: flex;
                      align-items: center;
                      justify-content: center;
                    }

                    .option:hover {
                      background: #eff9fc;
                      border-color: #8fc8d6;
                    }

                    .option:active {
                      background: #dff5f8;
                    }
                  </style>
                </head>
                <body>
                  <div class="options">
                    {{buttons}}
                  </div>
                </body>
                </html>
                """;
        }

        private static ElementHost CreateChoiceOptionHost(string text, int width, int height, Action onClick)
        {
            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = text,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji"),
                FontSize = 20,
                FontWeight = System.Windows.FontWeights.Normal,
                TextAlignment = System.Windows.TextAlignment.Center,
                TextWrapping = System.Windows.TextWrapping.Wrap,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Foreground = new System.Windows.Media.SolidColorBrush(ToMediaColor(SolvoDeep))
            };

            var button = new System.Windows.Controls.Button
            {
                Content = textBlock,
                Width = width,
                Height = height,
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(ToMediaColor(Border)),
                BorderThickness = new System.Windows.Thickness(1),
                Padding = new System.Windows.Thickness(10, 0, 10, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Focusable = false,
                UseLayoutRounding = true
            };

            button.Click += (_, _) => onClick();

            return new ElementHost
            {
                Width = width,
                Height = height,
                BackColor = Color.Transparent,
                Child = button
            };
        }

        private static System.Windows.Media.Color ToMediaColor(Color color)
            => System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);

        private static bool HasMetadataFlag(string scheduleRule, string flag)
        {
            if (string.IsNullOrWhiteSpace(scheduleRule) || string.IsNullOrWhiteSpace(flag))
            {
                return false;
            }

            var split = scheduleRule.Split('#', 2, StringSplitOptions.TrimEntries);
            if (split.Length < 2 || string.IsNullOrWhiteSpace(split[1]))
            {
                return false;
            }

            return split[1]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(item => string.Equals(item, flag, StringComparison.OrdinalIgnoreCase));
        }

        public void RequestSystemClose()
        {
            allowSystemClose = true;
            Close();
        }

        private void CloseInternal()
        {
            allowSystemClose = true;
            Close();
        }

        private void PostponeDefault()
        {
            PostponeFor = TimeSpan.FromMinutes(30);
            CloseInternal();
        }

        private void OnFormClosingGuard(object? sender, FormClosingEventArgs args)
        {
            if (!requireResponse || allowSystemClose)
            {
                return;
            }

            if (args.CloseReason == CloseReason.UserClosing)
            {
                args.Cancel = true;
                progressLabel.Text = RequiredResponseNotice;
                progressLabel.ForeColor = Color.FromArgb(209, 94, 20);
            }
        }

        private void OnVisibilityGuardTick(object? sender, EventArgs eventArgs)
            => EnforcePromptVisibility();

        private void EnforcePromptVisibility()
        {
            if (!IsHandleCreated || IsDisposed)
            {
                return;
            }

            TopMost = true;
            ShowWindow(Handle, SW_SHOWNORMAL);
            SetWindowPos(Handle, HWND_TOPMOST, Left, Top, Width, Height, SWP_NOACTIVATE);
            BringToFront();
            Activate();
            Focus();
            SetForegroundWindow(Handle);
        }

        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private const int SW_SHOWNORMAL = 1;
        private const uint SWP_NOACTIVATE = 0x0010;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);
    }
}
