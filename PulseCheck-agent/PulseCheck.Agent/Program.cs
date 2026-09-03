using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PulseCheck.Agent;
using Velopack;

internal static class Program
{
    private const string WatchdogArgument = "--watchdog";
    private const string TrayOnlyArgument = "--tray";

    private static string ResolveEnvironmentName()
    {
        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if (string.IsNullOrWhiteSpace(environmentName))
        {
            environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        }

        return string.IsNullOrWhiteSpace(environmentName) ? Environments.Development : environmentName;
    }

    [STAThread]
    private static async Task Main(string[] args)
    {
        if (RunWatchdogIfRequested(args))
        {
            return;
        }

        VelopackApp.Build().Run();
        ApplicationConfiguration.Initialize();
        DiagnosticLog.Write("Program start.");
        var trayOnly = args.Any(item => string.Equals(item, TrayOnlyArgument, StringComparison.OrdinalIgnoreCase));

        using var mutex = new Mutex(true, trayOnly ? "PulseCheck.Agent.Tray.Singleton" : "PulseCheck.Agent.Singleton", out var isFirstInstance);
        if (!isFirstInstance)
        {
            DiagnosticLog.Write("Another instance is already running.");
            MessageBox.Show(
                "PulseCheck ya se está ejecutando en este equipo.",
                "PulseCheck",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        StartWatchdog(trayOnly);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = ResolveEnvironmentName()
        });
        builder.Configuration.AddJsonFile("appsettings.runtime.json", optional: true, reloadOnChange: false);
        builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
        builder.Services.AddHttpClient<PulseCheckApiClient>();
        builder.Services.AddSingleton<AgentCredentialStore>();
        builder.Services.AddSingleton<AgentIdentityResolver>();
        builder.Services.AddSingleton<AgentRuntimeState>();
        builder.Services.AddSingleton<LocalQueueStore>();
        builder.Services.AddSingleton<AgentActivityQueueStore>();
        builder.Services.AddSingleton<AgentActivityQueueService>();
        builder.Services.AddSingleton<LocalAgentStateStore>();
        builder.Services.AddSingleton<SyncWakeSignal>();
        builder.Services.AddSingleton<WindowsPromptService>();
        builder.Services.AddSingleton<ICampaignPromptService>(services => services.GetRequiredService<WindowsPromptService>());

        if (trayOnly)
        {
            builder.Services.AddSingleton<InteractiveUserIdentityStore>();
            builder.Services.AddHostedService<UserCredentialMigrationService>();
            builder.Services.AddHostedService<InteractiveIdentityReporterService>();
            builder.Services.AddHostedService<TrayPromptPipeServer>();
        }
        else
        {
            builder.Services.AddHostedService<CampaignNotificationListener>();
            builder.Services.AddHostedService<SessionActivityMonitorService>();
            builder.Services.AddHostedService<AutoUpdateService>();
            builder.Services.AddHostedService<Worker>();
        }

        using var host = builder.Build();
        DiagnosticLog.Write("Host built. Starting background services.");
        await host.StartAsync();
        DiagnosticLog.Write("Host started. Opening tray context.");

        using var trayContext = ActivatorUtilities.CreateInstance<TrayAgentApplicationContext>(host.Services);
        Application.Run(trayContext);

        DiagnosticLog.Write("Tray loop finished. Stopping host.");
        await host.StopAsync();
        DiagnosticLog.Write("Host stopped.");
    }

    private static bool RunWatchdogIfRequested(string[] args)
    {
        if (args.Length is not (2 or 3) ||
            !string.Equals(args[0], WatchdogArgument, StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(args[1], out var parentProcessId))
        {
            return false;
        }

        var shouldRestart = true;
        var restartTrayOnly = args.Length == 3 &&
                              string.Equals(args[2], TrayOnlyArgument, StringComparison.OrdinalIgnoreCase);

        try
        {
            using var parentProcess = Process.GetProcessById(parentProcessId);
            parentProcess.WaitForExit();
            shouldRestart = parentProcess.ExitCode != 0;
        }
        catch
        {
            // The parent process is already gone; continue with the restart attempt.
        }

        if (!shouldRestart)
        {
            DiagnosticLog.Write("Watchdog observed graceful shutdown; restart skipped.");
            return true;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? Application.ExecutablePath,
                Arguments = restartTrayOnly ? TrayOnlyArgument : string.Empty,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Watchdog restart failed: {exception.Message}");
        }

        return true;
    }

    private static void StartWatchdog(bool trayOnly)
    {
        try
        {
            var executablePath = Environment.ProcessPath ?? Application.ExecutablePath;
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = trayOnly
                    ? $"{WatchdogArgument} {Environment.ProcessId} {TrayOnlyArgument}"
                    : $"{WatchdogArgument} {Environment.ProcessId}",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Watchdog start failed: {exception.Message}");
        }
    }
}
