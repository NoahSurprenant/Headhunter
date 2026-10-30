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
    public async Task Voter_WithNoMailingAddress_HasNoMailingFlagsOrLines()
    {
        var voter = await _client.GetFromJsonAsync<VoterDetailDto>($"/Api/Voter/{TestData.AdaId}");

        Assert.Multiple(() =>
        {
            Assert.That(voter!.HasMailingAddress, Is.False);
            Assert.That(voter.MailingAddressOutOfState, Is.False);
            Assert.That(voter.MailingAddressLines, Is.Empty);
        });
    }

    [Test]
    public async Task Voter_WithAnOutOfStateMailingAddress_FlagsBoth()
    {
        var voter = await _client.GetFromJsonAsync<VoterDetailDto>($"/Api/Voter/{TestData.DanaId}");

        Assert.Multiple(() =>
        {
            Assert.That(voter!.HasMailingAddress, Is.True);
            Assert.That(voter.MailingAddressOutOfState, Is.True);
            Assert.That(voter.MailingAddressLines, Is.EqualTo(new[] { "PO Box 1", "Chicago, IL 60601" }));
        });
    }

    [Test]
    public async Task Voter_WithAnInStateMailingAddress_OnlyFlagsLivesElsewhere()
    {
        var voter = await _client.GetFromJsonAsync<VoterDetailDto>($"/Api/Voter/{TestData.EmersonId}");

        Assert.Multiple(() =>
        {
            Assert.That(voter!.HasMailingAddress, Is.True);
            Assert.That(voter.MailingAddressOutOfState, Is.False);
        });
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
    public async Task Walk_DefaultsToTheSameSideOfTheStreet()
    {
        // House 103 (odd): previous and next odd-numbered houses on the block, evens skipped.
        var walk = await _client.GetFromJsonAsync<WalkDto>($"/Api/Address/{TestData.ElmAddressIds[2]}/Walk?sameSideOnly=true");

        Assert.Multiple(() =>
        {
            Assert.That(walk!.Previous!.Id, Is.EqualTo(TestData.ElmAddressIds[0])); // 101
            Assert.That(walk.Previous!.FullStreetAddress, Is.EqualTo("101 Elm St"));
            Assert.That(walk.Next!.Id, Is.EqualTo(TestData.ElmAddressIds[4])); // 105
        });
    }

    [Test]
    public async Task Walk_SameSideOnlyFalse_IncludesBothSidesOfTheStreet()
    {
        var walk = await _client.GetFromJsonAsync<WalkDto>($"/Api/Address/{TestData.ElmAddressIds[2]}/Walk?sameSideOnly=false");

        Assert.Multiple(() =>
        {
            Assert.That(walk!.Previous!.Id, Is.EqualTo(TestData.ElmAddressIds[1])); // 102
            Assert.That(walk.Next!.Id, Is.EqualTo(TestData.ElmAddressIds[3])); // 104
        });
    }

    [Test]
    public async Task Walk_FirstAndLastHouseOnTheSideHaveNoNeighborThatWay()
    {
        var first = await _client.GetFromJsonAsync<WalkDto>($"/Api/Address/{TestData.ElmAddressIds[0]}/Walk?sameSideOnly=true"); // 101
        var last = await _client.GetFromJsonAsync<WalkDto>($"/Api/Address/{TestData.ElmAddressIds[6]}/Walk?sameSideOnly=true"); // 107

        Assert.Multiple(() =>
        {
            Assert.That(first!.Previous, Is.Null);
            Assert.That(first.Next!.Id, Is.EqualTo(TestData.ElmAddressIds[2])); // 103
            Assert.That(last!.Next, Is.Null);
            Assert.That(last.Previous!.Id, Is.EqualTo(TestData.ElmAddressIds[4])); // 105
        });
    }

    [Test]
    public async Task Walk_UnknownAddressIsNotFound()
    {
        using var response = await _client.GetAsync($"/Api/Address/{Guid.NewGuid()}/Walk?sameSideOnly=true");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Walk_Nearby_OrdersByDistanceWithVoterCountsAndLimitsToTheConfiguredCount()
    {
        // House 101: distances to 102..107 increase monotonically (11m apart in a line), so the nearest 3
        // are unambiguous: 102, 103, 104, each with its seeded voter count.
        using var client = new HeadhunterApiFactory("Production")
            .WithWebHostBuilder(b => b.UseSetting("Walk:NearbyCount", "3"))
            .CreateClient();

        var walk = await client.GetFromJsonAsync<WalkDto>($"/Api/Address/{TestData.ElmAddressIds[0]}/Walk?sameSideOnly=true");

        Assert.That(walk!.Nearby.Select(n => n.Id), Is.EqualTo(new[]
        {
            TestData.ElmAddressIds[1], TestData.ElmAddressIds[2], TestData.ElmAddressIds[3],
        }));
        Assert.Multiple(() =>
        {
            Assert.That(walk.Nearby.Select(n => n.VoterCount), Is.EqualTo(new[] { 0, 1, 0 }));
            // Strictly increasing: each house is further from 101 than the last.
            Assert.That(walk.Nearby[0].DistanceMeters, Is.LessThan(walk.Nearby[1].DistanceMeters));
            Assert.That(walk.Nearby[1].DistanceMeters, Is.LessThan(walk.Nearby[2].DistanceMeters));
        });
    }

    [Test]
    public async Task Walk_Nearby_WidensTheSearchBoxUntilItHasEnoughOrGivesUp()
    {
        // Only 7 houses exist on Elm St; the default NearbyCount (8) can never be met, so the box widens
        // until it hits Walk:MaxDegrees and returns everything it found, including the far house.
        var walk = await _client.GetFromJsonAsync<WalkDto>($"/Api/Address/{TestData.ElmAddressIds[0]}/Walk?sameSideOnly=true");

        Assert.That(walk!.Nearby.Select(n => n.Id), Is.EqualTo(new[]
        {
            TestData.ElmAddressIds[1], TestData.ElmAddressIds[2], TestData.ElmAddressIds[3],
            TestData.ElmAddressIds[4], TestData.ElmAddressIds[5], TestData.ElmAddressIds[6],
            TestData.ElmFarAddressId,
        }));
    }

    [Test]
    public async Task NotOnMap_ListsAddressesWhereMatchedIsntTrue()
    {
        var page = await _client.GetFromJsonAsync<PaginationResult<NotOnMapAddressDto>>("/Api/NotOnMap?pageSize=10&pageNumber=1");

        Assert.That(page!.Results.Select(a => a.Id), Is.EquivalentTo(new[]
        {
            TestData.PineFailedAddressId, TestData.BirchNeverTriedAddressId, TestData.ElmFailedAddressId,
        }));
        var pine = page.Results.Single(a => a.Id == TestData.PineFailedAddressId);
        Assert.Multiple(() =>
        {
            Assert.That(pine.Matched, Is.False);
            Assert.That(pine.City, Is.EqualTo("Lansing"));
            Assert.That(pine.County, Is.EqualTo("Ingham"));
            Assert.That(page.Results.Single(a => a.Id == TestData.BirchNeverTriedAddressId).Matched, Is.Null);
        });
    }

    [Test]
    public async Task NotOnMap_FiltersByCity()
    {
        var page = await _client.GetFromJsonAsync<PaginationResult<NotOnMapAddressDto>>("/Api/NotOnMap?city=Flint");

        Assert.That(page!.Results.Select(a => a.Id), Is.EqualTo(new[] { TestData.ElmFailedAddressId }));
    }

    [Test]
    public async Task NotOnMap_FiltersByCounty()
    {
        var page = await _client.GetFromJsonAsync<PaginationResult<NotOnMapAddressDto>>("/Api/NotOnMap?county=Ingham");

        Assert.That(page!.Results.Select(a => a.Id), Is.EquivalentTo(new[] { TestData.PineFailedAddressId, TestData.BirchNeverTriedAddressId }));
    }

    [Test]
    public async Task NotOnMap_Paginates()
    {
        var page1 = await _client.GetFromJsonAsync<PaginationResult<NotOnMapAddressDto>>("/Api/NotOnMap?pageNumber=1&pageSize=2");
        var page2 = await _client.GetFromJsonAsync<PaginationResult<NotOnMapAddressDto>>("/Api/NotOnMap?pageNumber=2&pageSize=2");

        Assert.Multiple(() =>
        {
            Assert.That(page1!.TotalCount, Is.EqualTo(3));
            Assert.That(page1.Results, Has.Length.EqualTo(2));
            Assert.That(page2!.Results, Has.Length.EqualTo(1));
        });
    }

    [Test]
    public async Task NotOnMap_DoesNotIncludeMatchedAddresses()
    {
        var page = await _client.GetFromJsonAsync<PaginationResult<NotOnMapAddressDto>>("/Api/NotOnMap?pageSize=50");

        Assert.That(page!.Results.Select(a => a.Id), Does.Not.Contain(TestData.MapleAddressId).And.Not.Contain(TestData.OakAddressId));
    }

    [Test]
    public async Task NotOnMapCounties_CountsUnmatchedAddressesPerCounty()
    {
        var counties = await _client.GetFromJsonAsync<CountyCountDto[]>("/Api/NotOnMap/Counties");

        var ingham = counties!.Single(c => c.County == "Ingham");
        var genesee = counties.Single(c => c.County == "Genesee");
        Assert.Multiple(() =>
        {
            Assert.That(ingham.Count, Is.EqualTo(2));
            Assert.That(genesee.Count, Is.EqualTo(1));
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

    // A box drawn on the globe narrows the list to the voters living inside it.
    [Test]
    public async Task Voters_FiltersByTheBoxOnTheGlobe()
    {
        var filter = new SearchFilterDto { West = -83.6m, East = -83.4m, North = 42.6m, South = 42.4m };

        using var response = await _client.PostAsJsonAsync("/Api/Voters?PageNumber=1&PageSize=10", filter);
        var result = await response.Content.ReadFromJsonAsync<PaginationResult<VoterGridDto>>();

        Assert.That(result!.Results.Select(v => v.ID), Is.EquivalentTo(new[] { TestData.AdaId, TestData.AdalineId }));
    }

    [Test]
    public async Task Voters_BoxCombinesWithTheOtherFilters()
    {
        // Oak's box holds Ada Placeholder and Bruno Fakename; only Bruno is a Fakename.
        var filter = new SearchFilterDto { LastName = "Fakename", West = -84.1m, East = -83.9m, North = 43.1m, South = 42.9m };

        using var response = await _client.PostAsJsonAsync("/Api/Voters?PageNumber=1&PageSize=10", filter);
        var result = await response.Content.ReadFromJsonAsync<PaginationResult<VoterGridDto>>();

        Assert.That(result!.Results.Select(v => v.ID), Is.EqualTo(new[] { TestData.BrunoId }));
    }

    [Test]
    public async Task Voters_BoxCrossingTheDateLine()
    {
        // 179 to -179 is two degrees across the date line, not 358 degrees around the world through Michigan.
        var filter = new SearchFilterDto { West = 179m, East = -179m, North = 11m, South = 9m };

        using var response = await _client.PostAsJsonAsync("/Api/Voters?PageNumber=1&PageSize=10", filter);
        var result = await response.Content.ReadFromJsonAsync<PaginationResult<VoterGridDto>>();

        Assert.That(result!.Results.Select(v => v.ID), Is.EqualTo(new[] { TestData.CleoId }));
    }

    [Test]
    public async Task Voters_PartOfABoxIsIgnored()
    {
        var filter = new SearchFilterDto { LastName = "Fakename", West = 179m };

        using var response = await _client.PostAsJsonAsync("/Api/Voters?PageNumber=1&PageSize=10", filter);
        var result = await response.Content.ReadFromJsonAsync<PaginationResult<VoterGridDto>>();

        Assert.That(result!.TotalCount, Is.EqualTo(3));
    }

    [Test]
    public async Task VotersCsv_DownloadsEveryMatchingVoter()
    {
        using var response = await _client.GetAsync("/Api/VotersCsv?lastName=Fakename");
        var lines = (await response.Content.ReadAsStringAsync()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(l => l.Split('\n', StringSplitOptions.RemoveEmptyEntries)).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("text/csv"));
            Assert.That(response.Content.Headers.ContentDisposition!.FileName, Is.EqualTo("voters.csv"));
            Assert.That(lines, Is.EqualTo(new[]
            {
                "Name,Birth Year,Gender,Address,City,Zip,Registration Date",
                $"Ada Q Fakename,{TestData.ThisYear - 30},F,100 Maple St,Testville,00000,2020-01-15",
                $"Adaline R Fakename,{TestData.ThisYear - 31},F,100 Maple St,Testville,00000,2022-06-01",
                $"Bruno T Fakename,{TestData.ThisYear - 50},M,200 Oak Ave,Testburg,00000,2024-10-02",
            }));
        });
    }

    [Test]
    public async Task VotersCsv_FiltersByABoxAcrossTheDateLine()
    {
        var csv = await _client.GetStringAsync("/Api/VotersCsv?west=179&east=-179&north=11&south=9");

        Assert.That(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(l => l.Split(',')[0]),
            Is.EqualTo(new[] { "Cleo U Sample" }));
    }

    [Test]
    public async Task VotersCsv_StopsAtExportMaxRows()
    {
        using var client = new HeadhunterApiFactory("Production")
            .WithWebHostBuilder(b => b.UseSetting("Export:MaxRows", "2"))
            .CreateClient();

        var csv = await client.GetStringAsync("/Api/VotersCsv");

        // The header and two of the five voters.
        Assert.That(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries), Has.Length.EqualTo(3));
    }

    [Test]
    public async Task Voters_FlagsMailingAddressAndOutOfStateOnEachRow()
    {
        var filter = new SearchFilterDto { LastName = "Transplant" };

        using var response = await _client.PostAsJsonAsync("/Api/Voters?PageNumber=1&PageSize=10", filter);
        var result = await response.Content.ReadFromJsonAsync<PaginationResult<VoterGridDto>>();

        var dana = result!.Results.Single(v => v.ID == TestData.DanaId);
        var emerson = result.Results.Single(v => v.ID == TestData.EmersonId);
        Assert.Multiple(() =>
        {
            Assert.That(dana.HasMailingAddress, Is.True);
            Assert.That(dana.MailingAddressOutOfState, Is.True);
            Assert.That(emerson.HasMailingAddress, Is.True);
            Assert.That(emerson.MailingAddressOutOfState, Is.False);
        });
    }

    [Test]
    public async Task Voters_MailingAddressDiffersFilter_OnlyMatchesVotersWithOne()
    {
        using var response = await _client.PostAsJsonAsync("/Api/Voters?PageNumber=1&PageSize=50&mailingAddressDiffers=true", new SearchFilterDto());
        var result = await response.Content.ReadFromJsonAsync<PaginationResult<VoterGridDto>>();

        Assert.That(result!.Results.Select(v => v.ID), Is.EquivalentTo(new[] { TestData.DanaId, TestData.EmersonId }));
    }

    // The query param combines with the request body's filters (an AND), rather than replacing them.
    [Test]
    public async Task Voters_MailingAddressDiffersFilter_CombinesWithTheSearchFormFilters()
    {
        var filter = new SearchFilterDto { FirstName = "Dana" };

        using var response = await _client.PostAsJsonAsync("/Api/Voters?PageNumber=1&PageSize=50&mailingAddressDiffers=true", filter);
        var result = await response.Content.ReadFromJsonAsync<PaginationResult<VoterGridDto>>();

        Assert.That(result!.Results.Select(v => v.ID), Is.EqualTo(new[] { TestData.DanaId }));
    }

    [Test]
    public async Task Voters_WithoutTheMailingAddressDiffersFilter_IncludesEveryone()
    {
        // Ada (no mailing address) and Dana (one), each found by name: the unfiltered list is longer than a page.
        async Task<Guid[]> Ids(string lastName)
        {
            using var response = await _client.PostAsJsonAsync("/Api/Voters?PageNumber=1&PageSize=50", new SearchFilterDto { LastName = lastName });
            var result = await response.Content.ReadFromJsonAsync<PaginationResult<VoterGridDto>>();
            return result!.Results.Select(v => v.ID).ToArray();
        }

        var fakenames = await Ids("Fakename");
        var transplants = await Ids("Transplant");

        Assert.Multiple(() =>
        {
            Assert.That(fakenames, Does.Contain(TestData.AdaId));
            Assert.That(transplants, Does.Contain(TestData.DanaId));
        });
    }

    // The download takes the same flag as the list, so it exports what the list shows.
    [Test]
    public async Task VotersCsv_MailingAddressDiffersFilter_OnlyExportsVotersWithOne()
    {
        var csv = await _client.GetStringAsync("/Api/VotersCsv?mailingAddressDiffers=true");

        Assert.That(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(l => l.Split(',')[0]),
            Is.EquivalentTo(new[] { "Dana V Transplant", "Emerson W Transplant" }));
    }
}
