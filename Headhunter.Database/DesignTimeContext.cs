using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Headhunter.Database;

/// <summary>
/// The context dotnet ef and the migrations bundles use, for one provider: each migrations project's
/// IDesignTimeDbContextFactory calls this with its own. The connection string comes from
/// ConnectionStrings:Headhunter (appsettings.json, user secrets, the environment).
/// </summary>
public static class DesignTimeContext
{
    public static HeadhunterContext Create(DatabaseProvider provider)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            // Environment variables last: the migrations bundle in the image (Dockerfile) gets
            // ConnectionStrings__Headhunter from the homelab migration Job.
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddUserSecrets(typeof(DesignTimeContext).Assembly, optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("Headhunter")
            ?? throw new Exception("Failed to find Headhunter connection string in configuration");

        // Not the connection string itself: it carries the password, and the migration Job's output goes to Loki.
        Console.WriteLine($"Using {provider} database {Describe(provider, connectionString)}");

        // Migrations (the bundle in the migration Job) build indexes over all 8M voters, which can take minutes on a
        // small server: well past the default 30 s command timeout.
        var optionsBuilder = new DbContextOptionsBuilder<HeadhunterContext>();
        optionsBuilder.UseHeadhunterDatabase(provider, connectionString, TimeSpan.FromMinutes(30));
        return new HeadhunterContext(optionsBuilder.Options);
    }

    private static string Describe(DatabaseProvider provider, string connectionString)
    {
        if (provider == DatabaseProvider.Postgres)
        {
            var postgres = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
            return $"{postgres.Database} on {postgres.Host}";
        }
        var sql = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        return $"{sql.InitialCatalog} on {sql.DataSource}";
    }
}
