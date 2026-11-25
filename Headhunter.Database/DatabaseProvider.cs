using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Headhunter.Database;

/// <summary>
/// The database the app runs on: setting Database:Provider, SQL Server unless set. SQL Server's migrations are in
/// this project; Postgres's are in Headhunter.Migrations.Postgres. Both are generated from the same model.
/// </summary>
public enum DatabaseProvider
{
    SqlServer,
    Postgres,
}

public static class DatabaseProviderExtensions
{
    public const string PostgresMigrationsAssembly = "Headhunter.Migrations.Postgres";

    /// <summary>Database:Provider (SqlServer or Postgres, any case); SqlServer when not set.</summary>
    public static DatabaseProvider GetDatabaseProvider(this IConfiguration configuration)
    {
        var value = configuration["Database:Provider"];
        if (string.IsNullOrWhiteSpace(value))
            return DatabaseProvider.SqlServer;
        if (Enum.TryParse<DatabaseProvider>(value, ignoreCase: true, out var provider) && Enum.IsDefined(provider))
            return provider;
        throw new InvalidOperationException($"Database:Provider '{value}' is not one of: {string.Join(", ", Enum.GetNames<DatabaseProvider>())}");
    }

    public static DbContextOptionsBuilder UseHeadhunterDatabase(this DbContextOptionsBuilder options, DatabaseProvider provider,
        string connectionString, TimeSpan? commandTimeout = null)
    {
        return provider switch
        {
            DatabaseProvider.SqlServer => options.UseSqlServer(connectionString, o =>
            {
                if (commandTimeout is { } t) o.CommandTimeout((int)t.TotalSeconds);
            }),
            DatabaseProvider.Postgres => options.UseNpgsql(connectionString, o =>
            {
                o.MigrationsAssembly(PostgresMigrationsAssembly);
                if (commandTimeout is { } t) o.CommandTimeout((int)t.TotalSeconds);
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
        };
    }

    /// <summary>Database:Provider on ConnectionStrings:Headhunter.</summary>
    public static DbContextOptionsBuilder UseHeadhunterDatabase(this DbContextOptionsBuilder options, IConfiguration configuration) =>
        options.UseHeadhunterDatabase(configuration.GetDatabaseProvider(),
            configuration.GetConnectionString("Headhunter") ?? throw new InvalidOperationException("ConnectionStrings:Headhunter is not set"));
}
