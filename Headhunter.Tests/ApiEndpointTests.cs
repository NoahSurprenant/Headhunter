using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Diagnostics;
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
    public async Task StreetSuggestions_MatchTheStartOfTheStreetName()
    {
        var streets = await _client.GetFromJsonAsync<string[]>("/Api/StreetSuggestions?query=Meri");

        Assert.That(streets, Is.EqualTo(new[] { "1 Meridian Rd", "2 Meridian Rd" }));
    }

    [Test]
    public async Task StreetSuggestions_MatchTheStartOfTheAddress()
    {
        var streets = await _client.GetFromJsonAsync<string[]>("/Api/StreetSuggestions?query=100%20Ma");

        Assert.That(streets, Is.EqualTo(new[] { "100 Maple St" }));
    }

    // Prefix searches only, so they can seek an index: a word inside the address doesn't match.
    [Test]
    public async Task StreetSuggestions_DontMatchTheMiddleOfTheAddress()
    {
        var streets = await _client.GetFromJsonAsync<string[]>("/Api/StreetSuggestions?query=Rd");

        Assert.That(streets, Is.Empty);
    }

    [Test]
    public async Task Area_ReturnsAddressesInsideTheBoxWithTheirVoters()
    {
        var area = await _client.GetFromJsonAsync<AreaDto>(
            "/Api/Area?west=-84.5&east=-83&north=43.5&south=42");

        Assert.That(area!.Clusters, Is.Empty);
        var addresses = area.Addresses;
        Assert.That(addresses.Select(a => a.Id), Is.EquivalentTo(new[] { TestData.MapleAddressId, TestData.OakAddressId }));
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
        var area = await _client.GetFromJsonAsync<AreaDto>(
            "/Api/Area?west=179&east=-179&north=11&south=9");

        Assert.That(area!.Addresses.Select(a => a.Id),
            Is.EquivalentTo(new[] { TestData.DatelineEastAddressId, TestData.DatelineWestAddressId }));
    }

    // Over Area:MaxAddresses, the box comes back as a grid of counts instead of addresses.
    private static HttpClient ClusteringClient(int gridCells) => new HeadhunterApiFactory("Production")
        .WithWebHostBuilder(b =>
        {
            b.UseSetting("Area:MaxAddresses", "1");
            b.UseSetting("Area:GridCells", gridCells.ToString());
        })
        .CreateClient();

    [Test]
    public async Task Area_OverTheLimit_ReturnsClustersWithCountsAndCentres()
    {
        using var client = ClusteringClient(gridCells: 1);

        var area = await client.GetFromJsonAsync<AreaDto>("/Api/Area?west=-84.5&east=-83&north=43.5&south=42");

        Assert.That(area!.Addresses, Is.Empty);
        var cluster = area.Clusters.Single();
        Assert.Multiple(() =>
        {
            // Maple (42.5, -83.5) and Oak (43, -84).
            Assert.That(cluster.Count, Is.EqualTo(2));
            Assert.That(cluster.Latitude, Is.EqualTo(42.75).Within(1e-9));
            Assert.That(cluster.Longitude, Is.EqualTo(-83.75).Within(1e-9));
        });
    }

    [Test]
    public async Task Area_Clusters_SplitTheBoxIntoAGrid()
    {
        using var client = ClusteringClient(gridCells: 40);

        var area = await client.GetFromJsonAsync<AreaDto>("/Api/Area?west=-84.5&east=-83&north=43.5&south=42");

        Assert.That(area!.Clusters.Select(c => c.Count), Is.EquivalentTo(new[] { 1, 1 }));
    }

    // Eastedge at 179.5 and Westedge at -179.5 are one degree apart across the date line: their centre is on it, not at 0.
    [Test]
    public async Task Area_Clusters_AverageAcrossTheDateLine()
    {
        using var client = ClusteringClient(gridCells: 1);

        var area = await client.GetFromJsonAsync<AreaDto>("/Api/Area?west=179&east=-179&north=11&south=9");

        var cluster = area!.Clusters.Single();
        Assert.Multiple(() =>
        {
            Assert.That(cluster.Count, Is.EqualTo(2));
            Assert.That(Math.Abs(cluster.Longitude), Is.EqualTo(180).Within(1e-9));
        });
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
    public async Task Relatives_MatchesNearbyOrSameStreet_ExcludingHouseholdAndFarMatches()
    {
        var relatives = await _client.GetFromJsonAsync<RelativeDto[]>($"/Api/Voter/{TestData.RelativeRefId}/Relatives");

        Assert.That(relatives, Is.Not.Null);
        Assert.Multiple(() =>
        {
            // RelativeHouseholdId shares the address (excluded); RelativeFarId shares the last name but is
            // neither nearby nor on the same street (excluded).
            Assert.That(relatives!.Select(r => r.Id),
                Is.EqualTo(new[] { TestData.RelativeNearId, TestData.RelativeStreetId }), "closest first");
            var near = relatives!.Single(r => r.Id == TestData.RelativeNearId);
            var street = relatives!.Single(r => r.Id == TestData.RelativeStreetId);
            Assert.That(near.DistanceKm, Is.Not.Null.And.LessThan(2.0));
            Assert.That(street.DistanceKm, Is.Not.Null.And.GreaterThan(100));
        });
    }

    [Test]
    public async Task Relatives_UnknownVoterIsNotFound()
    {
        using var response = await _client.GetAsync($"/Api/Voter/{Guid.NewGuid()}/Relatives");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    // A common surname (Commonname) has NoiseCount extra voters scattered far away, none of them a real
    // match: the result must still be exactly the two real relatives, and quick despite the noise.
    [Test]
    public async Task Relatives_CommonSurname_StaysFastAndCapped()
    {
        var stopwatch = Stopwatch.StartNew();

        var relatives = await _client.GetFromJsonAsync<RelativeDto[]>($"/Api/Voter/{TestData.CommonRefId}/Relatives");

        stopwatch.Stop();
        TestContext.Out.WriteLine($"Relatives for a common surname ({TestData.NoiseCount} noise voters): {stopwatch.ElapsedMilliseconds} ms");

        Assert.That(relatives, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(relatives!.Select(r => r.Id),
                Is.EquivalentTo(new[] { TestData.CommonNearId, TestData.CommonStreetId }));
            // Generous for CI; the point is that it doesn't scale with how many voters share the name.
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000));
        });
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
