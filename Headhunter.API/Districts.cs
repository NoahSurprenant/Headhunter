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
            DistrictType.County => addresses.Where(a => a.COUNTY_NAME == name),
            DistrictType.Jurisdiction => addresses.Where(a => a.JURISDICTION_NAME == name),
            DistrictType.Precinct => addresses.Where(a => a.JURISDICTION_NAME == within && a.PRECINCT == name),
            DistrictType.Ward => addresses.Where(a => a.JURISDICTION_NAME == within && a.WARD == name),
            DistrictType.SchoolDistrict => addresses.Where(a => a.SCHOOL_DISTRICT_NAME == name),
            DistrictType.StateHouse => addresses.Where(a => a.STATE_HOUSE_DISTRICT_NAME == name),
            DistrictType.StateSenate => addresses.Where(a => a.STATE_SENATE_DISTRICT_NAME == name),
            DistrictType.UsCongress => addresses.Where(a => a.US_CONGRESS_DISTRICT_NAME == name),
            DistrictType.CountyCommissioner => addresses.Where(a => a.COUNTY_NAME == within && a.COUNTY_COMMISSIONER_DISTRICT_NAME == name),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

    /// <summary>Every address's name for its district of this type (one row per address, not distinct).</summary>
    public static IQueryable<string> Names(this IQueryable<Address> addresses, DistrictType type, string within) =>
        type switch
        {
            DistrictType.County => addresses.Select(a => a.COUNTY_NAME),
            DistrictType.Jurisdiction => addresses.Select(a => a.JURISDICTION_NAME),
            DistrictType.Precinct => addresses.Where(a => a.JURISDICTION_NAME == within).Select(a => a.PRECINCT),
            DistrictType.Ward => addresses.Where(a => a.JURISDICTION_NAME == within).Select(a => a.WARD),
            DistrictType.SchoolDistrict => addresses.Select(a => a.SCHOOL_DISTRICT_NAME),
            DistrictType.StateHouse => addresses.Select(a => a.STATE_HOUSE_DISTRICT_NAME),
            DistrictType.StateSenate => addresses.Select(a => a.STATE_SENATE_DISTRICT_NAME),
            DistrictType.UsCongress => addresses.Select(a => a.US_CONGRESS_DISTRICT_NAME),
            DistrictType.CountyCommissioner => addresses.Where(a => a.COUNTY_NAME == within).Select(a => a.COUNTY_COMMISSIONER_DISTRICT_NAME),
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

        return await _context.Addresses
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

        var count = await voters.CountAsync(ct);
        if (count == 0)
            return null;

        var households = await voters.Select(v => v.AddressID).Distinct().CountAsync(ct);
        var permanentAbsentee = await voters.CountAsync(v => v.IS_PERM_AV_BALLOT_VOTER, ct);
        // A hundred-odd birth years, banded here rather than in SQL.
        var birthYears = await voters
            .GroupBy(v => v.YEAR_OF_BIRTH)
            .Select(g => new { Year = g.Key, Count = g.Count() })
            .ToArrayAsync(ct);
        var genders = await voters
            .GroupBy(v => v.GENDER)
            .Select(g => new GenderCountDto { Gender = g.Key, Count = g.Count() })
            .ToArrayAsync(ct);
        var newest = await voters
            .OrderByDescending(v => v.REGISTRATION_DATE)
            .ThenBy(v => v.ID)
            .Take(5)
            .Select(v => new RegistrationDto
            {
                Id = v.ID,
                FirstName = v.FIRST_NAME,
                MiddleName = v.MIDDLE_NAME,
                LastName = v.LAST_NAME,
                RegistrationDate = v.REGISTRATION_DATE,
            })
            .ToArrayAsync(ct);

        var thisYear = DateTime.Now.Year;
        var ages = new int[AgeBands.Length];
        foreach (var b in birthYears)
        {
            // The last band the age reaches; under the first band's start (a bad birth year) counts in the first.
            var age = thisYear - b.Year;
            ages[Math.Max(0, Array.FindLastIndex(AgeBands, band => age >= band.From))] += b.Count;
        }

        return new DistrictStatsDto
        {
            Type = type,
            Name = name,
            Within = within,
            Voters = count,
            Households = households,
            Ages = AgeBands.Select((band, i) => new AgeBandDto { Label = band.Label, Count = ages[i] }).ToArray(),
            Genders = genders.OrderByDescending(g => g.Count).ThenBy(g => g.Gender).ToArray(),
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
