using Headhunter.Database;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Headhunter.Tests;

/// <summary>
/// Starts one SQL Server 2025 container for the whole test run, applies the EF Core
/// migrations and seeds the synthetic data set from <see cref="TestData"/>.
/// </summary>
[SetUpFixture]
public class SqlServerFixture
{
    // Same image and digest as the homelab deployment (home/manifests/headhunter/headhunter.yml).
    public const string SqlServerImage =
        "mcr.microsoft.com/mssql/server:2025-CU8-ubuntu-24.04@sha256:4bab24f36c1ecd48e85f7d37df26e6bf301641d84c3fe652f9a0dcc947d512e1";

    private static MsSqlContainer? _container;

    public static string ConnectionString { get; private set; } = "";

    public static HeadhunterContext CreateContext() =>
        new(new DbContextOptionsBuilder<HeadhunterContext>().UseSqlServer(ConnectionString).Options);

    [OneTimeSetUp]
    public async Task StartSqlServer()
    {
        _container = new MsSqlBuilder(SqlServerImage)
            .Build();
        await _container.StartAsync();

        // Use a named database rather than master so migrations run like they do in production.
        ConnectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "Headhunter",
        }.ConnectionString;

        await using var context = CreateContext();
        // Throws if the model has changes that aren't captured in a migration (PendingModelChangesWarning).
        await context.Database.MigrateAsync();
        await TestData.SeedAsync(context);
    }

    [OneTimeTearDown]
    public async Task StopSqlServer()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
