using Headhunter.Database;
using Microsoft.EntityFrameworkCore;

namespace Headhunter.Tests;

public class DatabaseTests
{
    [Test]
    public async Task Migrations_AreAllAppliedAndMatchTheModel()
    {
        await using var context = SqlServerFixture.CreateContext();

        var pending = await context.Database.GetPendingMigrationsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(pending, Is.Empty);
            Assert.That(context.Database.HasPendingModelChanges(), Is.False,
                "The EF model differs from the last migration snapshot");
        });
    }

    [Test]
    public async Task StartsWith_IsTranslatedToSqlAndCaseInsensitiveUnderDefaultCollation()
    {
        await using var context = SqlServerFixture.CreateContext();

        // SQL_Latin1_General_CP1_CI_AS (the SQL Server default) is case-insensitive; the API relies on that.
        var names = await context.Voters
            .Where(v => v.FIRST_NAME.StartsWith("ada"))
            .Select(v => v.FIRST_NAME)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();

        Assert.That(names, Is.EqualTo(new[] { "Ada", "Adaline" }));
    }

    [Test]
    public async Task VoterToAddress_NavigationLoadsWithSplitQuery()
    {
        await using var context = SqlServerFixture.CreateContext();

        var address = await context.Addresses
            .Include(a => a.Voters)
            .AsSplitQuery()
            .SingleAsync(a => a.ID == TestData.OakAddressId);

        Assert.That(address.Voters.Select(v => v.ID), Is.EquivalentTo(new[] { TestData.AdaOakId, TestData.BrunoId }));
    }

    [Test]
    public async Task Voter_IsLinkedToItsRawMichiganVoterRecord()
    {
        await using var context = SqlServerFixture.CreateContext();

        var voter = await context.Voters
            .Include(v => v.MichiganVoterRecord)
            .SingleAsync(v => v.ID == TestData.CleoId);

        Assert.That(voter.MichiganVoterRecord.FIRST_NAME, Is.EqualTo("Cleo"));
    }

    // Without these indexes the suggestion queries scan the whole table: ~20 s for CitySuggestions on the
    // 4M-row production Addresses table (issue #5).
    [TestCase("IX_Addresses_CITY")]
    [TestCase("IX_Voters_FIRST_NAME")]
    [TestCase("IX_Voters_MIDDLE_NAME")]
    [TestCase("IX_Voters_LAST_NAME")]
    [TestCase("IX_Addresses_FullStreetAddress")]
    [TestCase("IX_Addresses_STREET_NAME")]
    public async Task SuggestionQuery_SeeksItsIndex(string index)
    {
        await using var context = SqlServerFixture.CreateContext();

        IQueryable<string> query = index switch
        {
            "IX_Addresses_CITY" => context.Addresses.Where(a => a.CITY.StartsWith("Oa")).Select(a => a.CITY),
            "IX_Voters_FIRST_NAME" => context.Voters.Where(v => v.FIRST_NAME.StartsWith("Ada")).Select(v => v.FIRST_NAME),
            "IX_Voters_MIDDLE_NAME" => context.Voters.Where(v => v.MIDDLE_NAME.StartsWith("A")).Select(v => v.MIDDLE_NAME),
            "IX_Voters_LAST_NAME" => context.Voters.Where(v => v.LAST_NAME.StartsWith("B")).Select(v => v.LAST_NAME),
            "IX_Addresses_FullStreetAddress" => context.Addresses.Where(a => a.FullStreetAddress.StartsWith("100 Ma")).Select(a => a.FullStreetAddress),
            // The street name itself: with four test rows SQL Server would rather scan the small
            // FullStreetAddress index for the full addresses than seek this one.
            "IX_Addresses_STREET_NAME" => context.Addresses.Where(a => a.STREET_NAME.StartsWith("Meri")).Select(a => a.STREET_NAME),
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
        // Same shape as the API's suggestion endpoints.
        var sql = query.Distinct().OrderBy(x => x).Take(50).ToQueryString();

        var plan = await GetEstimatedPlanXml(context, sql);

        Assert.Multiple(() =>
        {
            Assert.That(plan, Does.Contain($"Index=\"[{index}]\""), $"Plan does not use {index}:\n{plan}");
            Assert.That(plan, Does.Contain("<SeekPredicates>"), $"Plan has no index seek:\n{plan}");
        });
    }

    private static async Task<string> GetEstimatedPlanXml(HeadhunterContext context, string sql)
    {
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await using (var on = connection.CreateCommand())
            {
                on.CommandText = "SET SHOWPLAN_XML ON";
                await on.ExecuteNonQueryAsync();
            }

            var plans = new System.Text.StringBuilder();
            await using (var command = connection.CreateCommand())
            {
                // ToQueryString() emits DECLAREs for the parameters followed by the query, so it runs as one batch.
                command.CommandText = sql;
                await using var reader = await command.ExecuteReaderAsync();
                do
                {
                    while (await reader.ReadAsync())
                    {
                        plans.AppendLine(reader.GetString(0));
                    }
                } while (await reader.NextResultAsync());
            }

            await using (var off = connection.CreateCommand())
            {
                off.CommandText = "SET SHOWPLAN_XML OFF";
                await off.ExecuteNonQueryAsync();
            }

            return plans.ToString();
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
