using CsvHelper;
using CsvHelper.Configuration;
using Headhunter.Database;
using Spectre.Console;

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


        Console.WriteLine("Done");
    }

    //public IEnumerable<List<T>> GetBatches<T>(IEnumerable<T> source, int batchSize)
    //{
    //    var batch = new List<T>();

    //    foreach (var item in source)
    //    {
    //        batch.Add(item);

    //        if (batch.Count == batchSize)
    //        {
    //            yield return batch;
    //            batch.Clear();
    //        }
    //    }

    //    if (batch.Count > 0)
    //    {
    //        yield return batch;
    //    }
    //}

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
