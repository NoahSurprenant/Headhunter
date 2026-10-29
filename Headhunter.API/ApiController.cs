using Headhunter.API.Pagination;
using Headhunter.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Headhunter.API;

[ApiController]
[Route("[controller]")]
public class ApiController : ControllerBase
{
    private readonly ILogger<ApiController> _logger;
    private readonly HeadhunterContext _context;

    public ApiController(ILogger<ApiController> logger, HeadhunterContext context)
    {
        _logger = logger;
        _context = context;
    }

    [HttpGet("FirstNameSuggestions")]
    public async Task<string[]> FirstNameSuggestions(string query, CancellationToken ct)
    {
        return await _context.Voters
            .Where(x => x.FIRST_NAME.StartsWith(query))
            .Select(x => x.FIRST_NAME)
            .Distinct()
            .OrderBy(x => x)
            .Take(50)
            .ToArrayAsync(ct);
    }

    [HttpGet("MiddleNameSuggestions")]
    public async Task<string[]> MiddleNameSuggestions(string query, CancellationToken ct)
    {
        return await _context.Voters
            .Where(x => x.MIDDLE_NAME.StartsWith(query))
            .Select(x => x.MIDDLE_NAME)
            .Distinct()
            .OrderBy(x => x)
            .Take(50)
            .ToArrayAsync(ct);
    }

    [HttpGet("LastNameSuggestions")]
    public async Task<string[]> LastNameSuggestions(string query, CancellationToken ct)
    {
        return await _context.Voters
            .Where(x => x.LAST_NAME.StartsWith(query))
            .Select(x => x.LAST_NAME)
            .Distinct()
            .OrderBy(x => x)
            .Take(50)
            .ToArrayAsync(ct);
    }

    [HttpGet("CitySuggestions")]
    public async Task<string[]> CitySuggestions(string query, CancellationToken ct)
    {
        return await _context.Addresses
            .Where(x => x.CITY.StartsWith(query))
            .Select(x => x.CITY)
            .Distinct()
            .OrderBy(x => x)
            .Take(50)
            .ToArrayAsync(ct);
    }

    // Addresses that start with the query ("2078 W WYN") or whose street name does ("WYNDHAM"): two prefix
    // searches, each an index seek, rather than LIKE '%query%', which reads all 4M addresses.
    [HttpGet("StreetSuggestions")]
    public async Task<string[]> StreetSuggestions(string query, CancellationToken ct)
    {
        var byAddress = _context.Addresses
            .Where(x => x.FullStreetAddress.StartsWith(query))
            .Select(x => x.FullStreetAddress);
        var byStreetName = _context.Addresses
            .Where(x => x.STREET_NAME.StartsWith(query))
            .Select(x => x.FullStreetAddress);

        return await byAddress
            .Union(byStreetName)
            .OrderBy(x => x)
            .Take(50)
            .ToArrayAsync(ct);
    }

