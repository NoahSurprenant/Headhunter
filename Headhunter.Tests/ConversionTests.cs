using Headhunter.Database;
using Headhunter.Database.Conversion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace Headhunter.Tests;

/// <summary>
/// The SQL Server to PostgreSQL data copy (and back): the shared database's synthetic data copied into a new
/// database on the same server and into one on the other kind of server, which these tests start themselves.
/// </summary>
[TestFixture]
public class ConversionTests
{
    private static readonly DatabaseConverter Converter = new(NullLogger<DatabaseConverter>.Instance);

    private static DatabaseEndpoint Source => new(DatabaseFixture.Provider, DatabaseFixture.ConnectionString);

    [Test]
    public async Task Convert_CopiesEveryRow_ToANewDatabaseOnTheSameServer()
    {
        var target = await DatabaseFixture.CreateScratchDatabaseAsync($"convert_{Guid.NewGuid():N}");
        await using var targetContext = target();

        var tables = await Converter.ConvertAsync(Source,
            new DatabaseEndpoint(DatabaseFixture.Provider, targetContext.Database.GetConnectionString()!));

        await AssertSameData(targetContext, tables);
    }

    [Test]
    public async Task Convert_RefusesATargetThatHasRows()
    {
        var target = await DatabaseFixture.CreateScratchDatabaseAsync($"convert_{Guid.NewGuid():N}");
        await using var targetContext = target();
        var endpoint = new DatabaseEndpoint(DatabaseFixture.Provider, targetContext.Database.GetConnectionString()!);
        await Converter.ConvertAsync(Source, endpoint);

        var error = await Assert.ThrowsAsync<ConversionException>(() => Converter.ConvertAsync(Source, endpoint));

        Assert.That(error!.Message, Does.Contain("already has rows"));
    }

    [Test]
    public async Task Convert_RefusesASourceMissingMigrations()
    {
        var name = $"convert_{Guid.NewGuid():N}";
        await using (var admin = DatabaseFixture.CreateContext())
            await admin.Database.ExecuteSqlRawAsync(DatabaseFixture.Provider == DatabaseProvider.Postgres
                ? $"CREATE DATABASE \"{name}\"" : $"CREATE DATABASE [{name}]");
        var empty = DatabaseFixture.Provider == DatabaseProvider.Postgres
            ? new Npgsql.NpgsqlConnectionStringBuilder(DatabaseFixture.ConnectionString) { Database = name }.ConnectionString
            : new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(DatabaseFixture.ConnectionString) { InitialCatalog = name }.ConnectionString;

        var error = await Assert.ThrowsAsync<ConversionException>(() => Converter.ConvertAsync(
            new DatabaseEndpoint(DatabaseFixture.Provider, empty), Source));

        Assert.That(error!.Message, Does.Contain("missing migrations"));
    }

    [Test]
    public async Task Convert_CopiesEveryRow_ToTheOtherKindOfServer()
    {
        var other = DatabaseFixture.Provider == DatabaseProvider.Postgres ? DatabaseProvider.SqlServer : DatabaseProvider.Postgres;
        await using var container = await StartAsync(other);
        var targetEndpoint = new DatabaseEndpoint(other, container.ConnectionString);

        var tables = await Converter.ConvertAsync(Source, targetEndpoint);
        await using var targetContext = DatabaseConverter.NewContext(targetEndpoint);
        await AssertSameData(targetContext, tables);

        // And back, into a database on this server.
        var back = await DatabaseFixture.CreateScratchDatabaseAsync($"convert_{Guid.NewGuid():N}");
        await using var backContext = back();
        var backTables = await Converter.ConvertAsync(targetEndpoint,
            new DatabaseEndpoint(DatabaseFixture.Provider, backContext.Database.GetConnectionString()!));
        await AssertSameData(backContext, backTables);
    }

