using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Velopack;

namespace PulseCheck.Agent;

public sealed class AutoUpdateService(
    ILogger<AutoUpdateService> logger,
    IOptions<AgentOptions> options,
    AgentRuntimeState runtimeState,
    IHttpClientFactory httpClientFactory,
    IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
{
    private const string WindowsServiceName = "PulseCheckAgentService";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var interval = TimeSpan.FromMinutes(Math.Max(settings.UpdateCheckIntervalMinutes, 5));

        if (!settings.AutoUpdateEnabled)
        {
            DiagnosticLog.Write("Auto-update disabled by configuration.");
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.UpdateFeedUrl))
        {
            DiagnosticLog.Write("Auto-update disabled because UpdateFeedUrl is missing.");
            return;
        }

        UpdateManager updateManager;
        try
        {
            updateManager = new UpdateManager(settings.UpdateFeedUrl);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Failed to initialize auto-update manager: {exception.Message}");
            logger.LogWarning(exception, "Failed to initialize auto-update manager.");
            DiagnosticLog.Write("Falling back to legacy zip updates because Velopack initialization failed.");
            await RunLegacyLoopAsync(settings, interval, stoppingToken);
            return;
        }

        if (!updateManager.IsInstalled)
        {
            DiagnosticLog.Write("Current installation is not Velopack-managed. Falling back to legacy zip updates.");
            logger.LogInformation("Current installation is not Velopack-managed. Falling back to legacy zip updates.");
            await RunLegacyLoopAsync(settings, interval, stoppingToken);
            return;
        }

        DiagnosticLog.Write($"Auto-update enabled against {settings.UpdateFeedUrl} with interval {interval}.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForUpdatesAsync(updateManager, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Write($"Auto-update check failed: {exception.Message}");
                logger.LogWarning(exception, "Auto-update check failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task RunLegacyLoopAsync(AgentOptions settings, TimeSpan interval, CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(settings.LegacyUpdateManifestUrl))
        {
            DiagnosticLog.Write("Legacy auto-update disabled because LegacyUpdateManifestUrl is missing.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckLegacyUpdateAsync(settings, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Write($"Legacy auto-update failed: {exception.Message}");
                logger.LogWarning(exception, "Legacy auto-update failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task CheckForUpdatesAsync(UpdateManager updateManager, CancellationToken cancellationToken)
    {
        if (updateManager.UpdatePendingRestart is { } pendingUpdate)
        {
            DiagnosticLog.Write($"Pending update detected: {pendingUpdate.Version}. Applying on next safe window.");
            await ApplyWhenSafeAsync(updateManager, pendingUpdate, cancellationToken);
            return;
        }

        var updateInfo = await updateManager.CheckForUpdatesAsync();
        if (updateInfo is null)
        {
            DiagnosticLog.Write("Auto-update check completed. No updates available.");
            return;
        }

        DiagnosticLog.Write($"Auto-update found version {updateInfo.TargetFullRelease.Version}. Downloading package.");
        await updateManager.DownloadUpdatesAsync(
            updateInfo,
            progress => DiagnosticLog.Write($"Auto-update download progress: {progress}%"),
            cancellationToken);

        DiagnosticLog.Write($"Auto-update package downloaded: {updateInfo.TargetFullRelease.Version}.");
        await ApplyWhenSafeAsync(updateManager, updateInfo.TargetFullRelease, cancellationToken);
    }

    private async Task ApplyWhenSafeAsync(UpdateManager updateManager, VelopackAsset targetRelease, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var snapshot = runtimeState.GetSnapshot();
            if (CanApplyUpdate(snapshot))
            {
                DiagnosticLog.Write($"Applying auto-update to version {targetRelease.Version}.");
                logger.LogInformation("Applying auto-update to version {Version}.", targetRelease.Version);

                updateManager.WaitExitThenApplyUpdates(
                    targetRelease,
                    silent: true,
                    restart: true,
                    restartArgs: []);

                hostApplicationLifetime.StopApplication();
                return;
            }

            DiagnosticLog.Write($"Auto-update waiting for idle state. Current status: {snapshot.Status}.");
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }
    }

    private async Task CheckLegacyUpdateAsync(AgentOptions settings, CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient();
        var manifest = await client.GetFromJsonAsync<LegacyUpdateManifest>(settings.LegacyUpdateManifestUrl, cancellationToken);
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version) || string.IsNullOrWhiteSpace(manifest.PackageUrl))
        {
            DiagnosticLog.Write("Legacy auto-update skipped because manifest is invalid.");
            return;
        }

        if (!Uri.TryCreate(manifest.PackageUrl, UriKind.Absolute, out var packageUri) ||
            !string.Equals(packageUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            DiagnosticLog.Write("Legacy auto-update skipped because package URL is not a valid HTTPS URL.");
            return;
        }

        if (!TryParseVersion(settings.AgentVersion, out var currentVersion) ||
            !TryParseVersion(manifest.Version, out var targetVersion))
        {
            DiagnosticLog.Write($"Legacy auto-update skipped because version parse failed. Current={settings.AgentVersion}, Target={manifest.Version}");
            return;
        }

        if (targetVersion <= currentVersion)
        {
            DiagnosticLog.Write("Legacy auto-update check completed. No newer package available.");
            return;
        }

        DiagnosticLog.Write($"Legacy auto-update found newer version {manifest.Version}. Downloading package.");

        while (!cancellationToken.IsCancellationRequested)
        {
            var snapshot = runtimeState.GetSnapshot();
            if (!CanApplyUpdate(snapshot))
            {
                DiagnosticLog.Write($"Legacy auto-update waiting for idle state. Current status: {snapshot.Status}.");
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                continue;
            }

            var tempRoot = Path.Combine(Path.GetTempPath(), "PulseCheck.Agent.Update", manifest.Version);
            var packagePath = Path.Combine(tempRoot, "agent-update.zip");
            var extractPath = Path.Combine(tempRoot, "content");

            Directory.CreateDirectory(tempRoot);
            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, recursive: true);
            }

            await using (var stream = await client.GetStreamAsync(packageUri, cancellationToken))
            await using (var file = File.Create(packagePath))
            {
                await stream.CopyToAsync(file, cancellationToken);
            }

            if (!await ValidatePackageHashAsync(client, packagePath, manifest, cancellationToken))
            {
                DiagnosticLog.Write("Legacy auto-update skipped because package SHA256 validation failed.");
                return;
            }

            ZipFile.ExtractToDirectory(packagePath, extractPath, overwriteFiles: true);

            var scriptPath = Path.Combine(tempRoot, "apply-update.cmd");
            var appDirectory = ResolveLegacyUpdateTargetDirectory();
            var executablePath = Path.Combine(appDirectory, "PulseCheck.Agent.exe");
            var restartCommand = IsRunningFromServiceSubdirectory()
                ? $"sc start {WindowsServiceName} >nul 2>nul"
                : $@"start """" ""{executablePath}""";
            var script = $@"@echo off
setlocal
sc stop {WindowsServiceName} >nul 2>nul
taskkill /IM PulseCheck.Agent.exe /F >nul 2>nul
timeout /t 3 /nobreak >nul
robocopy ""{extractPath}"" ""{appDirectory}"" /E /NFL /NDL /NJH /NJS /NP /R:5 /W:1 >nul
{restartCommand}
";

            await File.WriteAllTextAsync(scriptPath, script, cancellationToken);
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{scriptPath}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            DiagnosticLog.Write($"Legacy auto-update applying version {manifest.Version} from package {manifest.PackageUrl}.");
            hostApplicationLifetime.StopApplication();
            return;
        }
    }

    private static bool CanApplyUpdate(AgentRuntimeSnapshot snapshot)
    {
        return !string.Equals(snapshot.Status, "Esperando respuesta", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveLegacyUpdateTargetDirectory()
    {
        var currentDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        if (!IsRunningFromServiceSubdirectory())
        {
            return currentDirectory;
        }

        return Directory.GetParent(currentDirectory)?.FullName ?? currentDirectory;
    }

    private static bool IsRunningFromServiceSubdirectory()
    {
        var currentDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        return string.Equals(
            Path.GetFileName(currentDirectory),
            "service",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseVersion(string value, out Version version)
    {
        if (Version.TryParse(value, out version!))
        {
            return true;
        }

        var sanitized = value.Split('-', '+')[0];
        return Version.TryParse(sanitized, out version!);
    }

    private static async Task<bool> ValidatePackageHashAsync(
        HttpClient client,
        string packagePath,
        LegacyUpdateManifest manifest,
        CancellationToken cancellationToken)
    {
        var expectedHash = manifest.PackageSha256;
        if (string.IsNullOrWhiteSpace(expectedHash) && !string.IsNullOrWhiteSpace(manifest.PackageSha256Url))
        {
            if (!Uri.TryCreate(manifest.PackageSha256Url, UriKind.Absolute, out var hashUri) ||
                !string.Equals(hashUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            expectedHash = await client.GetStringAsync(hashUri, cancellationToken);
        }

        expectedHash = NormalizeSha256(expectedHash);
        if (expectedHash is null)
        {
            return false;
        }

        await using var file = File.OpenRead(packagePath);
        var actualHash = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(file, cancellationToken));
        return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeSha256(string? value)
    {
        var token = value?
            .Trim()
            .Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        return token is { Length: 64 } && token.All(Uri.IsHexDigit) ? token.ToUpperInvariant() : null;
    }

    private sealed record LegacyUpdateManifest(
        string Version,
        string PackageUrl,
        string? PackageSha256,
        string? PackageSha256Url,
        string? InstallerUrl,
        string? InstallerSha256Url,
        string? PublishedAtUtc);
}
