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
    [TestCase("IX_Voters_YEAR_OF_BIRTH")]
    [TestCase("IX_Voters_HAS_MAILING_ADDRESS")]
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
            // The voter search by birth year, age or star sign.
            "IX_Voters_YEAR_OF_BIRTH" => context.Voters.Where(v => v.YEAR_OF_BIRTH == 1990).Select(v => v.YEAR_OF_BIRTH.ToString()),
            "IX_Addresses_STREET_NAME" => context.Addresses.Where(a => a.STREET_NAME.StartsWith("Meri")).Select(a => a.STREET_NAME),
            // The "Mailing address differs" filter: seeks the persisted computed column instead of comparing
            // MAILING_ADDRESS_LINE_ONE (nvarchar(max), not indexable) to '' on every row.
            "IX_Voters_HAS_MAILING_ADDRESS" => context.Voters.Where(v => v.HAS_MAILING_ADDRESS).Select(v => v.ID.ToString()),
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

    // The district suggestions (and the filters, on the same columns) seek the district indexes. The jurisdiction
    // leads two composite indexes, so either may be picked for it.
    [TestCase(API.DistrictType.County, "", "IX_Addresses_COUNTY_NAME_COUNTY_COMMISSIONER_DISTRICT_NAME")]
    [TestCase(API.DistrictType.Jurisdiction, "", "IX_Addresses_JURISDICTION_NAME_")]
    [TestCase(API.DistrictType.Precinct, "CITY OF TESTVILLE", "IX_Addresses_JURISDICTION_NAME_PRECINCT")]
    [TestCase(API.DistrictType.Ward, "CITY OF TESTVILLE", "IX_Addresses_JURISDICTION_NAME_WARD")]
    [TestCase(API.DistrictType.SchoolDistrict, "", "IX_Addresses_SCHOOL_DISTRICT_NAME")]
    [TestCase(API.DistrictType.StateHouse, "", "IX_Addresses_STATE_HOUSE_DISTRICT_NAME")]
    [TestCase(API.DistrictType.StateSenate, "", "IX_Addresses_STATE_SENATE_DISTRICT_NAME")]
    [TestCase(API.DistrictType.UsCongress, "", "IX_Addresses_US_CONGRESS_DISTRICT_NAME")]
    [TestCase(API.DistrictType.CountyCommissioner, "TESTSHIRE", "IX_Addresses_COUNTY_NAME_COUNTY_COMMISSIONER_DISTRICT_NAME")]
    public async Task DistrictSuggestionQuery_SeeksItsIndex(API.DistrictType type, string within, string index)
    {
        await using var context = SqlServerFixture.CreateContext();

        // Same shape as DistrictController.DistrictSuggestions.
        var sql = API.Districts.Names(context.Addresses, type, within)
            .Where(x => x != "" && x.StartsWith("1"))
            .Distinct().OrderBy(x => x).Take(50).ToQueryString();

        var plan = await GetEstimatedPlanXml(context, sql);

        Assert.Multiple(() =>
        {
            Assert.That(plan, Does.Contain($"Index=\"[{index}"), $"Plan does not use {index}:\n{plan}");
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
