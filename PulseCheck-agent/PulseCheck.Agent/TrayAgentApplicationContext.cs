using System.Diagnostics;
using System.Drawing;
using Microsoft.Extensions.Options;

namespace PulseCheck.Agent;

public sealed class TrayAgentApplicationContext : ApplicationContext
{
    private readonly AgentRuntimeState runtimeState;
    private readonly string agentVersion;
    private readonly Icon trayIcon;
    private readonly Icon pendingTrayIcon;
    private readonly NotifyIcon notifyIcon;
    private readonly ToolStripMenuItem statusMenuItem;
    private readonly ToolStripMenuItem syncMenuItem;
    private readonly System.Windows.Forms.Timer refreshTimer;

    public TrayAgentApplicationContext(AgentRuntimeState runtimeState, IOptions<AgentOptions> options)
    {
        this.runtimeState = runtimeState;
        agentVersion = string.IsNullOrWhiteSpace(options.Value.AgentVersion) ? "desconocida" : options.Value.AgentVersion;

        statusMenuItem = new ToolStripMenuItem("Estado: iniciando") { Enabled = false };
        syncMenuItem = new ToolStripMenuItem("Ultima sincronizacion: pendiente") { Enabled = false };

        var openDataMenuItem = new ToolStripMenuItem("Abrir carpeta de datos");
        openDataMenuItem.Click += (_, _) => OpenDataDirectory();

        trayIcon = AgentIconProvider.CreateIcon();
        pendingTrayIcon = AgentIconProvider.CreateIconWithBadge();
        notifyIcon = new NotifyIcon
        {
            Icon = trayIcon,
            Visible = true,
            Text = BuildTrayText(runtimeState.GetSnapshot(), agentVersion),
            ContextMenuStrip = new ContextMenuStrip()
        };

        notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem($"PulseCheck Agent v{agentVersion}") { Enabled = false });
        notifyIcon.ContextMenuStrip.Items.Add(statusMenuItem);
        notifyIcon.ContextMenuStrip.Items.Add(syncMenuItem);
        notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem($"Dispositivo: {options.Value.Hostname}") { Enabled = false });
        notifyIcon.ContextMenuStrip.Items.Add(openDataMenuItem);

        notifyIcon.DoubleClick += (_, _) => ShowStatusBalloon();
        notifyIcon.ShowBalloonTip(
            2500,
            "PulseCheck",
            $"El agente esta activo en segundo plano. Version {agentVersion}.",
            ToolTipIcon.Info);

        refreshTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        refreshTimer.Tick += (_, _) => RefreshMenu();
        refreshTimer.Start();
        RefreshMenu();
    }

    protected override void ExitThreadCore()
    {
        refreshTimer.Stop();
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        trayIcon.Dispose();
        pendingTrayIcon.Dispose();
        refreshTimer.Dispose();
        base.ExitThreadCore();
    }

    private void RefreshMenu()
    {
        var snapshot = runtimeState.GetSnapshot();
        statusMenuItem.Text = $"Estado: {snapshot.Status}";
        syncMenuItem.Text = snapshot.LastSyncAtUtc is null
            ? "Ultima sincronizacion: pendiente"
            : $"Ultima sincronizacion: {snapshot.LastSyncAtUtc.Value.ToLocalTime():hh:mm tt}";

        notifyIcon.Text = BuildTrayText(snapshot, agentVersion);
        notifyIcon.Icon = snapshot.HasPendingResponses ? pendingTrayIcon : trayIcon;
    }

    private void ShowStatusBalloon()
    {
        var snapshot = runtimeState.GetSnapshot();
        var lines = new List<string>
        {
            $"Estado: {snapshot.Status}",
            $"Version: {agentVersion}"
        };

        if (snapshot.ActiveCampaigns is int activeCampaigns)
        {
            lines.Add($"Campanas activas: {activeCampaigns}");
        }

        if (!string.IsNullOrWhiteSpace(snapshot.LastPromptedCampaign))
        {
            lines.Add($"Ultima campana: {snapshot.LastPromptedCampaign}");
        }

        if (snapshot.HasPendingResponses)
        {
            lines.Add("Hay una campana pendiente por responder.");
        }

        if (!string.IsNullOrWhiteSpace(snapshot.LastError))
        {
            lines.Add($"Error: {snapshot.LastError}");
        }

        notifyIcon.ShowBalloonTip(
            3000,
            "PulseCheck",
            string.Join(Environment.NewLine, lines),
            string.IsNullOrWhiteSpace(snapshot.LastError) ? ToolTipIcon.Info : ToolTipIcon.Warning);
    }

    private static string BuildTrayText(AgentRuntimeSnapshot snapshot, string agentVersion)
    {
        var text = $"PulseCheck v{agentVersion} - {snapshot.Status}";
        return text.Length <= 63 ? text : text[..63];
    }

    private static void OpenDataDirectory()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{AgentStoragePaths.DataDirectory}\"",
            UseShellExecute = true
        });
    }
}
