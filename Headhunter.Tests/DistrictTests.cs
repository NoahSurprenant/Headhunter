using System.Net;
using System.Net.Http.Json;
using Headhunter.API;
using Headhunter.API.Pagination;

namespace Headhunter.Tests;

/// <summary>District suggestions, stats and extents, and the district filters on the voter search and the globe.</summary>
public class DistrictTests
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

    [TestCase("County", "T", "", new[] { "TESTSHIRE" })]
    [TestCase("Jurisdiction", "CITY", "", new[] { "CITY OF TESTBURG", "CITY OF TESTVILLE" })]
    [TestCase("SchoolDistrict", "TEST", "", new[] { "TESTBURG SCHOOLS", "TESTVILLE SCHOOLS" })]
    [TestCase("StateHouse", "", "", new[] { "1ST DISTRICT", "2ND DISTRICT", "3RD DISTRICT" })]
    [TestCase("StateSenate", "1", "", new[] { "1ST DISTRICT" })]
    [TestCase("UsCongress", "1", "", new[] { "1ST DISTRICT" })]
    // Precincts, wards and commissioner districts: those of the jurisdiction or county only.
    [TestCase("Precinct", "", "CITY OF TESTVILLE", new[] { "00001" })]
    [TestCase("Ward", "0", "CITY OF TESTBURG", new[] { "01" })]
    [TestCase("CountyCommissioner", "DIST", "TESTSHIRE", new[] { "DISTRICT 01", "DISTRICT 02" })]
    [TestCase("CountyCommissioner", "DIST", "OTHERSHIRE", new[] { "DISTRICT 01" })]
    // Without the jurisdiction they are meaningless; Eastedge has no ward, and a blank is no district.
    [TestCase("Precinct", "", "", new string[0])]
    [TestCase("Ward", "", "TOWNSHIP OF EASTEDGE", new string[0])]
    public async Task DistrictSuggestions_AreDistinctOrderedPrefixMatches(string type, string query, string within, string[] expected)
    {
        var names = await _client.GetFromJsonAsync<string[]>(
            $"/Api/DistrictSuggestions?type={type}&query={Uri.EscapeDataString(query)}&within={Uri.EscapeDataString(within)}");

        Assert.That(names, Is.EqualTo(expected));
    }

    [Test]
    public async Task District_CountsItsVotersHouseholdsAgesAndGenders()
    {
        var stats = await _client.GetFromJsonAsync<DistrictStatsDto>("/Api/District?type=County&name=TESTSHIRE");

        Assert.Multiple(() =>
        {
            Assert.That(stats!.Type, Is.EqualTo(DistrictType.County));
            // Ada and Adaline on Maple, Ada and Bruno on Oak.
            Assert.That(stats.Voters, Is.EqualTo(4));
            Assert.That(stats.Households, Is.EqualTo(2));
            Assert.That(stats.Ages.Select(a => a.Label), Is.EqualTo(new[] { "Under 25", "25–34", "35–44", "45–54", "55–64", "65–74", "75+" }));
            Assert.That(stats.Ages.Select(a => a.Count), Is.EqualTo(new[] { 0, 2, 0, 2, 0, 0, 0 }));
            Assert.That(stats.Genders.Select(g => (g.Gender, g.Count)), Is.EqualTo(new[] { ("F", 3), ("M", 1) }));
            // Ada and Bruno.
            Assert.That(stats.PermanentAbsenteePercent, Is.EqualTo(50.0));
            Assert.That(stats.NewestRegistrations.Select(r => r.Id),
                Is.EqualTo(new[] { TestData.BrunoId, TestData.AdalineId, TestData.AdaId, TestData.AdaOakId }));
            Assert.That(stats.NewestRegistrations[0].RegistrationDate, Is.EqualTo(new DateTime(2024, 10, 2)));
            Assert.That(stats.Extent!.West, Is.EqualTo(-84.0m));
            Assert.That(stats.Extent.East, Is.EqualTo(-83.500000000001m));
            Assert.That(stats.Extent.South, Is.EqualTo(42.500000000001m));
            Assert.That(stats.Extent.North, Is.EqualTo(43.0m));
        });
    }

    // A county with mixed genders (F, M, X), birth years spanning four age bands and a mix of permanent-absentee
    // voters across two households: the grouped query (GROUP BY YEAR_OF_BIRTH, GENDER) that replaced the six
    // separate counts/group-bys must still add up to the same totals, ages, gender split and absentee percent.
    [Test]
    public async Task District_WithMixedGendersAgesAndAbsentee_CountsThemCorrectly()
    {
        var stats = await _client.GetFromJsonAsync<DistrictStatsDto>("/Api/District?type=County&name=MIXCOUNTY");

        Assert.Multiple(() =>
        {
            Assert.That(stats!.Voters, Is.EqualTo(4));
            Assert.That(stats.Households, Is.EqualTo(2));
            Assert.That(stats.Ages.Select(a => a.Count), Is.EqualTo(new[] { 1, 0, 0, 1, 0, 1, 1 }));
            Assert.That(stats.Genders.Select(g => (g.Gender, g.Count)), Is.EqualTo(new[] { ("F", 2), ("M", 1), ("X", 1) }));
            // Two of the four (the Under-25 F and the 65-74 X) are permanent absentee.
            Assert.That(stats.PermanentAbsenteePercent, Is.EqualTo(50.0));
            Assert.That(stats.NewestRegistrations.Select(r => r.Id), Is.EqualTo(new[]
            {
                TestData.MixVoterF1Id, TestData.MixVoterM1Id, TestData.MixVoterX1Id, TestData.MixVoterF2Id,
            }));
            Assert.That(stats.Extent!.West, Is.EqualTo(-95.01m));
            Assert.That(stats.Extent.East, Is.EqualTo(-95.0m));
            Assert.That(stats.Extent.South, Is.EqualTo(50.0m));
            Assert.That(stats.Extent.North, Is.EqualTo(50.01m));
        });
    }

    // Precinct 00001 is also Maple's, in Testville: only Testburg's is asked for.
    [Test]
    public async Task District_PrecinctIsTheJurisdictions()
    {
        var stats = await _client.GetFromJsonAsync<DistrictStatsDto>(
            "/Api/District?type=Precinct&name=00001&within=CITY%20OF%20TESTBURG");

        Assert.Multiple(() =>
        {
            Assert.That(stats!.Voters, Is.EqualTo(2));
            Assert.That(stats.Within, Is.EqualTo("CITY OF TESTBURG"));
            Assert.That(stats.Ages.Single(a => a.Label == "45–54").Count, Is.EqualTo(2));
            Assert.That(stats.Extent!.West, Is.EqualTo(stats.Extent.East));
        });
    }

    [Test]
    public async Task District_WithoutItsJurisdictionIsABadRequest()
    {
        using var response = await _client.GetAsync("/Api/District?type=Ward&name=01");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task District_UnknownIsNotFound()
    {
        using var response = await _client.GetAsync("/Api/District?type=County&name=NOWHERE");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DistrictExtent_IsTheBoxAroundItsAddresses()
    {
        var extent = await _client.GetFromJsonAsync<DistrictExtentDto>(
            "/Api/DistrictExtent?type=CountyCommissioner&name=DISTRICT%2001&within=OTHERSHIRE");

        Assert.Multiple(() =>
        {
            Assert.That(extent!.West, Is.EqualTo(179.5m));
            Assert.That(extent.East, Is.EqualTo(179.5m));
            Assert.That(extent.North, Is.EqualTo(10.0m));
        });
    }

    [Test]
    public async Task DistrictExtent_UnknownIsNotFound()
    {
        using var response = await _client.GetAsync("/Api/DistrictExtent?type=SchoolDistrict&name=NOWHERE");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    // Maple's ward, Oak's school district: the box covers both, the globe only draws the district's.
    [TestCase("Ward", "01", "CITY%20OF%20TESTVILLE", "00000000-0000-0000-0000-00000000a001")]
    [TestCase("SchoolDistrict", "TESTBURG%20SCHOOLS", "", "00000000-0000-0000-0000-00000000a002")]
    public async Task Area_WithADistrict_OnlyItsAddresses(string type, string name, string within, string expected)
    {
        var area = await _client.GetFromJsonAsync<AreaDto>(
            $"/Api/Area?west=-84.5&east=-83&north=43.5&south=42&districtType={type}&district={name}&districtWithin={within}");

        Assert.That(area!.Addresses.Select(a => a.Id), Is.EqualTo(new[] { Guid.Parse(expected) }));
    }

    [Test]
    public async Task Voters_FilterByDistrict_CombinesWithTheOtherFilters()
    {
        var precinct = new SearchFilterDto { DistrictType = DistrictType.Precinct, District = "00001", DistrictWithin = "CITY OF TESTBURG" };
        var precinctAda = new SearchFilterDto { DistrictType = DistrictType.Precinct, District = "00001", DistrictWithin = "CITY OF TESTBURG", FirstName = "Ada" };

        var inPrecinct = await VoterIds(precinct);
        var adaInPrecinct = await VoterIds(precinctAda);

        Assert.Multiple(() =>
        {
            Assert.That(inPrecinct, Is.EquivalentTo(new[] { TestData.AdaOakId, TestData.BrunoId }));
            Assert.That(adaInPrecinct, Is.EqualTo(new[] { TestData.AdaOakId }));
        });
    }

    // The client sends the district type by name, and null when there is none.
    [Test]
    public async Task Voters_FilterByDistrict_ReadsTheTypeByName()
    {
        using var response = await _client.PostAsync("/Api/Voters?PageNumber=1&PageSize=10", JsonContent.Create(new
        {
            districtType = "StateHouse",
            district = "1ST DISTRICT",
        }));
        var named = await response.Content.ReadFromJsonAsync<PaginationResult<VoterGridDto>>();
        using var none = await _client.PostAsync("/Api/Voters?PageNumber=1&PageSize=10", JsonContent.Create(new
        {
            districtType = (string?)null,
            district = "",
        }));

        Assert.Multiple(() =>
        {
            Assert.That(named!.Results.Select(v => v.ID), Is.EquivalentTo(new[] { TestData.AdaId, TestData.AdalineId }));
            Assert.That(none.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    private async Task<Guid[]> VoterIds(SearchFilterDto filter)
    {
        using var response = await _client.PostAsJsonAsync("/Api/Voters?PageNumber=1&PageSize=10", filter);
        var result = await response.Content.ReadFromJsonAsync<PaginationResult<VoterGridDto>>();
        return result!.Results.Select(v => v.ID).ToArray();
    }
}
