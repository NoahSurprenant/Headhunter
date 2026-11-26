using CsvHelper;
using Headhunter.Database;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using System.Security.AccessControl;

namespace Headhunter.CSVDump;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory serviceScopeFactory, IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _lifetime = lifetime;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await Import(ct);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            Environment.ExitCode = 1;
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    private async Task Import(CancellationToken ct)
    {
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            if (scope.ServiceProvider.GetRequiredService<IConfiguration>()["Import"]?.Equals("History", StringComparison.OrdinalIgnoreCase) == true)
            {
                await ImportVoteHistory(scope.ServiceProvider, ct);
                Console.WriteLine("Done");
                return;
            }
        }

        await ImportCsvData();
        Console.WriteLine("Imported CSV Data");
        await InsertAddresses(ct);
        Console.WriteLine("Inserted Addresses");
        await InsertVoters(ct);
        Console.WriteLine("Inserted Voters");

        Console.WriteLine("Done");
    }

    /// <summary>Import=History: CSVPath is the history file (the CSV or its zip), CodesPath the state's code files (optional).</summary>
    private async Task ImportVoteHistory(IServiceProvider services, CancellationToken ct)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var path = configuration.GetValue<string>("CSVPath") ?? throw new Exception("CSVPath missing in config");
        var context = services.GetRequiredService<HeadhunterContext>();

        if (configuration.GetValue<string>("CodesPath") is { Length: > 0 } codes)
        {
            await CodeFiles.LoadAsync(context, codes, ct);
            Console.WriteLine("Loaded code tables");
        }

        var result = await new VoteHistoryImporter(context).ImportAsync(path, ct);
        Console.WriteLine($"Read {result.Rows} history rows, inserted {result.Inserted}");
    }

    private async Task ImportCsvData()
    {
        using var scope = _serviceScopeFactory.CreateScope();

        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var path = configuration.GetValue<string>("CSVPath") ?? throw new Exception("CSVPath missing in config");



        //var count = csv.GetRecords<MichiganVoterRecord>().Count();
        var records = VoterFile.Records(path);
        var batches = GetBatches(records, 500);

        //var progress = AnsiConsole.Progress();

        //await progress.StartAsync(async ctx =>
        //{
        //    var task = ctx.AddTask("[green]Saving...[/]", new()
        //    {
        //        AutoStart = true,
        //        MaxValue = count,
        //    });

        await Parallel.ForEachAsync(batches, async (batch, ct) =>
        {
            using var innerScope = _serviceScopeFactory.CreateScope();
            var context = innerScope.ServiceProvider.GetRequiredService<HeadhunterContext>();

            var mapped = batch.Select(x =>
            {
                return new MichiganVoterRecord()
                {
                    LAST_NAME = x.LAST_NAME,
                    FIRST_NAME = x.FIRST_NAME,
                    MIDDLE_NAME = x.MIDDLE_NAME,
                    NAME_SUFFIX = x.NAME_SUFFIX,
                    YEAR_OF_BIRTH = x.YEAR_OF_BIRTH,
                    GENDER = x.GENDER,
                    REGISTRATION_DATE = x.REGISTRATION_DATE,
                    STREET_NUMBER_PREFIX = x.STREET_NUMBER_PREFIX,
                    STREET_NUMBER = x.STREET_NUMBER,
                    STREET_NUMBER_SUFFIX = x.STREET_NUMBER_SUFFIX,
                    DIRECTION_PREFIX = x.DIRECTION_PREFIX,
                    STREET_NAME = x.STREET_NAME,
                    STREET_TYPE = x.STREET_TYPE,
                    DIRECTION_SUFFIX = x.DIRECTION_SUFFIX,
                    EXTENSION = x.EXTENSION,
                    CITY = x.CITY,
                    STATE = x.STATE,
                    ZIP_CODE = x.ZIP_CODE,
                    MAILING_ADDRESS_LINE_ONE = x.MAILING_ADDRESS_LINE_ONE,
                    MAILING_ADDRESS_LINE_TWO = x.MAILING_ADDRESS_LINE_TWO,
                    MAILING_ADDRESS_LINE_THREE = x.MAILING_ADDRESS_LINE_THREE,
                    MAILING_ADDRESS_LINE_FOUR = x.MAILING_ADDRESS_LINE_FOUR,
                    MAILING_ADDRESS_LINE_FIVE = x.MAILING_ADDRESS_LINE_FIVE,
                    VOTER_IDENTIFICATION_NUMBER = x.VOTER_IDENTIFICATION_NUMBER,
                    COUNTY_CODE = x.COUNTY_CODE,
                    COUNTY_NAME = x.COUNTY_NAME,
                    JURISDICTION_CODE = x.JURISDICTION_CODE,
                    JURISDICTION_NAME = x.JURISDICTION_NAME,
                    PRECINCT = x.PRECINCT,
                    WARD = x.WARD,
                    SCHOOL_DISTRICT_CODE = x.SCHOOL_DISTRICT_CODE,
                    SCHOOL_DISTRICT_NAME = x.SCHOOL_DISTRICT_NAME,
                    STATE_HOUSE_DISTRICT_CODE = x.STATE_HOUSE_DISTRICT_CODE,
                    STATE_HOUSE_DISTRICT_NAME = x.STATE_HOUSE_DISTRICT_NAME,
                    STATE_SENATE_DISTRICT_CODE = x.STATE_SENATE_DISTRICT_CODE,
                    STATE_SENATE_DISTRICT_NAME = x.STATE_SENATE_DISTRICT_NAME,
                    US_CONGRESS_DISTRICT_CODE = x.US_CONGRESS_DISTRICT_CODE,
                    US_CONGRESS_DISTRICT_NAME = x.US_CONGRESS_DISTRICT_NAME,
                    COUNTY_COMMISSIONER_DISTRICT_CODE = x.COUNTY_COMMISSIONER_DISTRICT_CODE,
                    COUNTY_COMMISSIONER_DISTRICT_NAME = x.COUNTY_COMMISSIONER_DISTRICT_NAME,
                    VILLAGE_DISTRICT_CODE = x.VILLAGE_DISTRICT_CODE,
                    VILLAGE_DISTRICT_NAME = x.VILLAGE_DISTRICT_NAME,
                    VILLAGE_PRECINCT = x.VILLAGE_PRECINCT,
                    SCHOOL_PRECINCT = x.SCHOOL_PRECINCT,
                    IS_PERM_AV_BALLOT_VOTER = x.IS_PERM_AV_BALLOT_VOTER,
                    VOTER_STATUS_TYPE_CODE = x.VOTER_STATUS_TYPE_CODE,
                    UOCAVA_STATUS_CODE = x.UOCAVA_STATUS_CODE,
                    UOCAVA_STATUS_NAME = x.UOCAVA_STATUS_NAME,
                    IS_PERM_AV_APP_VOTER = x.IS_PERM_AV_APP_VOTER,
                };
            });

            context.MichiganVoterRecords.AddRange(mapped);

            await context.SaveChangesAsync(ct);
            //task.Increment(batch.Count);
        });

        //    task.StopTask();
        //});
    }

    /// <summary>
    /// 3556506 rows affected, took 1:45 minutes on my machine
    /// </summary>
    public async Task InsertAddresses(CancellationToken ct = default)
    {
        var rawSql = """
INSERT INTO "Addresses" (
	"ID",
    "STREET_NUMBER_PREFIX",
    "STREET_NUMBER",
    "STREET_NUMBER_SUFFIX",
    "DIRECTION_PREFIX",
    "STREET_NAME",
    "STREET_TYPE",
    "DIRECTION_SUFFIX",
    "EXTENSION",
    "FullStreetAddress",
    "CITY",
    "STATE",
    "ZIP_CODE",
    "COUNTY_CODE",
    "JURISDICTION_CODE",
    "PRECINCT",
    "WARD",
    "SCHOOL_DISTRICT_CODE",
    "STATE_HOUSE_DISTRICT_CODE",
    "STATE_SENATE_DISTRICT_CODE",
    "US_CONGRESS_DISTRICT_CODE",
    "COUNTY_COMMISSIONER_DISTRICT_CODE",
    "VILLAGE_DISTRICT_CODE",
    "VILLAGE_DISTRICT_NAME",
    "VILLAGE_PRECINCT",
    "SCHOOL_PRECINCT"
)
SELECT 
	NEWID() AS "ID",
    "STREET_NUMBER_PREFIX",
    "STREET_NUMBER",
    "STREET_NUMBER_SUFFIX",
    "DIRECTION_PREFIX",
    "STREET_NAME",
    "STREET_TYPE",
    "DIRECTION_SUFFIX",
    "EXTENSION",
    -- Ensuring no extra spaces and handling empty strings properly
    LTRIM(RTRIM(
        CONCAT_WS(' ', 
            NULLIF("STREET_NUMBER_PREFIX", ''), 
            NULLIF("STREET_NUMBER", ''), 
            NULLIF("STREET_NUMBER_SUFFIX", ''), 
            NULLIF("DIRECTION_PREFIX", ''), 
            NULLIF("STREET_NAME", ''), 
            NULLIF("STREET_TYPE", ''), 
            NULLIF("DIRECTION_SUFFIX", ''), 
            NULLIF("EXTENSION", '')
        )
    )) AS "FullStreetAddress",
    "CITY",
    "STATE",
    "ZIP_CODE",
    NULLIF("COUNTY_CODE", '') AS "COUNTY_CODE",
    NULLIF("JURISDICTION_CODE", '') AS "JURISDICTION_CODE",
    "PRECINCT",
    "WARD",
    NULLIF("SCHOOL_DISTRICT_CODE", '') AS "SCHOOL_DISTRICT_CODE",
    NULLIF("STATE_HOUSE_DISTRICT_CODE", '') AS "STATE_HOUSE_DISTRICT_CODE",
    NULLIF("STATE_SENATE_DISTRICT_CODE", '') AS "STATE_SENATE_DISTRICT_CODE",
    NULLIF("US_CONGRESS_DISTRICT_CODE", '') AS "US_CONGRESS_DISTRICT_CODE",
    NULLIF("COUNTY_COMMISSIONER_DISTRICT_CODE", '') AS "COUNTY_COMMISSIONER_DISTRICT_CODE",
    "VILLAGE_DISTRICT_CODE",
    "VILLAGE_DISTRICT_NAME",
    "VILLAGE_PRECINCT",
    "SCHOOL_PRECINCT"
FROM "MichiganVoterRecords"
GROUP BY 
    "STREET_NUMBER_PREFIX",
    "STREET_NUMBER",
    "STREET_NUMBER_SUFFIX",
    "DIRECTION_PREFIX",
    "STREET_NAME",
    "STREET_TYPE",
    "DIRECTION_SUFFIX",
    "EXTENSION",
    "CITY",
    "STATE",
    "ZIP_CODE",
    "COUNTY_CODE",
    "JURISDICTION_CODE",
    "PRECINCT",
    "WARD",
    "SCHOOL_DISTRICT_CODE",
    "STATE_HOUSE_DISTRICT_CODE",
    "STATE_SENATE_DISTRICT_CODE",
    "US_CONGRESS_DISTRICT_CODE",
    "COUNTY_COMMISSIONER_DISTRICT_CODE",
    "VILLAGE_DISTRICT_CODE",
    "VILLAGE_DISTRICT_NAME",
    "VILLAGE_PRECINCT",
    "SCHOOL_PRECINCT";
""";
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HeadhunterContext>();
        await InsertLookups(context, ct);
        context.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
        await context.Database.ExecuteSqlRawAsync(
            rawSql.Replace("NEWID()", context.Database.IsNpgsql() ? "gen_random_uuid()" : "NEWID()"), ct);
    }

    /// <summary>
    /// 8506687 rows affected, took 3:31 minutes on my machine
    /// </summary>
    public async Task InsertVoters(CancellationToken ct = default)
    {
        var rawSql = """
INSERT INTO "Voters" (
    "ID",
    "LAST_NAME",
    "FIRST_NAME",
    "MIDDLE_NAME",
    "NAME_SUFFIX",
    "YEAR_OF_BIRTH",
    "GENDER",
    "REGISTRATION_DATE",
    "MAILING_ADDRESS_LINE_ONE",
    "MAILING_ADDRESS_LINE_TWO",
    "MAILING_ADDRESS_LINE_THREE",
    "MAILING_ADDRESS_LINE_FOUR",
    "MAILING_ADDRESS_LINE_FIVE",
    "VOTER_IDENTIFICATION_NUMBER",
    "IS_PERM_AV_BALLOT_VOTER",
    "VOTER_STATUS_TYPE_CODE",
    "UOCAVA_STATUS_CODE",
    "UOCAVA_STATUS_NAME",
    "IS_PERM_AV_APP_VOTER",
    "AddressID"
)
SELECT 
    MR."ID",
    "LAST_NAME",
    "FIRST_NAME",
    "MIDDLE_NAME",
    "NAME_SUFFIX",
    "YEAR_OF_BIRTH",
    "GENDER",
    "REGISTRATION_DATE",
    "MAILING_ADDRESS_LINE_ONE",
    "MAILING_ADDRESS_LINE_TWO",
    "MAILING_ADDRESS_LINE_THREE",
    "MAILING_ADDRESS_LINE_FOUR",
    "MAILING_ADDRESS_LINE_FIVE",
    "VOTER_IDENTIFICATION_NUMBER",
    "IS_PERM_AV_BALLOT_VOTER",
    "VOTER_STATUS_TYPE_CODE",
    "UOCAVA_STATUS_CODE",
    "UOCAVA_STATUS_NAME",
    "IS_PERM_AV_APP_VOTER",
    AL."ID"
FROM "MichiganVoterRecords" AS MR
LEFT JOIN "Addresses" AS AL
ON AL."STREET_NUMBER_PREFIX" = MR."STREET_NUMBER_PREFIX" AND
   AL."STREET_NUMBER" = MR."STREET_NUMBER" AND
   AL."STREET_NUMBER_SUFFIX" = MR."STREET_NUMBER_SUFFIX" AND
   AL."DIRECTION_PREFIX" = MR."DIRECTION_PREFIX" AND
   AL."STREET_NAME" = MR."STREET_NAME" AND
   AL."STREET_TYPE" = MR."STREET_TYPE" AND
   AL."DIRECTION_SUFFIX" = MR."DIRECTION_SUFFIX" AND
   AL."EXTENSION" = MR."EXTENSION" AND
   AL."CITY" = MR."CITY" AND
   AL."STATE" = MR."STATE" AND
   AL."ZIP_CODE" = MR."ZIP_CODE" AND
   COALESCE(AL."COUNTY_CODE", '') = MR."COUNTY_CODE" AND
   COALESCE(AL."JURISDICTION_CODE", '') = MR."JURISDICTION_CODE" AND
   AL."PRECINCT" = MR."PRECINCT" AND
   AL."WARD" = MR."WARD" AND
   COALESCE(AL."SCHOOL_DISTRICT_CODE", '') = MR."SCHOOL_DISTRICT_CODE" AND
   COALESCE(AL."STATE_HOUSE_DISTRICT_CODE", '') = MR."STATE_HOUSE_DISTRICT_CODE" AND
   COALESCE(AL."STATE_SENATE_DISTRICT_CODE", '') = MR."STATE_SENATE_DISTRICT_CODE" AND
   COALESCE(AL."US_CONGRESS_DISTRICT_CODE", '') = MR."US_CONGRESS_DISTRICT_CODE" AND
   COALESCE(AL."COUNTY_COMMISSIONER_DISTRICT_CODE", '') = MR."COUNTY_COMMISSIONER_DISTRICT_CODE" AND
   AL."VILLAGE_DISTRICT_CODE" = MR."VILLAGE_DISTRICT_CODE" AND
   AL."VILLAGE_DISTRICT_NAME" = MR."VILLAGE_DISTRICT_NAME" AND
   AL."VILLAGE_PRECINCT" = MR."VILLAGE_PRECINCT" AND
   AL."SCHOOL_PRECINCT" = MR."SCHOOL_PRECINCT";
""";
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HeadhunterContext>();
        if (context.Database.IsNpgsql())
        {
            // Postgres cannot hash-join on the case-insensitive (nondeterministic) collation, so 31 "=" columns
            // become a nested loop (256 s for 300k voters, hopeless for 8.5M). Join on a key that compares
            // deterministically instead; lower() is how this collation folds case.
            var columns = rawSql[rawSql.IndexOf("\nON AL.", StringComparison.Ordinal)..]
                .Split("AL.\"")[1..]
                .Select(part => part[..part.IndexOf('"')])
                .ToList();
            string Key(string alias) =>
                $"md5(lower(concat_ws(chr(31), {string.Join(", ", columns.Select(c => $"coalesce({alias}.\"{c}\", '') COLLATE \"case_insensitive\""))})))";
            rawSql = rawSql[..rawSql.IndexOf("\nON AL.", StringComparison.Ordinal)] + $"\nON {Key("AL")} = {Key("MR")};";
        }
        context.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
        await context.Database.ExecuteSqlRawAsync(rawSql, ct);
    }

    /// <summary>
    /// The counties, jurisdictions, school districts and the other districts in the records, by code, for the
    /// addresses to point at. A code already in its table keeps its name (the state's code files may have loaded it).
    /// </summary>
    private static async Task InsertLookups(HeadhunterContext context, CancellationToken ct)
    {
        // Postgres compares the code columns exactly (the records' are case-insensitive).
        var c = context.Database.IsNpgsql() ? " COLLATE \"C\"" : "";

        string Lookup(string table, string code, string name) => $"""
            INSERT INTO "{table}" ("Code", "Name")
            SELECT r."{code}"{c}, MIN(r."{name}") FROM "MichiganVoterRecords" r
            WHERE r."{code}" <> '' AND NOT EXISTS (SELECT 1 FROM "{table}" x WHERE x."Code" = r."{code}"{c})
            GROUP BY r."{code}"{c}
            """;

        string InCounty(string table, string code, string name) => $"""
            INSERT INTO "{table}" ("CountyCode", "Code", "Name")
            SELECT r."COUNTY_CODE"{c}, r."{code}"{c}, MIN(r."{name}") FROM "MichiganVoterRecords" r
            WHERE r."COUNTY_CODE" <> '' AND r."{code}" <> '' AND NOT EXISTS (
                SELECT 1 FROM "{table}" x WHERE x."CountyCode" = r."COUNTY_CODE"{c} AND x."Code" = r."{code}"{c})
            GROUP BY r."COUNTY_CODE"{c}, r."{code}"{c}
            """;

        // A name that is empty (a row made from a code alone) is filled in when the records have one.
        string FillNames(string table, string code, string name, bool inCounty)
        {
            var match = $"r.\"{code}\"{c} = \"{table}\".\"Code\"" +
                (inCounty ? $" AND r.\"COUNTY_CODE\"{c} = \"{table}\".\"CountyCode\"" : "") + $" AND r.\"{name}\" <> ''";
            return $"UPDATE \"{table}\" SET \"Name\" = (SELECT MIN(r.\"{name}\") FROM \"MichiganVoterRecords\" r WHERE {match}) " +
                   $"WHERE \"Name\" = '' AND EXISTS (SELECT 1 FROM \"MichiganVoterRecords\" r WHERE {match})";
        }

        // The counties first: the others have a foreign key to them.
        string[] statements =
        [
            Lookup("Counties", "COUNTY_CODE", "COUNTY_NAME"),
            InCounty("Jurisdictions", "JURISDICTION_CODE", "JURISDICTION_NAME"),
            Lookup("SchoolDistricts", "SCHOOL_DISTRICT_CODE", "SCHOOL_DISTRICT_NAME"),
            Lookup("StateHouseDistricts", "STATE_HOUSE_DISTRICT_CODE", "STATE_HOUSE_DISTRICT_NAME"),
            Lookup("StateSenateDistricts", "STATE_SENATE_DISTRICT_CODE", "STATE_SENATE_DISTRICT_NAME"),
            Lookup("UsCongressDistricts", "US_CONGRESS_DISTRICT_CODE", "US_CONGRESS_DISTRICT_NAME"),
            InCounty("CountyCommissionerDistricts", "COUNTY_COMMISSIONER_DISTRICT_CODE", "COUNTY_COMMISSIONER_DISTRICT_NAME"),
            FillNames("Counties", "COUNTY_CODE", "COUNTY_NAME", false),
            FillNames("Jurisdictions", "JURISDICTION_CODE", "JURISDICTION_NAME", true),
            FillNames("SchoolDistricts", "SCHOOL_DISTRICT_CODE", "SCHOOL_DISTRICT_NAME", false),
            FillNames("StateHouseDistricts", "STATE_HOUSE_DISTRICT_CODE", "STATE_HOUSE_DISTRICT_NAME", false),
            FillNames("StateSenateDistricts", "STATE_SENATE_DISTRICT_CODE", "STATE_SENATE_DISTRICT_NAME", false),
            FillNames("UsCongressDistricts", "US_CONGRESS_DISTRICT_CODE", "US_CONGRESS_DISTRICT_NAME", false),
            FillNames("CountyCommissionerDistricts", "COUNTY_COMMISSIONER_DISTRICT_CODE", "COUNTY_COMMISSIONER_DISTRICT_NAME", true),
        ];
        context.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
        foreach (var statement in statements)
            await context.Database.ExecuteSqlRawAsync(statement, ct);
    }

    public IEnumerable<List<T>> GetBatches<T>(IEnumerable<T> source, int batchSize)
    {
        var batch = new List<T>();

        foreach (var item in source)
        {
            batch.Add(item);

            if (batch.Count == batchSize)
            {
                yield return new List<T>(batch); // Create a new list to avoid reference issues
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            yield return new List<T>(batch); // Create a new list for the final batch
        }
    }
}
