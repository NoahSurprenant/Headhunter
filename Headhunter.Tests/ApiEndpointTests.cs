using System.Net;
using System.Net.Http.Json;
using Headhunter.API;
using Headhunter.API.Pagination;

namespace Headhunter.Tests;

public class ApiEndpointTests
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

    [Test]
    public async Task FirstNameSuggestions_AreDistinctOrderedPrefixMatches()
    {
        var names = await _client.GetFromJsonAsync<string[]>("/Api/FirstNameSuggestions?query=Ada");

        Assert.That(names, Is.EqualTo(new[] { "Ada", "Adaline" }));
    }

    [Test]
    public async Task StreetSuggestions_MatchAnywhereInTheAddress()
    {
        var streets = await _client.GetFromJsonAsync<string[]>("/Api/StreetSuggestions?query=Meridian");

        Assert.That(streets, Is.EqualTo(new[] { "1 Meridian Rd", "2 Meridian Rd" }));
    }

    [Test]
    public async Task Area_ReturnsAddressesInsideTheBoxWithTheirVoters()
    {
        var addresses = await _client.GetFromJsonAsync<AddressDto[]>(
            "/Api/Area?west=-84.5&east=-83&north=43.5&south=42");

        Assert.That(addresses, Is.Not.Null);
        Assert.That(addresses!.Select(a => a.Id), Is.EquivalentTo(new[] { TestData.MapleAddressId, TestData.OakAddressId }));
        var maple = addresses.Single(a => a.Id == TestData.MapleAddressId);
        Assert.Multiple(() =>
        {
            // decimal(15,12) round-trips without losing precision.
            Assert.That(maple.Latitude, Is.EqualTo(42.500000000001m));
            Assert.That(maple.Voters.Select(v => v.FirstName), Is.EquivalentTo(new[] { "Ada", "Adaline" }));
        });
    }

    [Test]
    public async Task Area_HandlesBoxesThatCrossTheDateLine()
    {
        var addresses = await _client.GetFromJsonAsync<AddressDto[]>(
            "/Api/Area?west=179&east=-179&north=11&south=9");

        Assert.That(addresses!.Select(a => a.Id),
            Is.EquivalentTo(new[] { TestData.DatelineEastAddressId, TestData.DatelineWestAddressId }));
    }

    [Test]
    public async Task Voter_ReturnsDetailsWithAddress()
    {
        var voter = await _client.GetFromJsonAsync<VoterDetailDto>($"/Api/Voter/{TestData.AdaId}");

        Assert.That(voter, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(voter!.FirstName, Is.EqualTo("Ada"));
            Assert.That(voter.FullStreetAddress, Is.EqualTo("100 Maple St"));
            Assert.That(voter.AddressId, Is.EqualTo(TestData.MapleAddressId));
            Assert.That(voter.RegistrationDate, Is.EqualTo(new DateTime(2020, 1, 15)));
        });
    }

    [Test]
    public async Task Voter_UnknownIdIsNotFound()
    {
        using var response = await _client.GetAsync($"/Api/Voter/{Guid.NewGuid()}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Address_ListsEveryVoterAtTheAddress()
    {
        var voters = await _client.GetFromJsonAsync<VoterDetailDto[]>($"/Api/Address/{TestData.OakAddressId}");

        Assert.That(voters!.Select(v => v.FirstName), Is.EquivalentTo(new[] { "Ada", "Bruno" }));
    }

    [Test]
    public async Task Voters_FiltersByLastNameAndPaginates()
    {
        var filter = new SearchFilterDto { LastName = "Fakename" };

        using var page1 = await _client.PostAsJsonAsync("/Api/Voters?PageNumber=1&PageSize=2", filter);
        using var page2 = await _client.PostAsJsonAsync("/Api/Voters?PageNumber=2&PageSize=2", filter);
        var first = await page1.Content.ReadFromJsonAsync<PaginationResult<VoterGridDto>>();
        var second = await page2.Content.ReadFromJsonAsync<PaginationResult<VoterGridDto>>();

        Assert.Multiple(() =>
        {
            Assert.That(first!.TotalCount, Is.EqualTo(3));
            Assert.That(first.Results, Has.Length.EqualTo(2));
            Assert.That(second!.Results, Has.Length.EqualTo(1));
            // Ordered by ID, so the pages don't overlap and cover all three.
            Assert.That(first.Results.Concat(second.Results).Select(v => v.ID),
                Is.EqualTo(new[] { TestData.AdaId, TestData.AdalineId, TestData.BrunoId }));
        });
    }

    [Test]
    public async Task Voters_AgeFilterMatchesBothPossibleBirthYears()
    {
        // Age 30 means born this year minus 30 or minus 31, depending on whether the birthday has passed.
        var filter = new SearchFilterDto { Age = 30 };

        using var response = await _client.PostAsJsonAsync("/Api/Voters?PageNumber=1&PageSize=10", filter);
        var result = await response.Content.ReadFromJsonAsync<PaginationResult<VoterGridDto>>();

        Assert.That(result!.Results.Select(v => v.ID), Is.EquivalentTo(new[] { TestData.AdaId, TestData.AdalineId }));
    }

    [Test]
    public async Task Voters_NoMatchesReturnsEmptyPage()
    {
        var filter = new SearchFilterDto { LastName = "Nobody" };

        using var response = await _client.PostAsJsonAsync("/Api/Voters", filter);
        var result = await response.Content.ReadFromJsonAsync<PaginationResult<VoterGridDto>>();

        Assert.Multiple(() =>
        {
            Assert.That(result!.TotalCount, Is.Zero);
            Assert.That(result.Results, Is.Empty);
        });
    }
}
