using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PulseCheck.Infrastructure.Persistence;

public sealed class PulseCheckDbContextFactory : IDesignTimeDbContextFactory<PulseCheckDbContext>
{
    public PulseCheckDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PulseCheckDbContext>();
        var provider = Environment.GetEnvironmentVariable("PULSECHECK_DATABASE_PROVIDER") ?? "SqlServer";
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PulseCheckDb")
            ?? Environment.GetEnvironmentVariable("PULSECHECK_DB_CONNECTION_STRING");

        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            options.UseSqlite(string.IsNullOrWhiteSpace(connectionString)
                ? "Data Source=pulsecheck.db"
                : connectionString);
        }
        else
        {
            options.UseSqlServer(string.IsNullOrWhiteSpace(connectionString)
                ? "Server=localhost\\SQLEXPRESS;Database=PulseCheckDb;Trusted_Connection=True;TrustServerCertificate=True;"
                : connectionString);
        }

        return new PulseCheckDbContext(options.Options);
    }
}
