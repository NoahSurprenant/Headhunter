using Headhunter.CSVDump;
using Headhunter.Database;
using Microsoft.EntityFrameworkCore;
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
            // Voters 1..20 form households n/2 = 0..10.
            Assert.That(await check.Addresses.CountAsync(), Is.EqualTo(11));
            Assert.That(await check.Voters.CountAsync(), Is.EqualTo(20));
            var sizes = await check.Voters.GroupBy(v => v.AddressID).Select(g => g.Count()).ToListAsync();
            Assert.That(sizes.Count(s => s == 2), Is.EqualTo(10));
            Assert.That(sizes.Count(s => s == 1), Is.EqualTo(1));
            Assert.That(await check.Addresses.CountAsync(a => a.FullStreetAddress == "2 Street0"), Is.EqualTo(1));
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

    // Header and rows built from Raw's own column names, so the file always matches what the import reads.
    private static string BuildCsv(int voters)
    {
        var columns = typeof(Raw).GetProperties().Select(p => p.Name).ToList();
        var lines = new List<string> { string.Join(",", columns.Select(c => $"\"{c}\"")) };
        for (var n = 1; n <= voters; n++)
        {
            var household = n / 2;
            // Odd voters spell their street in capitals: still the household of the voter before them.
            var street = n % 2 == 1 ? $"STREET{household / 10}" : $"Street{household / 10}";
            var values = new Dictionary<string, string>
            {
                ["LAST_NAME"] = $"Last{n}",
                ["FIRST_NAME"] = $"First{n}",
                ["YEAR_OF_BIRTH"] = (1950 + n).ToString(),
                ["REGISTRATION_DATE"] = n % 2 == 1 ? "05/12/2019" : "2019-05-12",
                ["STREET_NUMBER"] = (household % 10 + 1).ToString(),
                ["STREET_NAME"] = street,
                ["STREET_TYPE"] = "St",
                ["CITY"] = "Testville",
                ["STATE"] = "MI",
                ["ZIP_CODE"] = "48000",
                ["VOTER_IDENTIFICATION_NUMBER"] = n.ToString(),
                ["IS_PERM_AV_BALLOT_VOTER"] = n % 3 == 0 ? "Y" : "N",
                ["IS_PERM_AV_APP_VOTER"] = "N",
            };
            lines.Add(string.Join(",", columns.Select(c => $"\"{values.GetValueOrDefault(c, "")}\"")));
        }
        return string.Join("\r\n", lines) + "\r\n";
    }

    [Test]
    public async Task Worker_ImportsACsvFileIntoRecordsAddressesAndVoters()
    {
        // 1200 voters: more than two batches of 500, so the parallel batch writes run concurrently.
        const int voters = 1200;
        _path = Path.Combine(Path.GetTempPath(), $"voters-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(_path, BuildCsv(voters));

        var create = await DatabaseFixture.CreateScratchDatabaseAsync("import_csv");
        var services = new ServiceCollection();
        services.AddScoped(_ => create());
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["CSVPath"] = _path })
                .Build());
        var worker = new Worker(NullLogger<Worker>.Instance, services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());

        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!;

        await using var check = create();
        Assert.Multiple(async () =>
        {
            Assert.That(await check.MichiganVoterRecords.CountAsync(), Is.EqualTo(voters));
            Assert.That(await check.Voters.CountAsync(), Is.EqualTo(voters));
            // Households n/2 = 0..600.
            Assert.That(await check.Addresses.CountAsync(), Is.EqualTo(voters / 2 + 1));
            Assert.That(await check.Voters.CountAsync(v => v.IS_PERM_AV_BALLOT_VOTER), Is.EqualTo(voters / 3));
            var voter = await check.Voters.Include(v => v.Address).SingleAsync(v => v.VOTER_IDENTIFICATION_NUMBER == "7");
            Assert.That(voter.REGISTRATION_DATE, Is.EqualTo(new DateTime(2019, 5, 12)));
            Assert.That(voter.Address.FullStreetAddress, Is.EqualTo("4 Street0").IgnoreCase);
        });
    }
}
