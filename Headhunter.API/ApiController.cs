using Headhunter.API.Pagination;
using Headhunter.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Headhunter.API;

[ApiController]
[Route("[controller]/[action]")]
public class ApiController : ControllerBase
{
    private readonly ILogger<ApiController> _logger;
    private readonly HeadhunterContext _context;

    public ApiController(ILogger<ApiController> logger, HeadhunterContext context)
    {
        _logger = logger;
        _context = context;
    }

    [HttpGet(Name = "Get")]
    public async Task<IActionResult> Get(decimal west, decimal east, decimal north, decimal south, CancellationToken ct)
    {
        var query = _context.Addresses
            .Where(x => x.Latitude <= north && x.Latitude >= south);

        if (west > east) // If we cross International Date Line, adjust logic
        {
            query = query.Where(x => x.Longitude <= east || x.Longitude >= west);
        }
        else
        {
            query = query.Where(x => x.Longitude <= east && x.Longitude >= west);
        }

        var address = await query
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
                }).ToArray()
            })
            .OrderBy(a => a.Id)
            .Take(1000)
            .AsSplitQuery()
            .ToArrayAsync(ct);

        return Ok(address);
    }

    [HttpPost(Name = "Voters")]
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

        var final = x.OrderBy(x => x.ID).Select(x => new VoterGridDto(x.ID, x.FIRST_NAME, x.LAST_NAME));

        return await final.PaginationResult(filter, ct);
    }

    private (DateTime Start, DateTime End) GetAstrologySignDates(string astrologySign)
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

    public BeforeAfter GetAstrologySignsRelativeToToday(string sign)
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
    public string City { get; set; } = "";
    public int? BirthYear { get; set; }
    public int? Age { get; set; }
    public string Astrology { get; set; } = "";
}

public record VoterGridDto(Guid ID, string FirstName, string LastName);

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

public enum BeforeAfter
{
    Before,
    After,
    Unsure,
}