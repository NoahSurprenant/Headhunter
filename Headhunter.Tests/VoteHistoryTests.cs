using Headhunter.CSVDump;
using Headhunter.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Headhunter.Tests;

public class VoteHistoryTests
{
    private readonly List<string> _cleanup = [];

    [TearDown]
    public void Delete()
    {
        foreach (var path in _cleanup)
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else File.Delete(path);
        }
    }

    private string Temp(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{name}-{Guid.NewGuid():N}");
        _cleanup.Add(path);
        return path;
    }

    private static async Task RunWorker(Func<HeadhunterContext> create, Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => create());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        var worker = new Worker(NullLogger<Worker>.Instance, services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!;
    }

    private async Task<(Func<HeadhunterContext> Create, FakeVoterFile Voters)> RegisteredVotersAsync(string database)
    {
        var voters = FakeVoterFile.Create(1200);
        var csv = Temp("voters") + ".csv";
        await File.WriteAllTextAsync(csv, voters.Csv);
        var create = await DatabaseFixture.CreateScratchDatabaseAsync(database);
        await RunWorker(create, new() { ["CSVPath"] = csv });
        return (create, voters);
    }

    [Test]
    public async Task Worker_ImportsHistoryForRegisteredVoters_WithCodesAndNamesInLookupTables()
    {
        var (create, voters) = await RegisteredVotersAsync("history_csv");
        var history = FakeHistoryFile.Create(voters);
        var csv = Temp("history") + ".csv";
        await File.WriteAllTextAsync(csv, history.Csv);
        var codes = Temp("codes");
        FakeHistoryFile.WriteCodeFiles(voters, codes);

        await RunWorker(create, new() { ["Import"] = "History", ["CSVPath"] = csv, ["CodesPath"] = codes });

        await using var check = create();
        Assert.Multiple(async () =>
        {
            // Only registered voters, once for each election: the 25 unregistered voters and 5 repeated rows are skipped.
            Assert.That(history.UnregisteredRows, Is.EqualTo(25));
            Assert.That(await check.VoteHistory.CountAsync(), Is.EqualTo(history.ExpectedRows));

            // One voter's history: the dates only. The file's county, jurisdiction and school district are the voter's
            // current ones on every row, so they are not imported.
            var voter = history.Votes[3].Voter;
            var expected = history.Votes.Where(v => v.Voter == voter)
                .Select(v => (v.Date, (string?)null, (string?)null, (string?)null)).Order().ToList();
            var actual = (await check.VoteHistory.Where(h => h.Voter.VOTER_IDENTIFICATION_NUMBER == voter.VOTER_IDENTIFICATION_NUMBER)
                    .Select(h => new { h.ElectionDate, h.CountyCode, h.JurisdictionCode, h.SchoolDistrictCode }).ToListAsync())
                .Select(h => (h.ElectionDate, h.CountyCode, h.JurisdictionCode, h.SchoolDistrictCode)).Order().ToList();
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(expected, Is.Not.Empty);

            // The names are in the lookup tables once, not on the rows.
            var places = voters.Records.Select(r => (r.COUNTY_CODE, r.COUNTY_NAME)).Distinct().ToList();
            Assert.That(await check.Counties.CountAsync(), Is.EqualTo(places.Count));
            Assert.That((await check.Counties.ToListAsync()).Select(c => (c.Code, c.Name)), Is.EquivalentTo(places));
            var jurisdictions = voters.Records.Select(r => (r.COUNTY_CODE, r.JURISDICTION_CODE, r.JURISDICTION_NAME)).Distinct().ToList();
            Assert.That((await check.Jurisdictions.ToListAsync()).Select(j => (j.CountyCode, j.Code, j.Name)), Is.EquivalentTo(jurisdictions));
            var schools = voters.Records.Select(r => (r.SCHOOL_DISTRICT_CODE, r.SCHOOL_DISTRICT_NAME)).Distinct().ToList();
            Assert.That((await check.SchoolDistricts.ToListAsync()).Select(s => (s.Code, s.Name)), Is.EquivalentTo(schools));

            // An election has its description (the code file has none for the oldest election).
            var elections = await check.Elections.OrderBy(e => e.Date).ToListAsync();
            Assert.That(elections.Select(e => e.Date), Is.EqualTo(FakeHistoryFile.Elections.Select(e => e.Date)));
            Assert.That(elections[0].Description, Is.Empty);
            Assert.That(elections[0].CodeId, Is.Null);
            Assert.That(elections[1].Description, Is.EqualTo("PRESIDENTIAL GENERAL"));
            Assert.That(elections[1].CodeId, Is.EqualTo(672));
        });

        // Loading the same file again changes nothing, and the staging table is gone.
        await RunWorker(create, new() { ["Import"] = "History", ["CSVPath"] = csv });
        await using var again = create();
        Assert.That(await again.VoteHistory.CountAsync(), Is.EqualTo(history.ExpectedRows));
        var staging = await again.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM information_schema.tables WHERE table_name = 'VoteHistoryStaging'").ToListAsync();
        Assert.That(staging.Single(), Is.Zero);
    }

    [Test]
    public async Task Worker_ImportsHistoryFromTheStatesZipFile_AndTheCodesFromItsInformationalZip()
    {
        var (create, voters) = await RegisteredVotersAsync("history_zip");
        var history = FakeHistoryFile.Create(voters, seed: 2);
        var zip = Temp("history") + ".zip";
        FakeHistoryFile.WriteZip(zip, "History Part 1.csv", history.Csv);
        var codes = Temp("codes");
        FakeHistoryFile.WriteCodeFiles(voters, codes);
        var codesZip = Temp("info") + ".zip";
        System.IO.Compression.ZipFile.CreateFromDirectory(codes, codesZip);

        await RunWorker(create, new() { ["Import"] = "History", ["CSVPath"] = zip, ["CodesPath"] = codesZip });

        await using var check = create();
        Assert.That(await check.VoteHistory.CountAsync(), Is.EqualTo(history.ExpectedRows));
        Assert.That(await check.Elections.CountAsync(e => e.CodeId != null), Is.EqualTo(FakeHistoryFile.Elections.Length - 1));
    }

    private string CodesWith(params (string File, string Content)[] files)
    {
        var dir = Temp("codes");
        Directory.CreateDirectory(dir);
        foreach (var (file, content) in files) File.WriteAllText(Path.Combine(dir, file), content);
        return dir;
    }

    [Test]
    public async Task CodeFiles_TwoElectionCodeFiles_UseTheNewest_AndSameDateElectionsMerge()
    {
        var create = await DatabaseFixture.CreateScratchDatabaseAsync($"codes_{Guid.NewGuid():N}");
        var dir = CodesWith(
            ("Election Codes through 2023.csv", "ID,DATE,DESC\r\n5,11/8/2022,OLD ONLY\r\n"),
            ("Election Codes through 2025.csv", "ID,DATE,DESC\r\n9,11/5/2024,SECOND\r\n7,11/5/2024,FIRST\r\n8,3/4/2025,OTHER\r\n"));

        await using (var db = create()) await CodeFiles.LoadAsync(db, dir);

        await using var check = create();
        var elections = (await check.Elections.OrderBy(e => e.Date).ToListAsync()).Select(e => (e.Date, e.CodeId, e.Description)).ToList();
        Assert.That(elections, Is.EqualTo(new (DateOnly, int?, string)[]
        {
            (new DateOnly(2024, 11, 5), 7, "FIRST / SECOND"),
            (new DateOnly(2025, 3, 4), 8, "OTHER"),
        }));
    }

    [Test]
    public async Task CodeFiles_TwoCountyCodeFiles_Throw()
    {
        var create = await DatabaseFixture.CreateScratchDatabaseAsync($"codes_{Guid.NewGuid():N}");
        var dir = CodesWith(("County Codes.csv", "C,N\r\n01,A\r\n"), ("County Codes copy.csv", "C,N\r\n01,B\r\n"));
        await using var db = create();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CodeFiles.LoadAsync(db, dir));
        Assert.That(error!.Message, Does.Contain("County Codes"));
    }

    [Test]
    public async Task Worker_FillsEmptyLookupNames_WithoutTouchingNamedOnes()
    {
        var voters = FakeVoterFile.Create(300);
        var csv = Temp("voters") + ".csv";
        await File.WriteAllTextAsync(csv, voters.Csv);
        var create = await DatabaseFixture.CreateScratchDatabaseAsync($"names_{Guid.NewGuid():N}");
        var first = voters.Records[0];
        var other = voters.Records.First(r => r.COUNTY_CODE != first.COUNTY_CODE);
        await using (var db = create())
        {
            db.Counties.AddRange(new County { Code = first.COUNTY_CODE, Name = "" }, new County { Code = other.COUNTY_CODE, Name = "KEEP ME" });
            await db.SaveChangesAsync();
            db.Jurisdictions.Add(new Jurisdiction { CountyCode = first.COUNTY_CODE, Code = first.JURISDICTION_CODE, Name = "" });
            db.SchoolDistricts.Add(new SchoolDistrict { Code = first.SCHOOL_DISTRICT_CODE, Name = "" });
            await db.SaveChangesAsync();
        }

        await RunWorker(create, new() { ["CSVPath"] = csv });

        await using var check = create();
        Assert.Multiple(async () =>
        {
            Assert.That((await check.Counties.SingleAsync(c => c.Code == first.COUNTY_CODE)).Name, Is.EqualTo(first.COUNTY_NAME));
            Assert.That((await check.Counties.SingleAsync(c => c.Code == other.COUNTY_CODE)).Name, Is.EqualTo("KEEP ME"));
            Assert.That((await check.Jurisdictions.SingleAsync(j => j.CountyCode == first.COUNTY_CODE && j.Code == first.JURISDICTION_CODE)).Name, Is.EqualTo(first.JURISDICTION_NAME));
            Assert.That((await check.SchoolDistricts.SingleAsync(s => s.Code == first.SCHOOL_DISTRICT_CODE)).Name, Is.EqualTo(first.SCHOOL_DISTRICT_NAME));
        });
    }

    [Test]
    public async Task Lookups_WithoutOverwrite_FillEmptyNamesOnly()
    {
        var create = await DatabaseFixture.CreateScratchDatabaseAsync($"lookups_{Guid.NewGuid():N}");
        await using var db = create();
        db.Counties.AddRange(new County { Code = "01", Name = "" }, new County { Code = "02", Name = "STAYS" });
        await db.SaveChangesAsync();

        await Lookups.UpsertAsync(db, [("01", "FILLED"), ("02", "CHANGED")], [], [], [], overwrite: false);

        await using var check = create();
        Assert.That((await check.Counties.OrderBy(c => c.Code).ToListAsync()).Select(c => c.Name), Is.EqualTo(new[] { "FILLED", "STAYS" }));
    }
}
