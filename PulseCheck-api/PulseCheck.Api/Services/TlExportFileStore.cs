namespace PulseCheck.Api.Services;

public sealed class TlExportFileStore(IWebHostEnvironment environment)
{
    public string CreateExportPath(Guid exportId, string fileName)
    {
        var directory = ResolveExportDirectory();
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{exportId:N}-{fileName}");
    }

    public bool Exists(string? path)
        => !string.IsNullOrWhiteSpace(path) && File.Exists(path);

    public FileStream OpenRead(string path)
        => File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);

    private string ResolveExportDirectory()
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrWhiteSpace(home))
        {
            return Path.Combine(home, "data", "pulsecheck-exports");
        }

        return Path.Combine(environment.ContentRootPath, "App_Data", "exports");
    }
}
