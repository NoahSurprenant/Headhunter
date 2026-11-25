using Headhunter.Database;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace Headhunter.Tests;

/// <summary>
/// Starts one database container for the whole test run, applies the EF Core migrations and seeds the
/// synthetic data set from <see cref="TestData"/>. SQL Server 2025 unless HEADHUNTER_TEST_DATABASE=Postgres
/// (PostgreSQL 18); CI runs both.
/// </summary>
[SetUpFixture]
public class DatabaseFixture
{
    // Same image and digest as the homelab deployment (home/manifests/headhunter/headhunter.yml).
    public const string SqlServerImage =
        "mcr.microsoft.com/mssql/server:2025-CU8-ubuntu-24.04@sha256:4bab24f36c1ecd48e85f7d37df26e6bf301641d84c3fe652f9a0dcc947d512e1";

    // LIKE on the case-insensitive (nondeterministic) collation needs PostgreSQL 18.
    public const string PostgresImage =
        "postgres:18@sha256:c699303802144847d238196173655dbbecf7a96aeca1565f2b02eb671a1f0b6b";

    private static MsSqlContainer? _sqlServer;
    private static PostgreSqlContainer? _postgres;

    public static DatabaseProvider Provider { get; } = ReadProvider();

    private static DatabaseProvider ReadProvider()
    {
        var value = Environment.GetEnvironmentVariable("HEADHUNTER_TEST_DATABASE");
        if (string.IsNullOrEmpty(value)) return DatabaseProvider.SqlServer;
        return Enum.TryParse<DatabaseProvider>(value, ignoreCase: true, out var p)
            ? p
            : throw new InvalidOperationException($"HEADHUNTER_TEST_DATABASE=\"{value}\" is not a database provider (SqlServer or Postgres).");
    }

    public static string ConnectionString { get; private set; } = "";

    public static HeadhunterContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<HeadhunterContext>();
        builder.UseHeadhunterDatabase(Provider, ConnectionString);
        return new HeadhunterContext(builder.Options);
    }

    /// <summary>
    /// A new, migrated, empty database on the same container, for tests that load or change a lot of data
    /// (the shared one holds <see cref="TestData"/> that other tests count).
    /// </summary>
    public static async Task<Func<HeadhunterContext>> CreateScratchDatabaseAsync(string name)
    {
        await using (var admin = CreateContext())
        {
            await admin.Database.ExecuteSqlRawAsync(Provider == DatabaseProvider.Postgres
                ? $"CREATE DATABASE \"{name}\""
                : $"CREATE DATABASE [{name}]");
        }

        var connectionString = Provider == DatabaseProvider.Postgres
            ? new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString
            : new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = name }.ConnectionString;

        HeadhunterContext Create()
        {
            var builder = new DbContextOptionsBuilder<HeadhunterContext>();
            builder.UseHeadhunterDatabase(Provider, connectionString);
            return new HeadhunterContext(builder.Options);
        }

        await using (var context = Create()) await context.Database.MigrateAsync();
        return Create;
    }

    [OneTimeSetUp]
    public async Task StartDatabase()
    {
        if (Provider == DatabaseProvider.Postgres)
        {
            _postgres = new PostgreSqlBuilder(PostgresImage).WithDatabase("headhunter").Build();
            await _postgres.StartAsync();
            ConnectionString = _postgres.GetConnectionString();
        }
        else
        {
            _sqlServer = new MsSqlBuilder(SqlServerImage).Build();
            await _sqlServer.StartAsync();

            // Use a named database rather than master so migrations run like they do in production.
            ConnectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_sqlServer.GetConnectionString())
            {
                InitialCatalog = "Headhunter",
            }.ConnectionString;
        }

        await using var context = CreateContext();
        // Throws if the model has changes that aren't captured in a migration (PendingModelChangesWarning).
        await context.Database.MigrateAsync();
        await TestData.SeedAsync(context);
    }

    [OneTimeTearDown]
    public async Task StopDatabase()
    {
        if (_sqlServer is not null) await _sqlServer.DisposeAsync();
        if (_postgres is not null) await _postgres.DisposeAsync();
    }
}
