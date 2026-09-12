using System.Text;

namespace PulseCheck.Agent;

internal static class DiagnosticLog
{
    private static readonly string LogFilePath = Path.Combine(AgentStoragePaths.DataDirectory, "agent.log");

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(AgentStoragePaths.DataDirectory);
            var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(LogFilePath, line, Encoding.UTF8);
        }
        catch
        {
        }
    }
}
