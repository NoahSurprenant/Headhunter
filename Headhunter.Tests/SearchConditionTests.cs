using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Headhunter.API;
using Headhunter.API.Pagination;
using Headhunter.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Headhunter.Tests;

/// <summary>
/// The voters page's advanced search (SearchConditions): each operator of each kind of field, against the three
/// Fakenames - Ada (born ThisYear-30, F, registered 2020-01-15, permanent absentee), Adaline (ThisYear-31, F,
/// 2022-06-01) and Bruno (ThisYear-50, M, 2024-10-02, permanent absentee, status V).
/// </summary>
public class SearchConditionTests
{
    private HeadhunterApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void StartApi()
    {
        _factory = new HeadhunterApiFactory("Production");
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task StopApi()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private static SearchConditionDto C(string field, string op, string value, string? value2 = null) =>
        new() { Field = field, Operator = op, Value = value, Value2 = value2 };

    /// <summary>The first names the list finds, among the Fakenames unless <paramref name="lastName"/> says otherwise.</summary>
    private async Task<string[]> FirstNames(string lastName, params SearchConditionDto[] conditions)
    {
        using var response = await _client.PostAsJsonAsync("/Api/Voters?PageNumber=1&PageSize=250",
            new SearchFilterDto { LastName = lastName, Conditions = [.. conditions] });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadFromJsonAsync<PaginationResult<VoterGridDto>>();
        return result!.Results.Select(v => v.FirstName).Order().ToArray();
    }

    private Task<string[]> Fakenames(params SearchConditionDto[] conditions) => FirstNames("Fakename", conditions);

    [TestCase("firstName", "equals", "Ada", new[] { "Ada" })]
    [TestCase("firstName", "notEquals", "Ada", new[] { "Adaline", "Bruno" })]
    [TestCase("firstName", "startsWith", "Ada", new[] { "Ada", "Adaline" })]
    [TestCase("firstName", "endsWith", "line", new[] { "Adaline" })]
    [TestCase("firstName", "contains", "run", new[] { "Bruno" })]
    // Any case, like the rest of the search (the default collation)
    [TestCase("firstName", "startsWith", "ada", new[] { "Ada", "Adaline" })]
    [TestCase("middleName", "equals", "R", new[] { "Adaline" })]
    [TestCase("street", "startsWith", "100 Ma", new[] { "Ada", "Adaline" })]
    [TestCase("street", "contains", "Oak", new[] { "Bruno" })]
    [TestCase("city", "equals", "Testburg", new[] { "Bruno" })]
    [TestCase("city", "notEquals", "Testburg", new[] { "Ada", "Adaline" })]
    [TestCase("zipCode", "equals", "00000", new[] { "Ada", "Adaline", "Bruno" })]
    [TestCase("ZIPCODE", "STARTSWITH", "1", new string[0])]
    public async Task TextOperators(string field, string op, string value, string[] expected)
    {
        Assert.That(await Fakenames(C(field, op, value)), Is.EqualTo(expected));
    }

    // A wildcard typed into the box is a character to match, not a wildcard: EF escapes it in the LIKE pattern.
    [Test]
    public async Task Text_LikeWildcardsAreLiteral()
    {
        var percent = await Fakenames(C("firstName", "startsWith", "%"));
        var underscore = await Fakenames(C("firstName", "contains", "_"));

        Assert.Multiple(() =>
        {
            Assert.That(percent, Is.Empty);
            Assert.That(underscore, Is.Empty);
        });
    }

    // Values as years before this one.
    [TestCase("eq", 30, null, new[] { "Ada" })]
    [TestCase("ne", 30, null, new[] { "Adaline", "Bruno" })]
    [TestCase("lt", 31, null, new[] { "Bruno" })]
    [TestCase("le", 31, null, new[] { "Adaline", "Bruno" })]
    [TestCase("gt", 31, null, new[] { "Ada" })]
    [TestCase("ge", 31, null, new[] { "Ada", "Adaline" })]
    [TestCase("between", 50, 31, new[] { "Adaline", "Bruno" })]
    // The wrong way round is the same range
    [TestCase("between", 31, 50, new[] { "Adaline", "Bruno" })]
    public async Task BirthYearOperators(string op, int yearsAgo, int? yearsAgo2, string[] expected)
    {
        var condition = C("birthYear", op, (TestData.ThisYear - yearsAgo).ToString(),
            yearsAgo2 is { } y2 ? (TestData.ThisYear - y2).ToString() : null);

        Assert.That(await Fakenames(condition), Is.EqualTo(expected));
    }

    // Age A is born this year minus A or the year before, as the simple form's Age: Ada (born Y-30) is 29 or 30,
    // Adaline (Y-31) 30 or 31, Bruno (Y-50) 49 or 50. Ranges take anyone who could be in them.
    [TestCase("eq", "30", null, new[] { "Ada", "Adaline" })]
    [TestCase("ne", "30", null, new[] { "Bruno" })]
    [TestCase("lt", "30", null, new[] { "Ada" })]
    [TestCase("le", "30", null, new[] { "Ada", "Adaline" })]
    [TestCase("gt", "30", null, new[] { "Adaline", "Bruno" })]
    [TestCase("gt", "31", null, new[] { "Bruno" })]
    [TestCase("ge", "50", null, new[] { "Bruno" })]
    [TestCase("between", "31", "50", new[] { "Adaline", "Bruno" })]
    public async Task AgeOperators(string op, string age, string? age2, string[] expected)
    {
        Assert.That(await Fakenames(C("age", op, age, age2)), Is.EqualTo(expected));
    }

    [TestCase("eq", "2020-01-15", null, new[] { "Ada" })]
    [TestCase("ne", "2020-01-15", null, new[] { "Adaline", "Bruno" })]
    [TestCase("lt", "2022-06-01", null, new[] { "Ada" })]
    [TestCase("le", "2022-06-01", null, new[] { "Ada", "Adaline" })]
    [TestCase("gt", "2022-06-01", null, new[] { "Bruno" })]
    [TestCase("ge", "2022-06-01", null, new[] { "Adaline", "Bruno" })]
    [TestCase("between", "2022-06-01", "2024-10-02", new[] { "Adaline", "Bruno" })]
    public async Task RegistrationDateOperators(string op, string date, string? date2, string[] expected)
    {
        Assert.That(await Fakenames(C("registrationDate", op, date, date2)), Is.EqualTo(expected));
    }

    [TestCase("gender", "is", "M", new[] { "Bruno" })]
    [TestCase("gender", "isNot", "M", new[] { "Ada", "Adaline" })]
    [TestCase("gender", "is", "f", new[] { "Ada", "Adaline" })]
    [TestCase("permanentAbsentee", "is", "yes", new[] { "Ada", "Bruno" })]
    [TestCase("permanentAbsentee", "isNot", "yes", new[] { "Adaline" })]
    [TestCase("permanentAbsentee", "is", "no", new[] { "Adaline" })]
    [TestCase("voterStatus", "is", "V", new[] { "Bruno" })]
    [TestCase("voterStatus", "isNot", "V", new[] { "Ada", "Adaline" })]
    public async Task ChoiceOperators(string field, string op, string value, string[] expected)
    {
        Assert.That(await Fakenames(C(field, op, value)), Is.EqualTo(expected));
    }

    [Test]
    public async Task VoterStatuses_ListsTheCodesInTheVoterFile()
    {
        var statuses = await _client.GetFromJsonAsync<string[]>("/Api/VoterStatuses");

        Assert.That(statuses, Is.EqualTo(new[] { "A", "V" }));
    }

    // With each other and with the simple form's fields: all of them (AND).
    [Test]
    public async Task Conditions_CombineWithEachOtherAndTheForm()
    {
        var conditionsOnly = await FirstNames("",
            C("lastName", "startsWith", "Fake"), C("gender", "is", "F"), C("registrationDate", "ge", "2021-01-01"));
        // The form's last name, and a city among the conditions: Ada Placeholder lives in Testburg too, but isn't one.
        var withTheForm = await Fakenames(C("city", "equals", "Testburg"), C("permanentAbsentee", "is", "yes"));
        var sameField = await Fakenames(C("firstName", "startsWith", "Ada"), C("firstName", "notEquals", "Ada"));

        Assert.Multiple(() =>
        {
            Assert.That(conditionsOnly, Is.EqualTo(new[] { "Adaline" }));
            Assert.That(withTheForm, Is.EqualTo(new[] { "Bruno" }));
            Assert.That(sameField, Is.EqualTo(new[] { "Adaline" }));
        });
    }

    // Without conditions (none sent, null or empty) the search is as it was.
    [TestCase("{\"lastName\":\"Fakename\"}")]
    [TestCase("{\"lastName\":\"Fakename\",\"conditions\":null}")]
    [TestCase("{\"lastName\":\"Fakename\",\"conditions\":[]}")]
    public async Task WithoutConditions_TheSearchIsUnchanged(string body)
    {
        using var response = await _client.PostAsync("/Api/Voters?PageNumber=1&PageSize=10",
            new StringContent(body, Encoding.UTF8, "application/json"));
        var result = await response.Content.ReadFromJsonAsync<PaginationResult<VoterGridDto>>();

        Assert.That(result!.Results.Select(v => v.ID), Is.EqualTo(new[] { TestData.AdaId, TestData.AdalineId, TestData.BrunoId }));
    }

    [TestCase("ssn", "equals", "1", null, "Unknown field \"ssn\"")]
    [TestCase("lastName", "gt", "A", null, "Unknown operator \"gt\" for lastName")]
    [TestCase("birthYear", "startsWith", "19", null, "Unknown operator \"startsWith\" for birthYear")]
    [TestCase("gender", "equals", "M", null, "Unknown operator \"equals\" for gender")]
    [TestCase("birthYear", "eq", "nineteen", null, "\"nineteen\" is not a valid birthYear")]
    [TestCase("birthYear", "eq", "1990.5", null, "is not a valid birthYear")]
    [TestCase("age", "eq", "200", null, "\"200\" is not a valid age")]
    [TestCase("registrationDate", "eq", "01/15/2020", null, "is not a valid registrationDate: expected a date as yyyy-MM-dd")]
    [TestCase("registrationDate", "eq", "2020-13-01", null, "is not a valid registrationDate")]
    [TestCase("birthYear", "between", "1990", null, "birthYear between needs two values")]
    [TestCase("birthYear", "between", "1990", "x", "\"x\" is not a valid birthYear")]
    [TestCase("gender", "is", "Q", null, "expected M, F or X")]
    [TestCase("permanentAbsentee", "is", "maybe", null, "expected yes or no")]
    [TestCase("voterStatus", "is", "A'; DROP TABLE Voters;--", null, "is not a valid voterStatus")]
    [TestCase("lastName", "equals", " ", null, "lastName equals needs a value")]
    public async Task BadConditions_Are400WithAClearMessage(string field, string op, string value, string? value2, string message)
    {
        using var response = await _client.PostAsJsonAsync("/Api/Voters?PageNumber=1&PageSize=10",
            new SearchFilterDto { Conditions = [C(field, op, value, value2)] });
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(problem!.Detail, Does.Contain(message));
        });
    }

    [Test]
    public async Task TooManyConditions_Are400()
    {
        var conditions = Enumerable.Range(0, 11).Select(_ => C("lastName", "equals", "Fakename")).ToList();

        using var response = await _client.PostAsJsonAsync("/Api/Voters?PageNumber=1&PageSize=10", new SearchFilterDto { Conditions = conditions });
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(problem!.Detail, Is.EqualTo("At most 10 conditions, not 11."));
        });
    }

    // The download takes the same conditions, as indexed query parameters (what the client's Export CSV link sends).
    [Test]
    public async Task VotersCsv_FiltersByTheConditions()
    {
        var csv = await _client.GetStringAsync("/Api/VotersCsv?lastName=Fakename"
            + "&conditions%5B0%5D.field=firstName&conditions%5B0%5D.operator=startsWith&conditions%5B0%5D.value=Ada"
            + "&conditions%5B1%5D.field=birthYear&conditions%5B1%5D.operator=between"
            + $"&conditions%5B1%5D.value={TestData.ThisYear - 40}&conditions%5B1%5D.value2={TestData.ThisYear - 30}");

        Assert.That(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(l => l.Split(',')[0]),
            Is.EqualTo(new[] { "Ada Q Fakename", "Adaline R Fakename" }));
    }

    [Test]
    public async Task VotersCsv_BadCondition_Is400()
    {
        using var response = await _client.GetAsync("/Api/VotersCsv?conditions%5B0%5D.field=ssn&conditions%5B0%5D.operator=equals&conditions%5B0%5D.value=1");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(problem!.Detail, Does.StartWith("Unknown field \"ssn\""));
        });
    }

    /// <summary>Every query takes longer than the one-second Search:CommandTimeout of <see cref="SlowSearchClient"/>.</summary>
    private sealed class SlowQueries : DbCommandInterceptor
    {
        private static void Slow(DbCommand command) => command.CommandText = "WAITFOR DELAY '00:00:03';\n" + command.CommandText;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Slow(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            Slow(command);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static WebApplicationFactory<API.Program> SlowSearchFactory() => new HeadhunterApiFactory("Production")
        .WithWebHostBuilder(b =>
        {
            b.UseSetting("Search:CommandTimeout", "00:00:01");
            b.ConfigureTestServices(s => s.ConfigureDbContext<HeadhunterContext>(o => o.AddInterceptors(new SlowQueries())));
        });

    // A search that runs out of Search:CommandTimeout (SQL Server's timeout, a SqlException -2) is a 422 the page shows.
    [Test]
    public async Task Voters_SearchThatTimesOut_Is422TooBroad()
    {
        await using var factory = SlowSearchFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/Api/Voters?PageNumber=1&PageSize=10",
            new SearchFilterDto { Conditions = [C("lastName", "contains", "ake")] });
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(problem!.Detail, Is.EqualTo(ApiController.TooBroadMessage));
        });
    }

    [Test]
    public async Task VotersCsv_SearchThatTimesOut_Is422RatherThanAShortFile()
    {
        await using var factory = SlowSearchFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/Api/VotersCsv?lastName=Fakename");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(problem!.Detail, Is.EqualTo(ApiController.TooBroadMessage));
        });
    }

    /// <summary>The SQL a condition becomes (no database needed). Printed, for a look in the test log.</summary>
    private static string Sql(params SearchConditionDto[] conditions)
    {
        using var context = SqlServerFixture.CreateContext();
        var sql = SearchConditions.Apply(context.Voters, conditions, 10, new DateTime(2026, 10, 30)).ToQueryString();
        TestContext.Progress.WriteLine($"--- {string.Join(" AND ", conditions.Select(c => $"{c.Field} {c.Operator} {c.Value} {c.Value2}"))}\n{sql}\n");
        return sql;
    }

    // Equals and starts with on the indexed columns, and ranges on YEAR_OF_BIRTH, stay sargable: a plain = or
    // LIKE 'x%' on the column (the % only at the end, in the parameter), never a function wrapped around it.
    [TestCase("lastName", "startsWith", "SUR", "[v].[LAST_NAME] LIKE @", "N'SUR%'")]
    [TestCase("firstName", "equals", "ADA", "[v].[FIRST_NAME] = @", "N'ADA'")]
    [TestCase("middleName", "startsWith", "Q", "[v].[MIDDLE_NAME] LIKE @", "N'Q%'")]
    [TestCase("city", "equals", "LANSING", "[a].[CITY] = @", "N'LANSING'")]
    [TestCase("street", "startsWith", "100 MA", "[a].[FullStreetAddress] LIKE @", "N'100 MA%'")]
    // A wildcard typed in is escaped in the parameter, so it is still a prefix search
    [TestCase("lastName", "startsWith", "50%", "[v].[LAST_NAME] LIKE @", @"N'50\%%'")]
    public void IndexedTextConditions_AreSargable(string field, string op, string value, string where, string parameter)
    {
        var sql = Sql(C(field, op, value));

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain(where));
            Assert.That(sql, Does.Contain(parameter));
            Assert.That(sql, Does.Not.Contain("LEFT(").And.Not.Contain("CHARINDEX").And.Not.Contain("LEN("));
        });
    }

    [Test]
    public void BirthYearAndAgeRanges_AreSargable()
    {
        var between = Sql(C("birthYear", "between", "1990", "1999"));
        var age = Sql(C("age", "eq", "30"));
        var equals = Sql(C("birthYear", "eq", "1990"));

        Assert.Multiple(() =>
        {
            Assert.That(between, Does.Contain("[v].[YEAR_OF_BIRTH] >= @").And.Contain("[v].[YEAR_OF_BIRTH] <= @"));
            Assert.That(between, Does.Contain("= 1990;").And.Contain("= 1999;"));
            // Age 30 on 2026-10-30: born 1995 or 1996.
            Assert.That(age, Does.Contain("[v].[YEAR_OF_BIRTH] >= @").And.Contain("[v].[YEAR_OF_BIRTH] <= @"));
            Assert.That(age, Does.Contain("= 1995;").And.Contain("= 1996;"));
            Assert.That(equals, Does.Contain("[v].[YEAR_OF_BIRTH] = @"));
        });
    }

    // The ones the page warns may be slow: no index seek, by their nature.
    [Test]
    public void ContainsEndsWithAndNotEquals_TranslateToo()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Sql(C("lastName", "contains", "UR")), Does.Contain("[v].[LAST_NAME] LIKE @").And.Contain("N'%UR%'"));
            Assert.That(Sql(C("lastName", "endsWith", "UR")), Does.Contain("[v].[LAST_NAME] LIKE @").And.Contain("N'%UR'"));
            Assert.That(Sql(C("lastName", "notEquals", "UR")), Does.Contain("[v].[LAST_NAME] <> @"));
            Assert.That(Sql(C("registrationDate", "eq", "2020-01-15")),
                Does.Contain("[v].[REGISTRATION_DATE] >= @").And.Contain("[v].[REGISTRATION_DATE] < @"));
        });
    }
}
