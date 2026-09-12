namespace PulseCheck.Agent;

public static class AgentStoragePaths
{
    public static string DataDirectory
    {
        get
        {
            if (UseMachineDataDirectory)
            {
                return MachineDataDirectory;
            }

            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PulseCheck",
                "Agent");

            Directory.CreateDirectory(path);
            return path;
        }
    }

    private static bool UseMachineDataDirectory =>
        string.Equals(
            Environment.GetEnvironmentVariable("PULSECHECK_AGENT_MACHINE_STORAGE"),
            "1",
            StringComparison.OrdinalIgnoreCase);

    public static string MachineDataDirectory
    {
        get
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "PulseCheck",
                "Agent");

            Directory.CreateDirectory(path);
            return path;
        }
    }
}
