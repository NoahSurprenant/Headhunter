using System.Text.Json.Serialization;
using Headhunter.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Headhunter.API;

/// <summary>The districts every address in the voter file is in. Sent and received by name ("SchoolDistrict").</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DistrictType>))]
public enum DistrictType
{
    County,
    Jurisdiction,
    /// <summary>Numbered within its jurisdiction ("00012"), so it needs one.</summary>
    Precinct,
    /// <summary>Numbered within its jurisdiction, so it needs one.</summary>
    Ward,
    SchoolDistrict,
    StateHouse,
    StateSenate,
    UsCongress,
    /// <summary>Numbered within its county, so it needs one.</summary>
    CountyCommissioner,
}

/// <summary>
/// A district's addresses, by its name (the voter file's *_NAME column, or PRECINCT and WARD as they are). Precincts,
/// wards and commissioner districts are only unique within a jurisdiction or county: <c>within</c> names it.
/// </summary>
public static class Districts
{
    /// <summary>Precincts and wards repeat in every jurisdiction, commissioner districts in every county.</summary>
    public static bool NeedsWithin(DistrictType type) =>
        type is DistrictType.Precinct or DistrictType.Ward or DistrictType.CountyCommissioner;

    public static IQueryable<Address> InDistrict(this IQueryable<Address> addresses, DistrictType type, string name, string within) =>
        type switch
        {
            DistrictType.County => addresses.Where(a => a.County!.Name == name),
            DistrictType.Jurisdiction => addresses.Where(a => a.Jurisdiction!.Name == name),
            DistrictType.Precinct => addresses.Where(a => a.Jurisdiction!.Name == within && a.PRECINCT == name),
            DistrictType.Ward => addresses.Where(a => a.Jurisdiction!.Name == within && a.WARD == name),
            DistrictType.SchoolDistrict => addresses.Where(a => a.SchoolDistrict!.Name == name),
            DistrictType.StateHouse => addresses.Where(a => a.StateHouseDistrict!.Name == name),
            DistrictType.StateSenate => addresses.Where(a => a.StateSenateDistrict!.Name == name),
            DistrictType.UsCongress => addresses.Where(a => a.UsCongressDistrict!.Name == name),
            DistrictType.CountyCommissioner => addresses.Where(a => a.County!.Name == within && a.CountyCommissionerDistrict!.Name == name),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

    /// <summary>InDistrict as a SQL predicate over Addresses, for raw SQL (the globe's coloured clusters): the district's
    /// name is @district and, for precincts, wards and commissioner districts, what it is within is @districtWithin.
    /// The names are in the lookup tables, so each is a subquery on the address's codes.</summary>
    public static string InDistrictSql(DistrictType type)
    {
        static string Lookup(string table, string name) =>
            $"\"COUNTY_CODE\" IN (SELECT \"Code\" FROM \"{table}\" WHERE \"Name\" = {name})";
        static string InJurisdiction(string name) =>
            "EXISTS (SELECT 1 FROM \"Jurisdictions\" j WHERE j.\"CountyCode\" = \"Addresses\".\"COUNTY_CODE\" " +
            $"AND j.\"Code\" = \"Addresses\".\"JURISDICTION_CODE\" AND j.\"Name\" = {name})";
        static string Simple(string column, string table) =>
            $"\"{column}\" IN (SELECT \"Code\" FROM \"{table}\" WHERE \"Name\" = @district)";

        return type switch
        {
            DistrictType.County => Lookup("Counties", "@district"),
            DistrictType.Jurisdiction => InJurisdiction("@district"),
            DistrictType.Precinct => $"{InJurisdiction("@districtWithin")} AND \"PRECINCT\" = @district",
            DistrictType.Ward => $"{InJurisdiction("@districtWithin")} AND \"WARD\" = @district",
            DistrictType.SchoolDistrict => Simple("SCHOOL_DISTRICT_CODE", "SchoolDistricts"),
            DistrictType.StateHouse => Simple("STATE_HOUSE_DISTRICT_CODE", "StateHouseDistricts"),
            DistrictType.StateSenate => Simple("STATE_SENATE_DISTRICT_CODE", "StateSenateDistricts"),
            DistrictType.UsCongress => Simple("US_CONGRESS_DISTRICT_CODE", "UsCongressDistricts"),
            DistrictType.CountyCommissioner => Lookup("Counties", "@districtWithin") +
                " AND EXISTS (SELECT 1 FROM \"CountyCommissionerDistricts\" d WHERE d.\"CountyCode\" = \"Addresses\".\"COUNTY_CODE\" " +
                "AND d.\"Code\" = \"Addresses\".\"COUNTY_COMMISSIONER_DISTRICT_CODE\" AND d.\"Name\" = @district)",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

    /// <summary>The names of the districts of this type that have an address, from their lookup table (a small
    /// table, each name checked against the address index on its code); precincts and wards from the addresses.</summary>
    public static IQueryable<string> Names(this HeadhunterContext context, DistrictType type, string within) =>
        type switch
        {
            DistrictType.County => context.Counties
                .Where(c => context.Addresses.Any(a => a.COUNTY_CODE == c.Code)).Select(c => c.Name),
            DistrictType.Jurisdiction => context.Jurisdictions
                .Where(j => context.Addresses.Any(a => a.COUNTY_CODE == j.CountyCode && a.JURISDICTION_CODE == j.Code)).Select(j => j.Name),
            DistrictType.Precinct => context.Addresses.Where(a => a.Jurisdiction!.Name == within).Select(a => a.PRECINCT),
            DistrictType.Ward => context.Addresses.Where(a => a.Jurisdiction!.Name == within).Select(a => a.WARD),
            DistrictType.SchoolDistrict => context.SchoolDistricts
                .Where(d => context.Addresses.Any(a => a.SCHOOL_DISTRICT_CODE == d.Code)).Select(d => d.Name),
            DistrictType.StateHouse => context.StateHouseDistricts
                .Where(d => context.Addresses.Any(a => a.STATE_HOUSE_DISTRICT_CODE == d.Code)).Select(d => d.Name),
            DistrictType.StateSenate => context.StateSenateDistricts
                .Where(d => context.Addresses.Any(a => a.STATE_SENATE_DISTRICT_CODE == d.Code)).Select(d => d.Name),
            DistrictType.UsCongress => context.UsCongressDistricts
                .Where(d => context.Addresses.Any(a => a.US_CONGRESS_DISTRICT_CODE == d.Code)).Select(d => d.Name),
            DistrictType.CountyCommissioner => context.CountyCommissionerDistricts
                .Where(d => d.County.Name == within)
                .Where(d => context.Addresses.Any(a => a.COUNTY_CODE == d.CountyCode && a.COUNTY_COMMISSIONER_DISTRICT_CODE == d.Code))
                .Select(d => d.Name),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
}

/// <summary>District suggestions, stats and extents (under Api/, like <see cref="ApiController"/>).</summary>
[ApiController]
[Route("Api")]
public class DistrictController : ControllerBase
{
    // The voter file only changes on import, and a county's stats read every voter in it: keep them for a while.
    public static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    // The age chart's bars: by approximate age (this year minus the birth year), as the voter pages show it.
    private static readonly (string Label, int From)[] AgeBands =
        [("Under 25", 0), ("25–34", 25), ("35–44", 35), ("45–54", 45), ("55–64", 55), ("65–74", 65), ("75+", 75)];

    private readonly HeadhunterContext _context;
    private readonly IMemoryCache _cache;

    public DistrictController(HeadhunterContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    /// <summary>The districts of a type whose name starts with the query; precincts, wards and commissioner districts
    /// of the jurisdiction or county given as <paramref name="within"/>.</summary>
    [HttpGet("DistrictSuggestions")]
    public async Task<string[]> DistrictSuggestions(DistrictType type, string query = "", string within = "", CancellationToken ct = default)
    {
        if (Districts.NeedsWithin(type) && within == "")
            return [];

        return await _context
            .Names(type, within)
            .Where(x => x != "" && x.StartsWith(query))
            .Distinct()
            .OrderBy(x => x)
            .Take(50)
            .ToArrayAsync(ct);
    }

    [ProducesResponseType(typeof(DistrictStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("District")]
    public async Task<IActionResult> District(DistrictType type, string name, string within = "", CancellationToken ct = default)
    {
        if (Districts.NeedsWithin(type) && within == "")
            return BadRequest($"A {type} needs the jurisdiction or county it is in (within).");

        var stats = await _cache.GetOrCreateAsync(("district", type, name, within), entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return Stats(type, name, within, ct);
        });
        return stats is null ? NotFound() : Ok(stats);
    }

    /// <summary>The box around a district's placed addresses, for the globe to fly to.</summary>
    [ProducesResponseType(typeof(DistrictExtentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("DistrictExtent")]
    public async Task<IActionResult> DistrictExtent(DistrictType type, string name, string within = "", CancellationToken ct = default)
    {
        var extent = await _cache.GetOrCreateAsync(("district-extent", type, name, within), entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return Extent(_context.Addresses.InDistrict(type, name, within), ct);
        });
        return extent is null ? NotFound() : Ok(extent);
    }

    private async Task<DistrictStatsDto?> Stats(DistrictType type, string name, string within, CancellationToken ct)
    {
        var addresses = _context.Addresses.InDistrict(type, name, within);
        var voters = addresses.SelectMany(a => a.Voters);

        // Count, permanent-absentee count, birth years and genders all come out of one grouped query (a hundred-odd
        // birth years crossed with a handful of genders, so the group count stays small): one pass over the join
        // instead of four, aggregated in SQL and only banded into ages and summed by gender here.
        var groups = await voters
            .GroupBy(v => new { v.YEAR_OF_BIRTH, v.GENDER })
            .Select(g => new
            {
                g.Key.YEAR_OF_BIRTH,
                g.Key.GENDER,
                Count = g.Count(),
                PermanentAbsentee = g.Sum(v => v.IS_PERM_AV_BALLOT_VOTER ? 1 : 0),
            })
            .ToArrayAsync(ct);
        if (groups.Length == 0)
            return null;

        var count = groups.Sum(g => g.Count);
        var permanentAbsentee = groups.Sum(g => g.PermanentAbsentee);

        var thisYear = DateTime.Now.Year;
        var ages = new int[AgeBands.Length];
        foreach (var g in groups)
        {
            // The last band the age reaches; under the first band's start (a bad birth year) counts in the first.
            var age = thisYear - g.YEAR_OF_BIRTH;
            ages[Math.Max(0, Array.FindLastIndex(AgeBands, band => age >= band.From))] += g.Count;
        }
        var genders = groups
            .GroupBy(g => g.GENDER)
            .Select(g => new GenderCountDto { Gender = g.Key, Count = g.Sum(x => x.Count) })
            .OrderByDescending(g => g.Count).ThenBy(g => g.Gender)
            .ToArray();

        // Households: the district's addresses with at least one voter, not a DISTINCT over every voter.
        var households = await addresses.CountAsync(a => a.Voters.Any(), ct);

        // Newest registrations in two steps: the five IDs from IX_Voters_AddressID alone (it includes
        // REGISTRATION_DATE), then their names. Asking for the names in the same query looked them up for every
        // voter in the district before sorting: 15-20 s and ~390k reads for a county.
        var newestIds = await voters
            .OrderByDescending(v => v.REGISTRATION_DATE)
            .ThenBy(v => v.ID)
            .Take(5)
            .Select(v => v.ID)
            .ToArrayAsync(ct);
        var newest = await _context.Voters
            .Where(v => newestIds.Contains(v.ID))
            .OrderByDescending(v => v.REGISTRATION_DATE)
            .ThenBy(v => v.ID)
            .Select(v => new RegistrationDto
            {
                Id = v.ID,
                FirstName = v.FIRST_NAME,
                MiddleName = v.MIDDLE_NAME,
                LastName = v.LAST_NAME,
                RegistrationDate = v.REGISTRATION_DATE,
            })
            .ToArrayAsync(ct);

        return new DistrictStatsDto
        {
            Type = type,
            Name = name,
            Within = within,
            Voters = count,
            Households = households,
            Ages = AgeBands.Select((band, i) => new AgeBandDto { Label = band.Label, Count = ages[i] }).ToArray(),
            Genders = genders,
            PermanentAbsenteePercent = Math.Round(100.0 * permanentAbsentee / count, 1),
            NewestRegistrations = newest,
            Extent = await Extent(addresses, ct),
        };
    }

    private static async Task<DistrictExtentDto?> Extent(IQueryable<Address> addresses, CancellationToken ct) =>
        await addresses
            .Where(a => a.Latitude != null && a.Longitude != null)
            .GroupBy(a => 1)
            .Select(g => new DistrictExtentDto
            {
                West = (decimal)g.Min(a => a.Longitude)!,
                East = (decimal)g.Max(a => a.Longitude)!,
                South = (decimal)g.Min(a => a.Latitude)!,
                North = (decimal)g.Max(a => a.Latitude)!,
            })
            .FirstOrDefaultAsync(ct);
}

/// <summary>Who lives in a district: the voters page's district card.</summary>
public class DistrictStatsDto
{
    public required DistrictType Type { get; set; }
    public required string Name { get; set; }
    public required string Within { get; set; }
    public required int Voters { get; set; }
    /// <summary>Addresses with at least one voter.</summary>
    public required int Households { get; set; }
    public required AgeBandDto[] Ages { get; set; }
    /// <summary>Most first.</summary>
    public required GenderCountDto[] Genders { get; set; }
    public required double PermanentAbsenteePercent { get; set; }
    /// <summary>The five most recently registered.</summary>
    public required RegistrationDto[] NewestRegistrations { get; set; }
    /// <summary>Null when none of its addresses has been placed on the map.</summary>
    public required DistrictExtentDto? Extent { get; set; }
}

public class AgeBandDto
{
    public required string Label { get; set; }
    public required int Count { get; set; }
}

public class GenderCountDto
{
    public required string Gender { get; set; }
    public required int Count { get; set; }
}

public class RegistrationDto
{
    public required Guid Id { get; set; }
    public required string FirstName { get; set; }
    public required string MiddleName { get; set; }
    public required string LastName { get; set; }
    public required DateTime RegistrationDate { get; set; }
}

public class DistrictExtentDto
{
    public required decimal West { get; set; }
    public required decimal East { get; set; }
    public required decimal South { get; set; }
    public required decimal North { get; set; }
}
