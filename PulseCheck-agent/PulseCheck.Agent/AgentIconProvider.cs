using System.Drawing;
using System.Windows.Forms;

namespace PulseCheck.Agent;

internal static class AgentIconProvider
{
    private static readonly Lazy<Icon> SharedIcon = new(LoadSharedIcon);

    public static Icon CreateIcon()
    {
        return (Icon)SharedIcon.Value.Clone();
    }

    public static Icon CreateIconWithBadge()
    {
        using var baseIcon = CreateIcon();
        using var bitmap = baseIcon.ToBitmap();
        using var canvas = new Bitmap(bitmap.Width, bitmap.Height);
        using (var graphics = Graphics.FromImage(canvas))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);

            var badgeSize = Math.Max(8, bitmap.Width / 3);
            var badgeX = bitmap.Width - badgeSize - 1;
            var badgeY = 1;
            using var badgeBrush = new SolidBrush(Color.FromArgb(225, 35, 46));
            using var borderBrush = new SolidBrush(Color.White);
            graphics.FillEllipse(borderBrush, badgeX - 1, badgeY - 1, badgeSize + 2, badgeSize + 2);
            graphics.FillEllipse(badgeBrush, badgeX, badgeY, badgeSize, badgeSize);
        }

        return Icon.FromHandle(canvas.GetHicon());
    }

    private static Icon LoadSharedIcon()
    {
        try
        {
            var extracted = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (extracted is not null)
            {
                return (Icon)extracted.Clone();
            }
        }
        catch
        {
        }

        var iconPath = Path.Combine(AppContext.BaseDirectory, "PulseCheck.Agent.ico");
        if (File.Exists(iconPath))
        {
            try
            {
                return new Icon(iconPath);
            }
            catch
            {
            }
        }

        return (Icon)SystemIcons.Shield.Clone();
    }
}
