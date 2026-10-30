using System.Linq.Expressions;
using Headhunter.API.Pagination;
using Headhunter.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;

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
                        Id = v.ID,
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
                Id = x.ID,
                Gender = x.GENDER,
                Latitude = x.Address.Latitude,
                Longitude = x.Address.Longitude,
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
                Id = x.ID,
                Gender = x.GENDER,
                Latitude = x.Address.Latitude,
                Longitude = x.Address.Longitude,
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

    // Other voters with the same last name who live on the same street in the same city, or within a few km
    // as the crow flies, excluding this voter's own household. Closest first, capped.
    [ProducesResponseType(typeof(RelativeDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("Voter/{id}/Relatives")]
    public async Task<IActionResult> Relatives([FromRoute] Guid id, [FromServices] IOptions<RelativesOptions> options, CancellationToken ct)
    {
        var o = options.Value;
        var self = await _context.Voters
            .Where(v => v.ID == id)
            .Select(v => new
            {
                v.LAST_NAME,
                v.AddressID,
                City = v.Address.CITY,
                StreetName = v.Address.STREET_NAME,
                Latitude = v.Address.Latitude,
                Longitude = v.Address.Longitude,
            })
            .FirstOrDefaultAsync(ct);
        if (self is null)
            return NotFound();

        // A flat box, not a true circle - same approximation Area uses. Good enough at ~2 km, and it lets the
        // query below seek IX_Addresses_Latitude_Longitude instead of computing a real distance for every row.
        var hasLocation = self.Latitude is not null && self.Longitude is not null;
        double lat0 = 0, lon0 = 0, cosLat0 = 1;
        decimal north = 0, south = 0, east = 0, west = 0;
        if (hasLocation)
        {
            lat0 = (double)self.Latitude!.Value;
            lon0 = (double)self.Longitude!.Value;
            // A degree of longitude shrinks toward the poles; clamped so a near-pole address (none in this
            // data, but cheap to guard) can't blow the box out to an entire latitude band.
            cosLat0 = Math.Max(Math.Cos(lat0 * Math.PI / 180), 0.01);
            var latSpan = (decimal)(o.RadiusKm / KmPerDegree);
            var lonSpan = (decimal)(o.RadiusKm / (KmPerDegree * cosLat0));
            north = self.Latitude.Value + latSpan;
            south = self.Latitude.Value - latSpan;
            east = self.Longitude.Value + lonSpan;
            west = self.Longitude.Value - lonSpan;
        }

        // Two queries, each with a single AND-only predicate on Addresses, unioned in memory - rather than one
        // query ORing "nearby" against "same street" on a joined table, which would force SQL Server to settle
        // on one plan for both halves. Split like this (same idea as StreetSuggestions' two unioned queries
        // above), the box half seeks IX_Addresses_Latitude_Longitude and the street half seeks the existing
        // CITY or STREET_NAME index (residual-filtered on the other column - both are bounded: a city's or a
        // street name's address count, not the whole table), and only then filters by last name, so a common
        // surname can't turn into a scan of every Voters row with that name - the candidate set is bounded by
        // geography first, not by how common the name is.
        var byBox = hasLocation
            ? await _context.Addresses
                .Where(a => a.Latitude >= south && a.Latitude <= north && a.Longitude >= west && a.Longitude <= east)
                .SelectMany(a => a.Voters.Where(v => v.LAST_NAME == self.LAST_NAME && v.AddressID != self.AddressID))
                .Select(RelativeRowProjection)
                .Take(o.MaxCandidatesPerBranch)
                .ToArrayAsync(ct)
            : [];

        var byStreet = await _context.Addresses
            .Where(a => a.CITY == self.City && a.STREET_NAME == self.StreetName)
            .SelectMany(a => a.Voters.Where(v => v.LAST_NAME == self.LAST_NAME && v.AddressID != self.AddressID))
            .Select(RelativeRowProjection)
            .Take(o.MaxCandidatesPerBranch)
            .ToArrayAsync(ct);

        var relatives = byBox.Concat(byStreet)
            .GroupBy(r => r.Id) // a match can satisfy both branches
            .Select(g => g.First())
            .Select(r => new RelativeDto
            {
                Id = r.Id,
                FirstName = r.FirstName,
                MiddleName = r.MiddleName,
                LastName = r.LastName,
                BirthYear = r.BirthYear,
                Gender = r.Gender,
                AddressId = r.AddressId,
                FullStreetAddress = r.FullStreetAddress,
                City = r.City,
                State = r.State,
                Zip = r.Zip,
                Latitude = r.Latitude,
                Longitude = r.Longitude,
                DistanceKm = hasLocation && r.Latitude is not null && r.Longitude is not null
                    ? DistanceKm(lat0, lon0, cosLat0, (double)r.Latitude.Value, (double)r.Longitude.Value)
                    : null,
            })
            .OrderBy(r => r.DistanceKm ?? double.MaxValue)
            .ThenBy(r => r.FullStreetAddress)
            .Take(o.MaxResults)
            .ToArray();

        return Ok(relatives);
    }

    // Equirectangular approximation (flat, like the box above): plenty accurate at a few km, much cheaper than
    // a real haversine, and this data (Michigan) is never anywhere that approximation would start to matter.
    private const double KmPerDegree = 111.32;

    private static double DistanceKm(double lat0, double lon0, double cosLat0, double lat, double lon)
    {
        var dLat = (lat - lat0) * KmPerDegree;
        var dLon = (lon - lon0) * KmPerDegree * cosLat0;
        return Math.Sqrt(dLat * dLat + dLon * dLon);
    }

    private static readonly Expression<Func<Voter, RelativeRow>> RelativeRowProjection = v => new RelativeRow
    {
        Id = v.ID,
        FirstName = v.FIRST_NAME,
        MiddleName = v.MIDDLE_NAME,
        LastName = v.LAST_NAME,
        BirthYear = v.YEAR_OF_BIRTH,
        Gender = v.GENDER,
        AddressId = v.AddressID,
        FullStreetAddress = v.Address.FullStreetAddress,
        City = v.Address.CITY,
        State = v.Address.STATE,
        Zip = v.Address.ZIP_CODE,
        Latitude = v.Address.Latitude,
        Longitude = v.Address.Longitude,
    };

    // "Walk the street": the previous and next house on the same block (same street name/type/direction/
    // city/zip), ordered numerically by STREET_NUMBER, plus the nearest households by straight-line distance.
    [ProducesResponseType(typeof(WalkDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("Address/{id}/Walk")]
    public async Task<IActionResult> Walk([FromRoute] Guid id, bool sameSideOnly,
        [FromServices] IOptions<WalkOptions> options, CancellationToken ct)
    {
        var current = await _context.Addresses
            .Where(a => a.ID == id)
            .Select(a => new
            {
                a.STREET_NUMBER,
                a.STREET_NAME,
                a.STREET_TYPE,
                a.DIRECTION_PREFIX,
                a.DIRECTION_SUFFIX,
                a.CITY,
                a.ZIP_CODE,
                a.Latitude,
                a.Longitude,
            })
            .FirstOrDefaultAsync(ct);
        if (current is null)
            return NotFound();

        // Same block: the STREET_NAME index seeks straight to this street. A single street, even state-wide
        // under a common name, is small enough to sort by house number in memory.
        var sameStreet = await _context.Addresses
            .Where(a => a.STREET_NAME == current.STREET_NAME
                && a.STREET_TYPE == current.STREET_TYPE
                && a.DIRECTION_PREFIX == current.DIRECTION_PREFIX
                && a.DIRECTION_SUFFIX == current.DIRECTION_SUFFIX
                && a.CITY == current.CITY
                && a.ZIP_CODE == current.ZIP_CODE)
            .Select(a => new { a.ID, a.STREET_NUMBER, a.FullStreetAddress })
            .ToArrayAsync(ct);

        var currentNumber = NumericStreetNumber(current.STREET_NUMBER);
        var ordered = sameStreet
            .Select(a => new { a.ID, a.FullStreetAddress, Number = NumericStreetNumber(a.STREET_NUMBER) })
            // Same side of the street by default (odd/even house numbers), toggled off to see both sides.
            .Where(a => !sameSideOnly || a.Number % 2 == currentNumber % 2)
            .OrderBy(a => a.Number)
            .ThenBy(a => a.ID) // Stable tie-break: duplicate numbers happen (units, apartments).
            .ToArray();

        var index = Array.FindIndex(ordered, a => a.ID == id);
        var previous = index > 0 ? new WalkNeighborDto { Id = ordered[index - 1].ID, FullStreetAddress = ordered[index - 1].FullStreetAddress } : null;
        var next = index >= 0 && index < ordered.Length - 1
            ? new WalkNeighborDto { Id = ordered[index + 1].ID, FullStreetAddress = ordered[index + 1].FullStreetAddress }
            : null;

        var o = options.Value;
        var nearby = current.Latitude is not null && current.Longitude is not null
            ? await Nearby(id, current.Latitude.Value, current.Longitude.Value, o, ct)
            : [];

        return Ok(new WalkDto { Previous = previous, Next = next, Nearby = nearby });
    }

    // Expands a box around the point (seeking IX_Addresses_Latitude_Longitude, same approach as Area) until it
    // holds enough candidates, then ranks them by actual distance. Only ever re-queries in near-empty areas.
    private async Task<NearbyAddressDto[]> Nearby(Guid id, decimal latitude, decimal longitude, WalkOptions o, CancellationToken ct)
    {
        var degrees = o.InitialDegrees;
        while (true)
        {
            var north = latitude + (decimal)degrees;
            var south = latitude - (decimal)degrees;
            var west = longitude - (decimal)degrees;
            var east = longitude + (decimal)degrees;

            var box = await _context.Addresses
                .Where(a => a.ID != id && a.Latitude <= north && a.Latitude >= south && a.Longitude <= east && a.Longitude >= west)
                .Select(a => new
                {
                    a.ID,
                    a.FullStreetAddress,
                    a.Latitude,
                    a.Longitude,
                    VoterCount = a.Voters.Count,
                })
                .ToArrayAsync(ct);

            if (box.Length >= o.NearbyCount || degrees >= o.MaxDegrees)
            {
                return box
                    .Select(a => new NearbyAddressDto
                    {
                        Id = a.ID,
                        FullStreetAddress = a.FullStreetAddress,
                        VoterCount = a.VoterCount,
                        DistanceMeters = HaversineMeters((double)latitude, (double)longitude, (double)a.Latitude!, (double)a.Longitude!),
                    })
                    .OrderBy(a => a.DistanceMeters)
                    .Take(o.NearbyCount)
                    .ToArray();
            }
            degrees *= 4;
        }
    }

    // STREET_NUMBER is text in the source data ("12", "12A", occasionally non-numeric); only the leading
    // digits are the house number, so a blank or non-numeric value sorts as 0 rather than throwing.
    private static int NumericStreetNumber(string streetNumber)
    {
        var digits = new string(streetNumber.TakeWhile(char.IsDigit).ToArray());
        return digits.Length > 0 ? int.Parse(digits) : 0;
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6371000;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMeters * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    // The voters list's filters, shared by the page (Voters) and its download (VotersCsv).
    private IQueryable<Voter> Search(SearchFilterDto? dto)
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

            // A box drawn on the globe, date-line safe like Area: west > east means it crosses it.
            if (dto.West is { } west && dto.East is { } east && dto.North is { } north && dto.South is { } south)
            {
                x = x.Where(x => x.Address.Latitude <= north && x.Address.Latitude >= south);
                x = west > east
                    ? x.Where(x => x.Address.Longitude <= east || x.Address.Longitude >= west)
                    : x.Where(x => x.Address.Longitude <= east && x.Address.Longitude >= west);
            }

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

        return x;
    }

    [HttpPost("Voters")]
    public async Task<PaginationResult<VoterGridDto>> Voters([FromQuery] PaginationFilter filter, [FromBody] SearchFilterDto? dto, CancellationToken ct)
    {
        var final = Search(dto).OrderBy(x => x.ID).Select(x => new VoterGridDto()
        {
            ID = x.ID,
            FirstName = x.FIRST_NAME,
            MiddleName = x.MIDDLE_NAME,
            LastName = x.LAST_NAME,
            BirthYear = x.YEAR_OF_BIRTH,
            Gender = x.GENDER,
            AddressId = x.AddressID,
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

    // The voters list as a file: every page of the current search, written row by row as SQL Server reads them,
    // so a big area never sits in memory. A GET, so the browser downloads it itself. At most Export:MaxRows rows.
    [HttpGet("VotersCsv")]
    public async Task VotersCsv([FromQuery] SearchFilterDto dto, [FromServices] IOptions<ExportOptions> options, CancellationToken ct)
    {
        var rows = Search(dto)
            .OrderBy(x => x.ID)
            .Take(options.Value.MaxRows)
            .Select(x => new
            {
                x.FIRST_NAME,
                x.MIDDLE_NAME,
                x.LAST_NAME,
                x.YEAR_OF_BIRTH,
                x.GENDER,
                x.Address.FullStreetAddress,
                x.Address.CITY,
                x.Address.ZIP_CODE,
                x.REGISTRATION_DATE,
            })
            .AsAsyncEnumerable();

        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition = "attachment; filename=\"voters.csv\"";
        await using var writer = new StreamWriter(Response.Body, new UTF8Encoding(false), bufferSize: 64 * 1024);
        await writer.WriteLineAsync(Csv.Row("Name", "Birth Year", "Gender", "Address", "City", "Zip", "Registration Date"));
        await foreach (var v in rows.WithCancellation(ct))
        {
            var name = string.Join(' ', new[] { v.FIRST_NAME, v.MIDDLE_NAME, v.LAST_NAME }.Where(n => !string.IsNullOrEmpty(n)));
            await writer.WriteLineAsync(Csv.Row(name, v.YEAR_OF_BIRTH.ToString(CultureInfo.InvariantCulture), v.GENDER,
                v.FullStreetAddress, v.CITY, v.ZIP_CODE, v.REGISTRATION_DATE.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }
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
    // The area drawn on the globe, in degrees; ignored unless all four are set. West > east crosses the date line.
    public decimal? West { get; set; }
    public decimal? East { get; set; }
    public decimal? North { get; set; }
    public decimal? South { get; set; }
}

public class VoterGridDto
{
    public required Guid ID { get; set; }
    public required string FirstName { get; set; }
    public required string MiddleName { get; set; }
    public required string LastName { get; set; }
    public required int BirthYear { get; set; }
    public required string Gender { get; set; }
    public required Guid AddressId { get; set; }
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

/// <summary>The voters list's CSV download (configuration section "Export").</summary>
public class ExportOptions
{
    /// <summary>The most rows one download writes.</summary>
    public int MaxRows { get; set; } = 50_000;
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
    public required Guid Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
}

/// <summary>"Walk the street": the neighbouring houses and the nearest ones by distance.</summary>
public class WalkDto
{
    public required WalkNeighborDto? Previous { get; set; }
    public required WalkNeighborDto? Next { get; set; }
    public required NearbyAddressDto[] Nearby { get; set; }
}

public class WalkNeighborDto
{
    public required Guid Id { get; set; }
    public required string FullStreetAddress { get; set; }
}

public class NearbyAddressDto
{
    public required Guid Id { get; set; }
    public required string FullStreetAddress { get; set; }
    public required int VoterCount { get; set; }
    public required double DistanceMeters { get; set; }
}

/// <summary>The walk endpoint's nearby search (configuration section "Walk").</summary>
public class WalkOptions
{
    /// <summary>How many nearby households to return.</summary>
    public int NearbyCount { get; set; } = 8;
    /// <summary>Starting box half-size in degrees (~300 m of latitude). Widens by 4x until it has enough.</summary>
    public double InitialDegrees { get; set; } = 0.003;
    /// <summary>Gives up widening the box here (~55 km), returning whatever it found.</summary>
    public double MaxDegrees { get; set; } = 0.5;
}

public class VoterDetailDto
{
    public required Guid Id { get; set; }
    public required string Gender { get; set; }
    public required decimal? Latitude { get; set; }
    public required decimal? Longitude { get; set; }
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

/// <summary>One row of ApiController.Relatives, before it's ranked into a RelativeDto.</summary>
public class RelativeRow
{
    public required Guid Id { get; set; }
    public required string FirstName { get; set; }
    public required string MiddleName { get; set; }
    public required string LastName { get; set; }
    public required int BirthYear { get; set; }
    public required string Gender { get; set; }
    public required Guid AddressId { get; set; }
    public required string FullStreetAddress { get; set; }
    public required string City { get; set; }
    public required string State { get; set; }
    public required string Zip { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public class RelativeDto
{
    public required Guid Id { get; set; }
    public required string FirstName { get; set; }
    public required string MiddleName { get; set; }
    public required string LastName { get; set; }
    public required int BirthYear { get; set; }
    public required string Gender { get; set; }
    public required Guid AddressId { get; set; }
    public required string FullStreetAddress { get; set; }
    public required string City { get; set; }
    public required string State { get; set; }
    public required string Zip { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    /// <summary>Straight-line km from the voter's own address. Null if either address isn't geocoded.</summary>
    public double? DistanceKm { get; set; }
}

/// <summary>ApiController.Relatives (configuration section "Relatives").</summary>
public class RelativesOptions
{
    /// <summary>Box half-width around the voter's address in km ("within ~2 km", not by road).</summary>
    public double RadiusKm { get; set; } = 2.0;
    /// <summary>Safety cap on each branch before ranking, so a huge street or a packed city block can't blow up the response.</summary>
    public int MaxCandidatesPerBranch { get; set; } = 500;
    /// <summary>How many relatives to return, closest first.</summary>
    public int MaxResults { get; set; } = 20;
}

public enum BeforeAfter
{
    Before,
    After,
    Unsure,
}