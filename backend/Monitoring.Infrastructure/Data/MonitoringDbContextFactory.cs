using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Monitoring.Infrastructure.Data;

public sealed class MonitoringDbContextFactory : IDesignTimeDbContextFactory<MonitoringDbContext>
{
    public MonitoringDbContext CreateDbContext(string[] args)
    {
        LoadNearestEnvFile();

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("CONNECTION_STRING")
            ?? BuildConnectionStringFromParts();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string not found. Set ConnectionStrings__DefaultConnection or CONNECTION_STRING, or DB_HOST/DB_PORT/DB_NAME/DB_USER/DB_PASSWORD in backend/.env.");
        }

        var options = new DbContextOptionsBuilder<MonitoringDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new MonitoringDbContext(options);
    }

    private static string? BuildConnectionStringFromParts()
    {
        var host = Environment.GetEnvironmentVariable("DB_HOST");
        var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        var name = Environment.GetEnvironmentVariable("DB_NAME");
        var user = Environment.GetEnvironmentVariable("DB_USER");
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD");

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
            return null;

        return $"Host={host};Port={port};Database={name};Username={user};Password={password}";
    }

    private static void LoadNearestEnvFile()
    {
        var start = Directory.GetCurrentDirectory();
        foreach (var dir in EnumerateParents(start).Concat(GetLikelyRepoDirs(start)))
        {
            var path = Path.Combine(dir, ".env");
            if (File.Exists(path))
            {
                LoadEnvFile(path);
                return;
            }
        }
    }

    private static IEnumerable<string> EnumerateParents(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            yield return dir.FullName;
            dir = dir.Parent;
        }
    }

    private static IEnumerable<string> GetLikelyRepoDirs(string start)
    {
        yield return Path.GetFullPath(Path.Combine(start, ".."));
        yield return Path.GetFullPath(Path.Combine(start, "..", ".."));
        yield return Path.GetFullPath(Path.Combine(start, "..", "..", "backend"));
    }

    private static void LoadEnvFile(string path)
    {
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var idx = line.IndexOf('=');
            if (idx <= 0) continue;

            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim().Trim('"', '\'');
            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