    // The globe's view: every address inside the box when there are few enough to draw (AreaOptions.MaxAddresses),
    // else the box split into a grid with each cell's address count and centre, so a zoomed-out view shows where
    // people are instead of an arbitrary sample. Unsorted on purpose: sorting the matches (it used to be by ID) made
    // SQL Server walk all 4M addresses in that order instead of seeking IX_Addresses_Latitude_Longitude.
    [HttpGet("Area")]
    public async Task<AreaDto> Area(decimal west, decimal east, decimal north, decimal south,
        [FromServices] IOptions<AreaOptions> options, [FromServices] IMemoryCache cache, CancellationToken ct)
    {
        var o = options.Value;
        var inBox = _context.Addresses.Where(x => x.Latitude <= north && x.Latitude >= south);
        inBox = west > east // The box crosses the date line.
            ? inBox.Where(x => x.Longitude <= east || x.Longitude >= west)
            : inBox.Where(x => x.Longitude <= east && x.Longitude >= west);

        // Over the limit? One more than it, unsorted, stops as soon as it has them.
        var overLimit = await inBox.Take(o.MaxAddresses + 1).CountAsync(ct) > o.MaxAddresses;
        if (!overLimit)
        {
            var addresses = await inBox
                .Select(x => new AddressDto()
                {
                    Id = x.ID,
                    StreetNumber = x.STREET_NUMBER,
                    StreetName = x.STREET_NAME,
                    City = x.CITY,
                    State = x.STATE,
                    ZipCode = x.ZIP_CODE,
                    Latitude = (decimal)x.Latitude!,
                    Longitude = (decimal)x.Longitude!,
                    Voters = x.Voters.Select(v => new VoterDto()
                    {
                        FirstName = v.FIRST_NAME,
                        LastName = v.LAST_NAME
                    }).ToArray(),
                })
                .AsSplitQuery()
                .ToArrayAsync(ct);
            return new AreaDto { Addresses = addresses, Clusters = [] };
        }

        // The same view asks again on every pan back to it, and the data only changes on import: the whole state
        // takes seconds to group (every address), so grids are kept for a while.
        var key = (west, east, north, south, o.GridCells);
        var clusters = await cache.GetOrCreateAsync(key, entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = o.ClusterCacheDuration;
            return Clusters(inBox, (double)west, (double)east, (double)north, (double)south, o.GridCells, ct);
        });
        return new AreaDto { Addresses = [], Clusters = clusters! };
    }

    private static async Task<ClusterDto[]> Clusters(IQueryable<Address> inBox, double west, double east, double north, double south,
        int gridCells, CancellationToken ct)
    {
        // Across the date line the box's width wraps: longitudes west of its west edge are counted past 180.
        var width = west > east ? east - west + 360 : east - west;
        var cellWidth = width / gridCells;
        var cellHeight = (north - south) / gridCells;

        var cells = await inBox
            .Select(x => new
            {
                Latitude = (double)x.Latitude!,
                // Degrees east of the box's west edge, so a cell's centre averages correctly across the date line.
                X = (double)x.Longitude! >= west ? (double)x.Longitude! - west : (double)x.Longitude! + 360 - west,
            })
            .GroupBy(x => new
            {
                Column = Math.Floor(x.X / cellWidth),
                Row = Math.Floor((x.Latitude - south) / cellHeight),
            })
            .Select(g => new
            {
                Latitude = g.Average(x => x.Latitude),
                X = g.Average(x => x.X),
                Count = g.Count(),
            })
            .ToArrayAsync(ct);

        return cells
            .Select(c => new ClusterDto
            {
                Latitude = c.Latitude,
                Longitude = west + c.X > 180 ? west + c.X - 360 : west + c.X,
                Count = c.Count,
            })
            .ToArray();
    }

    [ProducesResponseType(typeof(VoterDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("Voter/{id}")]
    public async Task<IActionResult> Voter([FromRoute] Guid id, CancellationToken ct)
    {
        var voter = await _context.Voters
            .Where(v => v.ID == id)
            .Select(x => new VoterDetailDto()
            {
                FirstName = x.FIRST_NAME,
                MiddleName = x.MIDDLE_NAME,
                LastName = x.LAST_NAME,
                BirthYear = x.YEAR_OF_BIRTH,
                RegistrationDate = x.REGISTRATION_DATE,
                AddressId = x.Address.ID,
                FullStreetAddress = x.Address.FullStreetAddress,
                City = x.Address.CITY,
                State = x.Address.STATE,
                Zip = x.Address.ZIP_CODE
            })
            .FirstOrDefaultAsync(ct);
        if (voter is null)
            return NotFound();
        return Ok(voter);
    }

    [ProducesResponseType(typeof(VoterDetailDto[]), StatusCodes.Status200OK)]
    //[ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("Address/{id}")]
    public async Task<IActionResult> Address([FromRoute] Guid id, CancellationToken ct)
    {
        var voters = await _context.Voters
            .Where(v => v.AddressID == id)
            .Select(x => new VoterDetailDto()
            {
                FirstName = x.FIRST_NAME,
                MiddleName = x.MIDDLE_NAME,
                LastName = x.LAST_NAME,
                BirthYear = x.YEAR_OF_BIRTH,
                RegistrationDate = x.REGISTRATION_DATE,
                AddressId = x.Address.ID,
                FullStreetAddress = x.Address.FullStreetAddress,
                City = x.Address.CITY,
                State = x.Address.STATE,
                Zip = x.Address.ZIP_CODE
            })
            .ToArrayAsync(ct);
        //if (voter is null)
        //    return NotFound();
        return Ok(voters);
    }

    [HttpPost("Voters")]
    public async Task<PaginationResult<VoterGridDto>> Voters([FromQuery] PaginationFilter filter, [FromBody] SearchFilterDto? dto, CancellationToken ct)
    {
        var x = _context.Voters.AsQueryable();

        if (dto is not null)
        {
            if (dto.FirstName != string.Empty)
                x = x.Where(x => x.FIRST_NAME == dto.FirstName);
            if (dto.MiddleName != string.Empty)
                x = x.Where(x => x.MIDDLE_NAME == dto.MiddleName);
            if (dto.LastName != string.Empty)
                x = x.Where(x => x.LAST_NAME == dto.LastName);

            if (dto.Street != string.Empty)
            {
                // This is too much, query times out, better to just have a column that appends all these values
                //x = x.Where(x =>(
                //                    (x.Address.STREET_NUMBER_PREFIX != "" ? " " + x.Address.STREET_NUMBER_PREFIX : "") +
                //                    (x.Address.STREET_NUMBER != "" ? " " + x.Address.STREET_NUMBER : "") +
                //                    (x.Address.STREET_NUMBER_SUFFIX != "" ? " " + x.Address.STREET_NUMBER_SUFFIX : "") +
                //                    (x.Address.DIRECTION_PREFIX != "" ? " " + x.Address.DIRECTION_PREFIX : "") +
                //                    (x.Address.STREET_NAME != "" ? " " + x.Address.STREET_NAME : "") +
                //                    (x.Address.STREET_TYPE != "" ? " " + x.Address.STREET_TYPE : "") +
                //                    (x.Address.DIRECTION_SUFFIX != "" ? " " + x.Address.DIRECTION_SUFFIX : "") +
                //                    (x.EXTENSION != "" ? " " + x.EXTENSION : "")
                //                ).Contains(dto.Street));
                x = x.Where(x => x.Address.FullStreetAddress == dto.Street);
            }

            if (dto.City != string.Empty)
                x = x.Where(x => x.Address.CITY == dto.City);
            if (dto.BirthYear is not null)
                x = x.Where(x => x.YEAR_OF_BIRTH == dto.BirthYear);

            if (dto.Age is not null)
            {
                var now = DateTime.Now;
                var year = now.Year;

                var maxBirthYear = year - dto.Age;
                var minBirthYear = maxBirthYear - 1;

                // If today is before the birthday, then its maxBirthYear
                // else if after then its minBirthYear

                if (dto.Astrology != string.Empty)
                {
                    x = GetAstrologySignsRelativeToToday(dto.Astrology) switch
                    {
                        BeforeAfter.Before => x.Where(x => x.YEAR_OF_BIRTH == maxBirthYear),
                        BeforeAfter.After => x.Where(x => x.YEAR_OF_BIRTH == minBirthYear),
                        BeforeAfter.Unsure => x.Where(x => x.YEAR_OF_BIRTH == minBirthYear || x.YEAR_OF_BIRTH == maxBirthYear),
                        _ => throw new ArgumentOutOfRangeException(nameof(dto.Astrology), "Must be before, after, or unsure"),
                    };
                }
                else
                {
                    x = x.Where(x => x.YEAR_OF_BIRTH == minBirthYear || x.YEAR_OF_BIRTH == maxBirthYear);
                }
            }
        }

        var final = x.OrderBy(x => x.ID).Select(x => new VoterGridDto()
        {
            ID = x.ID,
            FirstName = x.FIRST_NAME,
            MiddleName = x.MIDDLE_NAME,
            LastName = x.LAST_NAME,
            BirthYear = x.YEAR_OF_BIRTH,
            Gender = x.GENDER,
            StreetNumberPrefix = x.Address.STREET_NUMBER_PREFIX,
            StreetNumber = x.Address.STREET_NUMBER,
            StreetNumberSuffix = x.Address.STREET_NUMBER_SUFFIX,
            DirectionPrefix = x.Address.DIRECTION_PREFIX,
            StreetName = x.Address.STREET_NAME,
            StreetType = x.Address.STREET_TYPE,
            DirectionSuffix = x.Address.DIRECTION_SUFFIX,
            Extension = x.Address.EXTENSION,
            City = x.Address.CITY,
            State = x.Address.STATE,
            ZipCode = x.Address.ZIP_CODE,
            Latitude = x.Address.Latitude,
            Longitude = x.Address.Longitude,
        });

        return await final.PaginationResult(filter, ct);
    }

    private static (DateTime Start, DateTime End) GetAstrologySignDates(string astrologySign)
    {
        var zodiacSigns = new Dictionary<string, (DateTime Start, DateTime End)>
        {
            { "Capricorn", (new DateTime(DateTime.Now.Year - 1, 12, 22), new DateTime(DateTime.Now.Year, 1, 19)) },
            { "Aquarius", (new DateTime(DateTime.Now.Year, 1, 20), new DateTime(DateTime.Now.Year, 2, 18)) },
            { "Pisces", (new DateTime(DateTime.Now.Year, 2, 19), new DateTime(DateTime.Now.Year, 3, 20)) },
            { "Aries", (new DateTime(DateTime.Now.Year, 3, 21), new DateTime(DateTime.Now.Year, 4, 19)) },
            { "Taurus", (new DateTime(DateTime.Now.Year, 4, 20), new DateTime(DateTime.Now.Year, 5, 20)) },
            { "Gemini", (new DateTime(DateTime.Now.Year, 5, 21), new DateTime(DateTime.Now.Year, 6, 20)) },
            { "Cancer", (new DateTime(DateTime.Now.Year, 6, 21), new DateTime(DateTime.Now.Year, 7, 22)) },
            { "Leo", (new DateTime(DateTime.Now.Year, 7, 23), new DateTime(DateTime.Now.Year, 8, 22)) },
            { "Virgo", (new DateTime(DateTime.Now.Year, 8, 23), new DateTime(DateTime.Now.Year, 9, 22)) },
            { "Libra", (new DateTime(DateTime.Now.Year, 9, 23), new DateTime(DateTime.Now.Year, 10, 22)) },
            { "Scorpio", (new DateTime(DateTime.Now.Year, 10, 23), new DateTime(DateTime.Now.Year, 11, 21)) },
            { "Sagittarius", (new DateTime(DateTime.Now.Year, 11, 22), new DateTime(DateTime.Now.Year, 12, 21)) }
        };

        var (Start, End) = zodiacSigns[astrologySign];

        return (Start, End);
    }

    private BeforeAfter GetAstrologySignsRelativeToToday(string sign)
    {
        // Impossible to tell if Capricorn is before or after since it wraps the year
        if (sign is "Capricorn")
            return BeforeAfter.Unsure;

        var dates = GetAstrologySignDates(sign);

        var today = DateTime.Now;

        if (dates.End < today)
        {
            return BeforeAfter.Before;
        }
        else if (dates.Start > today)
        {
            return BeforeAfter.After;
        }
        else
        {
            return BeforeAfter.Unsure;
        }
    }

}

public class SearchFilterDto
{
    public string FirstName { get; set; } = "";
    public string MiddleName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public int? BirthYear { get; set; }
    public int? Age { get; set; }
    public string Astrology { get; set; } = "";
}

public class VoterGridDto
{
    public required Guid ID { get; set; }
    public required string FirstName { get; set; }
    public required string MiddleName { get; set; }
    public required string LastName { get; set; }
    public required int BirthYear { get; set; }
    public required string Gender { get; set; }
    public required string StreetNumberPrefix { get; set; }
    public required string StreetNumber { get; set; }
    public required string StreetNumberSuffix { get; set; }
    public required string DirectionPrefix { get; set; }
    public required string StreetName { get; set; }
    public required string StreetType { get; set; }
    public required string DirectionSuffix { get; set; }
    public required string Extension { get; set; }
    public required string City { get; set; }
    public required string State { get; set; }
    public required string ZipCode { get; set; }
    public required decimal? Latitude { get; set; }
    public required decimal? Longitude { get; set; }
}

/// <summary>What the globe draws for a box: its addresses, or, when there are too many, clusters. One is empty.</summary>
public class AreaDto
{
    public required AddressDto[] Addresses { get; set; }
    public required ClusterDto[] Clusters { get; set; }
}

/// <summary>One grid cell of a zoomed-out view: how many addresses, and their average position.</summary>
public class ClusterDto
{
    public required double Latitude { get; set; }
    public required double Longitude { get; set; }
    public required int Count { get; set; }
}

/// <summary>The globe's area queries (configuration section "Area").</summary>
public class AreaOptions
{
    /// <summary>Above this many addresses in the box, clusters instead.</summary>
    public int MaxAddresses { get; set; } = 1000;
    /// <summary>The cluster grid: this many cells across and down the box. 20 leaves the globe's bubbles room.</summary>
    public int GridCells { get; set; } = 20;
    public TimeSpan ClusterCacheDuration { get; set; } = TimeSpan.FromHours(1);
}

public class AddressDto
{
    public required Guid Id { get; set; }
    public required string StreetNumber { get; set; }
    public required string StreetName { get; set; }
    public required string City { get; set; }
    public required string State { get; set; }
    public required string ZipCode { get; set; }
    public required decimal Latitude { get; set; }
    public required decimal Longitude { get; set; }
    public required VoterDto[] Voters { get; set; }
}

public class VoterDto
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
}

public class VoterDetailDto
{
    public required string FirstName { get; set; }
    public required string MiddleName { get; set; }
    public required string LastName { get; set; }
    public required int BirthYear { get; set; }
    public required DateTime RegistrationDate { get; set; }
    public required Guid AddressId { get; set; }
    public required string FullStreetAddress { get; set; }
    public required string City { get; set; }
    public required string State { get; set; }
    public required string Zip { get; set; }
}

public enum BeforeAfter
{
    Before,
    After,
    Unsure,
}