    private static async Task AssertSameData(HeadhunterContext target, IReadOnlyList<ConvertedTable> tables)
    {
        await using var source = DatabaseFixture.CreateContext();

        var addresses = await source.Addresses.LongCountAsync();
        var records = await source.MichiganVoterRecords.LongCountAsync();
        var voters = await source.Voters.LongCountAsync();
        var elections = await source.Elections.LongCountAsync();
        var history = await source.VoteHistory.LongCountAsync();
        Assert.That(history, Is.GreaterThan(0));
        Assert.That(voters, Is.GreaterThan(0));
        Assert.That(tables.Select(t => (t.Name, t.Rows)), Is.EqualTo(new[]
        {
            ("Addresses", addresses), ("MichiganVoterRecords", records), ("Voters", voters),
            ("Elections", elections), ("VoteHistory", history),
        }));
        Assert.That(await target.Addresses.LongCountAsync(), Is.EqualTo(addresses));
        Assert.That(await target.MichiganVoterRecords.LongCountAsync(), Is.EqualTo(records));
        Assert.That(await target.Voters.LongCountAsync(), Is.EqualTo(voters));

        // Every row's values, by key (the two servers order keys differently).
        var expectedVoters = (await source.Voters.AsNoTracking()
            .Select(v => new { v.ID, v.LAST_NAME, v.FIRST_NAME, v.YEAR_OF_BIRTH, v.REGISTRATION_DATE, v.AddressID, v.HAS_MAILING_ADDRESS, v.IS_PERM_AV_BALLOT_VOTER })
            .ToListAsync()).OrderBy(v => v.ID).ToList();
        var actualVoters = (await target.Voters.AsNoTracking()
            .Select(v => new { v.ID, v.LAST_NAME, v.FIRST_NAME, v.YEAR_OF_BIRTH, v.REGISTRATION_DATE, v.AddressID, v.HAS_MAILING_ADDRESS, v.IS_PERM_AV_BALLOT_VOTER })
            .ToListAsync()).OrderBy(v => v.ID).ToList();
        Assert.That(actualVoters, Is.EqualTo(expectedVoters));

        var expectedAddresses = (await source.Addresses.AsNoTracking()
            .Select(a => new { a.ID, a.FullStreetAddress, a.CITY, a.ZIP_CODE, a.Latitude, a.Longitude, a.Matched }).ToListAsync())
            .OrderBy(a => a.ID).ToList();
        var actualAddresses = (await target.Addresses.AsNoTracking()
            .Select(a => new { a.ID, a.FullStreetAddress, a.CITY, a.ZIP_CODE, a.Latitude, a.Longitude, a.Matched }).ToListAsync())
            .OrderBy(a => a.ID).ToList();
        Assert.That(actualAddresses, Is.EqualTo(expectedAddresses));

        // The code tables and the vote history that points at them.
        var expectedHistory = (await source.VoteHistory.AsNoTracking().Select(h => new { h.VoterID, h.ElectionDate, h.CountyCode }).ToListAsync())
            .OrderBy(h => h.VoterID).ThenBy(h => h.ElectionDate).ToList();
        var actualHistory = (await target.VoteHistory.AsNoTracking().Select(h => new { h.VoterID, h.ElectionDate, h.CountyCode }).ToListAsync())
            .OrderBy(h => h.VoterID).ThenBy(h => h.ElectionDate).ToList();
        Assert.That(actualHistory, Is.EqualTo(expectedHistory));
        Assert.That((await target.Elections.AsNoTracking().OrderBy(e => e.Date).ToListAsync()).Select(e => (e.Date, e.CodeId, e.Description)),
            Is.EqualTo((await source.Elections.AsNoTracking().OrderBy(e => e.Date).ToListAsync()).Select(e => (e.Date, e.CodeId, e.Description))));
        Assert.That((await target.Counties.AsNoTracking().OrderBy(c => c.Code).ToListAsync()).Select(c => (c.Code, c.Name)),
            Is.EqualTo((await source.Counties.AsNoTracking().OrderBy(c => c.Code).ToListAsync()).Select(c => (c.Code, c.Name))));
    }

    private sealed class Container(IAsyncDisposable inner, string connectionString) : IAsyncDisposable
    {
        public string ConnectionString { get; } = connectionString;
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private static async Task<Container> StartAsync(DatabaseProvider provider)
    {
        if (provider == DatabaseProvider.Postgres)
        {
            var postgres = new PostgreSqlBuilder(DatabaseFixture.PostgresImage).WithDatabase("headhunter").Build();
            await postgres.StartAsync();
            return new Container(postgres, postgres.GetConnectionString());
        }

        var sqlServer = new MsSqlBuilder(DatabaseFixture.SqlServerImage).Build();
        await sqlServer.StartAsync();
        return new Container(sqlServer, new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sqlServer.GetConnectionString())
        {
            InitialCatalog = "Headhunter",
        }.ConnectionString);
    }
}
