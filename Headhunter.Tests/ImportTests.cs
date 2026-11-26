using Headhunter.CSVDump;
using Headhunter.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Headhunter.Tests;

/// <summary>
/// The production load: the CSV lands in MichiganVoterRecords, then InsertAddresses and InsertVoters (raw SQL,
/// one version for both providers) build Addresses and Voters from it. Runs in a scratch database so the
/// shared one keeps only its fixed test data.
/// </summary>
public class ImportTests
{
    public static Worker CreateWorker(Func<HeadhunterContext> create)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => create());
        return new Worker(NullLogger<Worker>.Instance, services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    [Test]
    public async Task Import_GroupsRecordsIntoAddresses_AndLinksEveryVoter_IgnoringCase()
    {
        var create = await DatabaseFixture.CreateScratchDatabaseAsync("import_small");
        await using (var context = create())
        {
            await RecordSeeder.SeedAsync(context, 20);
            // Voters 3, 5, ... spell their street and city in capitals; they still belong to the household
            // of the voter before them (same collation as the app's searches).
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE \"MichiganVoterRecords\" SET \"STREET_NAME\" = UPPER(\"STREET_NAME\"), \"CITY\" = UPPER(\"CITY\") WHERE \"YEAR_OF_BIRTH\" % 2 = 1");
        }

        var worker = CreateWorker(create);
        await worker.InsertAddresses();
        await worker.InsertVoters();

        await using var check = create();
        Assert.Multiple(async () =>
        {
            // Voters 1..20 form households n/2 = 0..10: nine pairs, with voters 1 and 20 alone.
            Assert.That(await check.Addresses.CountAsync(), Is.EqualTo(11));
            Assert.That(await check.Voters.CountAsync(), Is.EqualTo(20));
            var sizes = await check.Voters.GroupBy(v => v.AddressID).Select(g => g.Count()).ToListAsync();
            Assert.That(sizes.Count(s => s == 2), Is.EqualTo(9));
            Assert.That(sizes.Count(s => s == 1), Is.EqualTo(2));
            Assert.That(await check.Addresses.CountAsync(a => a.FullStreetAddress == "2 Street0 St"), Is.EqualTo(1));
        });
    }

    // Production applies the migrations with the EF bundle; this is the same migrator on a database that
    // is too old, which has to refuse before creating anything.
    [Test]
    public async Task Migrate_OnPostgresOlderThan18_RefusesWithAClearMessage()
    {
        Assume.That(DatabaseFixture.Provider, Is.EqualTo(DatabaseProvider.Postgres));

        await using var old = new Testcontainers.PostgreSql.PostgreSqlBuilder("postgres:17").WithDatabase("headhunter").Build();
        await old.StartAsync();
        var builder = new DbContextOptionsBuilder<HeadhunterContext>();
        builder.UseHeadhunterDatabase(DatabaseProvider.Postgres, old.GetConnectionString());
        await using var context = new HeadhunterContext(builder.Options);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => context.Database.MigrateAsync());
        Assert.That(ex!.MessageText, Does.Contain("PostgreSQL 18 or newer"));
    }
}

public class CsvImportTests
{
    private string _path = "";

    [TearDown]
    public void DeleteFile() => File.Delete(_path);

    [Test]
    public async Task Worker_ImportsACsvFileIntoRecordsAddressesAndVoters()
    {
        // 1200 voters: more than two batches of 500, so the parallel batch writes run concurrently.
        var fake = FakeVoterFile.Create(1200);
        _path = Path.Combine(Path.GetTempPath(), $"voters-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(_path, fake.Csv);

        var create = await DatabaseFixture.CreateScratchDatabaseAsync("import_csv");
        var services = new ServiceCollection();
        services.AddScoped(_ => create());
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["CSVPath"] = _path })
                .Build());
        var worker = new Worker(NullLogger<Worker>.Instance, services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());

        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!;

        await using var check = create();
        Assert.Multiple(async () =>
        {
            Assert.That(await check.MichiganVoterRecords.CountAsync(), Is.EqualTo(fake.Voters));
            Assert.That(await check.Voters.CountAsync(), Is.EqualTo(fake.Voters));
            Assert.That(await check.Addresses.CountAsync(), Is.EqualTo(fake.Households));
            Assert.That(await check.Voters.CountAsync(v => v.IS_PERM_AV_BALLOT_VOTER), Is.EqualTo(fake.PermAvBallotVoters));

            // Every household's voters share one address, and the address sizes match the file's.
            var expectedSizes = fake.Records.GroupBy(r => (FakeVoterFile.FullStreetAddress(r).ToUpperInvariant(), r.CITY)).Select(g => g.Count()).Order().ToList();
            var actualSizes = (await check.Addresses.Select(a => a.Voters.Count).ToListAsync()).Order().ToList();
            Assert.That(actualSizes, Is.EqualTo(expectedSizes));

            // One plain house, and one that has an apartment, so the street address is joined from every part.
            foreach (var expected in new[] { fake.Records[7], fake.Records.First(r => r.EXTENSION.Length > 0) })
            {
            var voter = await check.Voters.Include(v => v.Address).SingleAsync(v => v.VOTER_IDENTIFICATION_NUMBER == expected.VOTER_IDENTIFICATION_NUMBER);
            Assert.That(voter.LAST_NAME, Is.EqualTo(expected.LAST_NAME));
            Assert.That(voter.REGISTRATION_DATE, Is.EqualTo(expected.REGISTRATION_DATE));
            Assert.That(voter.Address.FullStreetAddress, Is.EqualTo(FakeVoterFile.FullStreetAddress(expected)).IgnoreCase);
            Assert.That(voter.Address.CITY, Is.EqualTo(expected.CITY));
                Assert.That(voter.IS_PERM_AV_APP_VOTER, Is.EqualTo(expected.IS_PERM_AV_APP_VOTER));
            }
        });
    }
}
