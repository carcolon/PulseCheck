using System.Diagnostics;

namespace PulseCheck.Agent;

public sealed class AgentIdentityResolver
{
    public AgentRuntimeIdentity Resolve(AgentOptions options, bool preferInteractiveIdentityStore = true)
    {
        var interactiveIdentity = preferInteractiveIdentityStore && IsMachineStorageEnabled()
            ? InteractiveUserIdentityStore.ReadFresh(TimeSpan.FromMinutes(10))
            : null;
        var windowsUser = Environment.UserName;
        var domain = Environment.UserDomainName;
        var compositeUser = string.IsNullOrWhiteSpace(domain) ? windowsUser : $"{domain}\\{windowsUser}";
        var machineName = Environment.MachineName;
        var osVersion = Environment.OSVersion.VersionString;

        var userId = IsPlaceholderUserId(options.UserId)
            ? interactiveIdentity?.UserId ?? compositeUser
            : options.UserId.Trim();

        var userName = IsPlaceholderUserName(options.UserName)
            ? interactiveIdentity?.UserName ?? windowsUser
            : options.UserName.Trim();

        var email = interactiveIdentity?.Email ?? ResolveCorporateEmail();
        if (string.IsNullOrWhiteSpace(email))
        {
            email = IsPlaceholderEmail(options.Email) ? string.Empty : options.Email.Trim();
        }

        var deviceId = IsPlaceholderDeviceId(options.DeviceId)
            ? machineName.ToLowerInvariant()
            : options.DeviceId.Trim();

        var hostname = IsPlaceholderHostname(options.Hostname)
            ? machineName
            : options.Hostname.Trim();

        var operatingSystem = IsPlaceholderOperatingSystem(options.OperatingSystem)
            ? osVersion
            : options.OperatingSystem.Trim();

        return new AgentRuntimeIdentity(
            userId,
            userName,
            email ?? string.Empty,
            string.IsNullOrWhiteSpace(interactiveIdentity?.Department) ? options.Department : interactiveIdentity.Department,
            deviceId,
            hostname,
            operatingSystem,
            options.AgentVersion);
    }

    private static bool IsMachineStorageEnabled() =>
        string.Equals(
            Environment.GetEnvironmentVariable("PULSECHECK_AGENT_MACHINE_STORAGE"),
            "1",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsPlaceholderUserId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "u-agent-demo" or "u-agent-dev" or "u-agent-prod";
    }

    private static bool IsPlaceholderUserName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "usuario demo" or "usuario desarrollo" or "usuario corporativo";
    }

    private static bool IsPlaceholderEmail(string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }

    private static bool IsPlaceholderDeviceId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return value.Trim().ToLowerInvariant().StartsWith("pc-agent-", StringComparison.Ordinal);
    }

    private static bool IsPlaceholderHostname(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return value.Trim().ToLowerInvariant().StartsWith("pc-agent-", StringComparison.Ordinal);
    }

    private static bool IsPlaceholderOperatingSystem(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "windows 11" or "windows 10";
    }

    private static string? ResolveCorporateEmail()
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "whoami",
                Arguments = "/upn",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(3000);

            return output.Contains('@') ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
