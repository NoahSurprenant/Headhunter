using System.Diagnostics;
using Headhunter.Database;
using Microsoft.EntityFrameworkCore;

namespace Headhunter.Tests;

/// <summary>
/// The import and the suggestion searches on a seeded data set big enough to show scans and bad join plans
/// (the real file has 8.5M voters at 3.5M addresses). Synthetic rows only. Excluded from the normal test run
/// (--filter "TestCategory!=Scale"); the "Scale" CI job runs it with HEADHUNTER_SCALE_VOTERS.
/// </summary>
[Category("Scale")]
public class ScaleTests
{
    private static int Voters =>
        int.TryParse(Environment.GetEnvironmentVariable("HEADHUNTER_SCALE_VOTERS"), out var n) ? n : 200_000;

    [Test]
    public async Task ImportAndSearch_OnSeededData_StayWithinBudget()
    {
        var create = await DatabaseFixture.CreateScratchDatabaseAsync("scale");
        var timings = new List<string>();

        async Task<T> Time<T>(string what, Func<Task<T>> action, TimeSpan budget)
        {
            var sw = Stopwatch.StartNew();
            var result = await action();
            sw.Stop();
            timings.Add($"{what}: {sw.Elapsed.TotalSeconds:F2}s (budget {budget.TotalSeconds:F0}s)");
            TestContext.Out.WriteLine(timings[^1]);
            Assert.That(sw.Elapsed, Is.LessThan(budget), $"{what} took {sw.Elapsed} (budget {budget}); {string.Join("; ", timings)}");
            return result;
        }

        await using (var context = create())
        {
            await Time("seed", async () => { await RecordSeeder.SeedAsync(context, Voters); return 0; }, TimeSpan.FromMinutes(5));
        }

        var worker = ImportTests.CreateWorker(create);
        await Time("InsertAddresses", async () => { await worker.InsertAddresses(); return 0; }, TimeSpan.FromMinutes(5));
        await Time("InsertVoters", async () => { await worker.InsertVoters(); return 0; }, TimeSpan.FromMinutes(5));

        await using var db = create();
        Assert.That(await db.Voters.CountAsync(), Is.EqualTo(Voters));
        Assert.That(await db.Addresses.CountAsync(), Is.EqualTo((Voters - 1) / 2 + 1));

        var searchBudget = TimeSpan.FromSeconds(2);
        // Same shapes as ApiController's suggestion endpoints.
        await Time("FirstNameSuggestions", () => db.Voters.Where(v => v.FIRST_NAME.StartsWith("First19")).Select(v => v.FIRST_NAME).Distinct().OrderBy(x => x).Take(50).ToArrayAsync(), searchBudget);
        await Time("LastNameSuggestions", () => db.Voters.Where(v => v.LAST_NAME.StartsWith("Last49")).Select(v => v.LAST_NAME).Distinct().OrderBy(x => x).Take(50).ToArrayAsync(), searchBudget);
        await Time("CitySuggestions", () => db.Addresses.Where(a => a.CITY.StartsWith("City29")).Select(a => a.CITY).Distinct().OrderBy(x => x).Take(50).ToArrayAsync(), searchBudget);
        await Time("StreetSuggestions", () => db.Addresses.Where(a => a.FullStreetAddress.StartsWith("12 Street")).Select(a => a.FullStreetAddress).OrderBy(x => x).Take(50).ToArrayAsync(), searchBudget);
    }
}
