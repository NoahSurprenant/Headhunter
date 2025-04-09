using CsvHelper;
using CsvHelper.Configuration;
using Headhunter.Database;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using System.Security.AccessControl;

namespace Headhunter.CSVDump;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await ImportCsvData();
        Console.WriteLine("Imported CSV Data");
        await InsertAddresses(ct);
        Console.WriteLine("Inserted Addresses");
        await InsertVoters(ct);
        Console.WriteLine("Inserted Voters");

        Console.WriteLine("Done");
    }

    private async Task ImportCsvData()
    {
        using var scope = _serviceScopeFactory.CreateScope();

        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var path = configuration.GetValue<string>("CSVPath") ?? throw new Exception("CSVPath missing in config");

        using var reader = File.OpenRead(path);
        using var sr = new StreamReader(reader);

        var config = new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture);
        using var csv = new CsvReader(sr, config);

        csv.Context.RegisterClassMap<RecordMap>();


        //var count = csv.GetRecords<MichiganVoterRecord>().Count();
        var records = csv.GetRecords<MichiganVoterRecord>();
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

            context.MichiganVoterRecords.AddRange(batch);

            await context.SaveChangesAsync(ct);
            //task.Increment(batch.Count);
        });

        //    task.StopTask();
        //});
    }

    /// <summary>
    /// 3556506 rows affected, took 1:45 minutes on my machine
    /// </summary>
    private async Task InsertAddresses(CancellationToken ct = default)
    {
        var rawSql = @"
INSERT INTO Addresses (
	Id,
    STREET_NUMBER_PREFIX,
    STREET_NUMBER,
    STREET_NUMBER_SUFFIX,
    DIRECTION_PREFIX,
    STREET_NAME,
    STREET_TYPE,
    DIRECTION_SUFFIX,
    CITY,
    STATE,
    ZIP_CODE,
    COUNTY_CODE,
    COUNTY_NAME,
    JURISDICTION_CODE,
    JURISDICTION_NAME,
    PRECINCT,
    WARD,
    SCHOOL_DISTRICT_CODE,
    SCHOOL_DISTRICT_NAME,
    STATE_HOUSE_DISTRICT_CODE,
    STATE_HOUSE_DISTRICT_NAME,
    STATE_SENATE_DISTRICT_CODE,
    STATE_SENATE_DISTRICT_NAME,
    US_CONGRESS_DISTRICT_CODE,
    US_CONGRESS_DISTRICT_NAME,
    COUNTY_COMMISSIONER_DISTRICT_CODE,
    COUNTY_COMMISSIONER_DISTRICT_NAME,
    VILLAGE_DISTRICT_CODE,
    VILLAGE_DISTRICT_NAME,
    VILLAGE_PRECINCT,
    SCHOOL_PRECINCT
)
SELECT 
	NEWID() AS Id,
    STREET_NUMBER_PREFIX,
    STREET_NUMBER,
    STREET_NUMBER_SUFFIX,
    DIRECTION_PREFIX,
    STREET_NAME,
    STREET_TYPE,
    DIRECTION_SUFFIX,
    CITY,
    STATE,
    ZIP_CODE,
    COUNTY_CODE,
    COUNTY_NAME,
    JURISDICTION_CODE,
    JURISDICTION_NAME,
    PRECINCT,
    WARD,
    SCHOOL_DISTRICT_CODE,
    SCHOOL_DISTRICT_NAME,
    STATE_HOUSE_DISTRICT_CODE,
    STATE_HOUSE_DISTRICT_NAME,
    STATE_SENATE_DISTRICT_CODE,
    STATE_SENATE_DISTRICT_NAME,
    US_CONGRESS_DISTRICT_CODE,
    US_CONGRESS_DISTRICT_NAME,
    COUNTY_COMMISSIONER_DISTRICT_CODE,
    COUNTY_COMMISSIONER_DISTRICT_NAME,
    VILLAGE_DISTRICT_CODE,
    VILLAGE_DISTRICT_NAME,
    VILLAGE_PRECINCT,
    SCHOOL_PRECINCT
FROM dbo.MichiganVoterRecords
GROUP BY 
    STREET_NUMBER_PREFIX,
    STREET_NUMBER,
    STREET_NUMBER_SUFFIX,
    DIRECTION_PREFIX,
    STREET_NAME,
    STREET_TYPE,
    DIRECTION_SUFFIX,
    CITY,
    STATE,
    ZIP_CODE,
    COUNTY_CODE,
    COUNTY_NAME,
    JURISDICTION_CODE,
    JURISDICTION_NAME,
    PRECINCT,
    WARD,
    SCHOOL_DISTRICT_CODE,
    SCHOOL_DISTRICT_NAME,
    STATE_HOUSE_DISTRICT_CODE,
    STATE_HOUSE_DISTRICT_NAME,
    STATE_SENATE_DISTRICT_CODE,
    STATE_SENATE_DISTRICT_NAME,
    US_CONGRESS_DISTRICT_CODE,
    US_CONGRESS_DISTRICT_NAME,
    COUNTY_COMMISSIONER_DISTRICT_CODE,
    COUNTY_COMMISSIONER_DISTRICT_NAME,
    VILLAGE_DISTRICT_CODE,
    VILLAGE_DISTRICT_NAME,
    VILLAGE_PRECINCT,
    SCHOOL_PRECINCT;
";
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HeadhunterContext>();
        context.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
        await context.Database.ExecuteSqlRawAsync(rawSql, ct);
    }

    /// <summary>
    /// 8506687 rows affected, took 3:31 minutes on my machine
    /// </summary>
    private async Task InsertVoters(CancellationToken ct = default)
    {
        var rawSql = @"
INSERT INTO Voters (
    ID,
    LAST_NAME,
    FIRST_NAME,
    MIDDLE_NAME,
    NAME_SUFFIX,
    YEAR_OF_BIRTH,
    GENDER,
    REGISTRATION_DATE,
    EXTENSION,
    MAILING_ADDRESS_LINE_ONE,
    MAILING_ADDRESS_LINE_TWO,
    MAILING_ADDRESS_LINE_THREE,
    MAILING_ADDRESS_LINE_FOUR,
    MAILING_ADDRESS_LINE_FIVE,
    VOTER_IDENTIFICATION_NUMBER,
    IS_PERM_AV_BALLOT_VOTER,
    VOTER_STATUS_TYPE_CODE,
    UOCAVA_STATUS_CODE,
    UOCAVA_STATUS_NAME,
    IS_PERM_AV_APP_VOTER,
    AddressID
)
SELECT 
    MR.ID,
    LAST_NAME,
    FIRST_NAME,
    MIDDLE_NAME,
    NAME_SUFFIX,
    YEAR_OF_BIRTH,
    GENDER,
    REGISTRATION_DATE,
    EXTENSION,
    MAILING_ADDRESS_LINE_ONE,
    MAILING_ADDRESS_LINE_TWO,
    MAILING_ADDRESS_LINE_THREE,
    MAILING_ADDRESS_LINE_FOUR,
    MAILING_ADDRESS_LINE_FIVE,
    VOTER_IDENTIFICATION_NUMBER,
    IS_PERM_AV_BALLOT_VOTER,
    VOTER_STATUS_TYPE_CODE,
    UOCAVA_STATUS_CODE,
    UOCAVA_STATUS_NAME,
    IS_PERM_AV_APP_VOTER,
    AL.ID
FROM MichiganVoterRecords AS MR
LEFT JOIN Addresses AS AL
ON AL.STREET_NUMBER_PREFIX = MR.STREET_NUMBER_PREFIX AND
   AL.STREET_NUMBER = MR.STREET_NUMBER AND
   AL.STREET_NUMBER_SUFFIX = MR.STREET_NUMBER_SUFFIX AND
   AL.DIRECTION_PREFIX = MR.DIRECTION_PREFIX AND
   AL.STREET_NAME = MR.STREET_NAME AND
   AL.STREET_TYPE = MR.STREET_TYPE AND
   AL.DIRECTION_SUFFIX = MR.DIRECTION_SUFFIX AND
   AL.CITY = MR.CITY AND
   AL.STATE = MR.STATE AND
   AL.ZIP_CODE = MR.ZIP_CODE AND
   AL.COUNTY_CODE = MR.COUNTY_CODE AND
   AL.COUNTY_NAME = MR.COUNTY_NAME AND
   AL.JURISDICTION_CODE = MR.JURISDICTION_CODE AND
   AL.JURISDICTION_NAME = MR.JURISDICTION_NAME AND
   AL.PRECINCT = MR.PRECINCT AND
   AL.WARD = MR.WARD AND
   AL.SCHOOL_DISTRICT_CODE = MR.SCHOOL_DISTRICT_CODE AND
   AL.SCHOOL_DISTRICT_NAME = MR.SCHOOL_DISTRICT_NAME AND
   AL.STATE_HOUSE_DISTRICT_CODE = MR.STATE_HOUSE_DISTRICT_CODE AND
   AL.STATE_HOUSE_DISTRICT_NAME = MR.STATE_HOUSE_DISTRICT_NAME AND
   AL.STATE_SENATE_DISTRICT_CODE = MR.STATE_SENATE_DISTRICT_CODE AND
   AL.STATE_SENATE_DISTRICT_NAME = MR.STATE_SENATE_DISTRICT_NAME AND
   AL.US_CONGRESS_DISTRICT_CODE = MR.US_CONGRESS_DISTRICT_CODE AND
   AL.US_CONGRESS_DISTRICT_NAME = MR.US_CONGRESS_DISTRICT_NAME AND
   AL.COUNTY_COMMISSIONER_DISTRICT_CODE = MR.COUNTY_COMMISSIONER_DISTRICT_CODE AND
   AL.COUNTY_COMMISSIONER_DISTRICT_NAME = MR.COUNTY_COMMISSIONER_DISTRICT_NAME AND
   AL.VILLAGE_DISTRICT_CODE = MR.VILLAGE_DISTRICT_CODE AND
   AL.VILLAGE_DISTRICT_NAME = MR.VILLAGE_DISTRICT_NAME AND
   AL.VILLAGE_PRECINCT = MR.VILLAGE_PRECINCT AND
   AL.SCHOOL_PRECINCT = MR.SCHOOL_PRECINCT;
";
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HeadhunterContext>();
        context.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
        await context.Database.ExecuteSqlRawAsync(rawSql, ct);
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
