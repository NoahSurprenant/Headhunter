using Headhunter.Database;
using Microsoft.EntityFrameworkCore;

namespace Headhunter.Tests;

public class DatabaseTests
{
    [Test]
    public async Task Migrations_AreAllAppliedAndMatchTheModel()
    {
        await using var context = DatabaseFixture.CreateContext();

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
        await using var context = DatabaseFixture.CreateContext();

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
        await using var context = DatabaseFixture.CreateContext();

        var address = await context.Addresses
            .Include(a => a.Voters)
            .AsSplitQuery()
            .SingleAsync(a => a.ID == TestData.OakAddressId);

        Assert.That(address.Voters.Select(v => v.ID), Is.EquivalentTo(new[] { TestData.AdaOakId, TestData.BrunoId }));
    }

    [Test]
    public async Task Voter_IsLinkedToItsRawMichiganVoterRecord()
    {
        await using var context = DatabaseFixture.CreateContext();

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
        // A showplan check; Postgres cannot seek a btree for LIKE on its case-insensitive (ICU) collation at all.
        Assume.That(DatabaseFixture.Provider, Is.EqualTo(DatabaseProvider.SqlServer), "SQL Server showplan only");

        await using var context = DatabaseFixture.CreateContext();

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

    // Postgres: the same, from the Suggestions the API runs. The test tables are tiny, so the planner is told not to
    // scan them; what matters is that the query can seek the index at all.
    [TestCase("FirstNames", "IX_Voters_FIRST_NAME_Prefix")]
    [TestCase("MiddleNames", "IX_Voters_MIDDLE_NAME_Prefix")]
    [TestCase("LastNames", "IX_Voters_LAST_NAME_Prefix")]
    [TestCase("Cities", "IX_Addresses_CITY_Prefix")]
    [TestCase("Streets", "IX_Addresses_FullStreetAddress_Prefix")]
    [TestCase("Streets", "IX_Addresses_STREET_NAME_Prefix")]
    public async Task PostgresSuggestionQuery_SeeksItsPrefixIndex(string suggestion, string index)
    {
        Assume.That(DatabaseFixture.Provider, Is.EqualTo(DatabaseProvider.Postgres), "Postgres plans only");

        await using var context = DatabaseFixture.CreateContext();
        var query = suggestion switch
        {
            "FirstNames" => API.Suggestions.FirstNames(context, "Ada"),
            "MiddleNames" => API.Suggestions.MiddleNames(context, "A"),
            "LastNames" => API.Suggestions.LastNames(context, "B"),
            "Cities" => API.Suggestions.Cities(context, "Oa"),
            "Streets" => API.Suggestions.Streets(context, "Meri"),
            _ => throw new ArgumentOutOfRangeException(nameof(suggestion)),
        };

        var plan = await ExplainPostgres(context, query.ToQueryString());

        Assert.That(plan, Does.Contain(index), $"Plan does not use {index}:\n{plan}");
    }

    // An index on upper(column) can only answer from the index alone (an index-only scan) if the column itself is
    // included: without it a common prefix ("MAR", 566k voters) read the table for every match, 3.4 s.
    [TestCase("IX_Voters_FIRST_NAME_Prefix")]
    [TestCase("IX_Voters_MIDDLE_NAME_Prefix")]
    [TestCase("IX_Voters_LAST_NAME_Prefix")]
    [TestCase("IX_Addresses_CITY_Prefix")]
    [TestCase("IX_Addresses_FullStreetAddress_Prefix")]
    [TestCase("IX_Addresses_STREET_NAME_Prefix")]
    public async Task PostgresPrefixIndex_IncludesItsColumn(string index)
    {
        Assume.That(DatabaseFixture.Provider, Is.EqualTo(DatabaseProvider.Postgres), "Postgres catalog only");

        await using var context = DatabaseFixture.CreateContext();
        var definition = await context.Database
            .SqlQueryRaw<string>("SELECT indexdef AS \"Value\" FROM pg_indexes WHERE indexname = {0}", index)
            .SingleAsync();

        Assert.That(definition, Does.Contain("INCLUDE"), definition);
    }

    // The voter search by city and birth year joins the two from covering indexes.
    [TestCase("IX_Addresses_CITY_ID")]
    [TestCase("IX_Voters_YEAR_OF_BIRTH_AddressID")]
    public async Task PostgresCityAndBirthYearSearch_UsesCoveringIndexes(string index)
    {
        Assume.That(DatabaseFixture.Provider, Is.EqualTo(DatabaseProvider.Postgres), "Postgres plans only");

        await using var context = DatabaseFixture.CreateContext();
        var sql = context.Voters.Where(v => v.Address.CITY == "Oakland" && v.YEAR_OF_BIRTH == 1990)
            .Select(v => v.ID).ToQueryString();

        var plan = await ExplainPostgres(context, sql, "SET LOCAL enable_hashjoin = on");

        Assert.That(plan, Does.Contain(index), $"Plan does not use {index}:\n{plan}");
    }

    private static async Task<string> ExplainPostgres(HeadhunterContext context, string sql, string? extraSetting = null)
    {
        // ToQueryString leaves the parameters as "-- @p='value'" comments above the statement.
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(sql, @"^-- (@\w+)='(.*?)'", System.Text.RegularExpressions.RegexOptions.Multiline))
            sql = sql.Replace(m.Groups[1].Value, "'" + m.Groups[2].Value.Replace("'", "''") + "'");

        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await using var transaction = await connection.BeginTransactionAsync();
            foreach (var setting in new[] { "SET LOCAL enable_seqscan = off", "SET LOCAL enable_bitmapscan = off", extraSetting })
            {
                if (setting is null) continue;
                await using var set = connection.CreateCommand();
                set.Transaction = transaction;
                set.CommandText = setting;
                await set.ExecuteNonQueryAsync();
            }
            await using var explain = connection.CreateCommand();
            explain.Transaction = transaction;
            explain.CommandText = "EXPLAIN " + sql;
            var plan = new System.Text.StringBuilder();
            await using var reader = await explain.ExecuteReaderAsync();
            while (await reader.ReadAsync()) plan.AppendLine(reader.GetString(0));
            return plan.ToString();
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    // The district suggestions check each district of the lookup table against the address index on its code (and the
    // filters use the same indexes). The jurisdiction leads two composite indexes, so either may be picked for it.
    [TestCase(API.DistrictType.County, "", "IX_Addresses_COUNTY_CODE_COUNTY_COMMISSIONER_DISTRICT_CODE")]
    [TestCase(API.DistrictType.Jurisdiction, "", "IX_Addresses_COUNTY_CODE_JURISDICTION_CODE_")]
    [TestCase(API.DistrictType.Precinct, "CITY OF TESTVILLE", "IX_Addresses_COUNTY_CODE_JURISDICTION_CODE_PRECINCT")]
    [TestCase(API.DistrictType.Ward, "CITY OF TESTVILLE", "IX_Addresses_COUNTY_CODE_JURISDICTION_CODE_WARD")]
    [TestCase(API.DistrictType.SchoolDistrict, "", "IX_Addresses_SCHOOL_DISTRICT_CODE")]
    [TestCase(API.DistrictType.StateHouse, "", "IX_Addresses_STATE_HOUSE_DISTRICT_CODE")]
    [TestCase(API.DistrictType.StateSenate, "", "IX_Addresses_STATE_SENATE_DISTRICT_CODE")]
    [TestCase(API.DistrictType.UsCongress, "", "IX_Addresses_US_CONGRESS_DISTRICT_CODE")]
    [TestCase(API.DistrictType.CountyCommissioner, "TESTSHIRE", "IX_Addresses_COUNTY_CODE_COUNTY_COMMISSIONER_DISTRICT_CODE")]
    public async Task DistrictSuggestionQuery_SeeksItsIndex(API.DistrictType type, string within, string index)
    {
        // A showplan check; Postgres cannot seek a btree for LIKE on its case-insensitive (ICU) collation at all.
        Assume.That(DatabaseFixture.Provider, Is.EqualTo(DatabaseProvider.SqlServer), "SQL Server showplan only");

        await using var context = DatabaseFixture.CreateContext();

        // Same shape as DistrictController.DistrictSuggestions.
        var sql = API.Districts.Names(context, type, within)
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
