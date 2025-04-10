using CensusGeocoder;
using Headhunter.Database;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;

namespace Headhunter.Geocoding;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var progress = AnsiConsole.Progress();
        progress.Columns(
        [
            new TaskDescriptionColumn(),    // Task description
            new ProgressBarColumn(),        // Progress bar
            new PercentageColumn(),         // Percentage
            new ElapsedTimeColumn(),
            new SpinnerColumn(),            // Spinner
        ]);

        await progress.StartAsync(async ctx =>
        {
            var batchSize = 10000;

            using var scope = _scopeFactory.CreateScope();
            var DB = scope.ServiceProvider.GetRequiredService<HeadhunterContext>();

            // we need to do key based pagination, skip and take does not cut it
            var totalRecords = await DB.Addresses
            //.Where(x => x.Matched == null)
            .CountAsync(ct);

            var task = ctx.AddTask("Geolocating", new()
            {
                AutoStart = true,
                MaxValue = totalRecords,
            });

            var ranges = Enumerable.Range(0, (totalRecords + batchSize - 1) / batchSize)
                .Select(batch => new { Skip = batch * batchSize, Take = batchSize });


            foreach (var range in ranges)
            {
                using var innerScope = _scopeFactory.CreateScope();
                var db = innerScope.ServiceProvider.GetRequiredService<HeadhunterContext>();
                var client = innerScope.ServiceProvider.GetRequiredService<GeocodingService>();

                var voterRecords = await db.Addresses
                    .OrderBy(x => x.ID)
                    .Skip(range.Skip)
                    .Take(range.Take)
                    .ToListAsync(ct);

                var request = voterRecords.Where(x => x.Matched == null).Select(x =>
                {
                    var list = new List<string>();
                    if (x.STREET_NUMBER_PREFIX != string.Empty)
                        list.Add(x.STREET_NUMBER_PREFIX);

                    if (x.STREET_NUMBER != string.Empty)
                        list.Add(x.STREET_NUMBER);

                    if (x.STREET_NUMBER_SUFFIX != string.Empty)
                        list.Add(x.STREET_NUMBER_SUFFIX);

                    if (x.DIRECTION_PREFIX != string.Empty)
                        list.Add(x.DIRECTION_PREFIX);

                    if (x.STREET_NAME != string.Empty)
                        list.Add(x.STREET_NAME);

                    if (x.STREET_TYPE != string.Empty)
                        list.Add(x.STREET_TYPE);

                    if (x.DIRECTION_SUFFIX != string.Empty)
                        list.Add(x.DIRECTION_SUFFIX);

                    var fullAlt = string.Join(' ', list.ToArray());

                    //var street = x.STREET_NUMBER + " " + x.STREET_NAME;
                    return new BulkLine(x.ID.ToString(), fullAlt, x.CITY, x.STATE, x.ZIP_CODE);
                }).ToArray();

                if (request.Length == 0)
                {
                    task.Increment(batchSize);
                    continue;
                }

                var response = await client.BulkAddressAsync(request, ct);

                // Insert into db
                voterRecords.ForEach(address =>
                {
                    if (address.Matched is not null)
                        return;

                    var match = response.FirstOrDefault(x => x.UniqueId == address.ID.ToString());
                    if (match is null)
                    {
                        // I do not expect this to happen
                        address.Matched = false;
                        return;
                    }

                    if (match.Match == Match.Match)
                    {
                        address.Latitude = match.Latitude;
                        address.Longitude = match.Longitude;
                        address.Matched = true;
                    }
                    else
                    {
                        address.Matched = false;
                    }
                });

                await db.SaveChangesAsync(ct);
                task.Increment(batchSize);
            }

            task.StopTask();
        });

        AnsiConsole.MarkupLine("[green]Done![/]");
    }
}
