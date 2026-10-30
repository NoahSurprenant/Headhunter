using System.Linq.Expressions;
using Headhunter.API.Pagination;
using Headhunter.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

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
    // With a district (districtType, district and, for precincts, wards and commissioner districts, districtWithin), only
    // its addresses: the globe highlighting one. With a metric, each cell also carries that figure for its voters
    // (ClusterMetric), which the globe colours it by.
    [HttpGet("Area")]
    public async Task<AreaDto> Area(decimal west, decimal east, decimal north, decimal south,
        [FromServices] IOptions<AreaOptions> options, [FromServices] IMemoryCache cache, CancellationToken ct,
        DistrictType? districtType = null, string district = "", string districtWithin = "",
        ClusterMetric metric = ClusterMetric.None)
    {
        var o = options.Value;
        var source = districtType is { } type && district != ""
            ? _context.Addresses.InDistrict(type, district, districtWithin)
            : _context.Addresses;
        var inBox = source.Where(x => x.Latitude <= north && x.Latitude >= south);
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
        var key = (west, east, north, south, o.GridCells, districtType, district, districtWithin, metric);
        var clusters = await cache.GetOrCreateAsync(key, entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = o.ClusterCacheDuration;
            return metric == ClusterMetric.None
                ? Clusters(inBox, (double)west, (double)east, (double)north, (double)south, o.GridCells, ct)
                : ClustersWithMetric(west, east, north, south, o.GridCells, metric,
                    districtType is { } t && district != "" ? t : null, district, districtWithin, ct);
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

    // The same grid as Clusters, plus a figure over each cell's voters. Raw SQL because the median has no LINQ
    // translation (APPROX_PERCENTILE_CONT, SQL Server 2022+, is an ordinary aggregate, unlike PERCENTILE_CONT). One
    // statement, two GROUP BYs over the same cells: the counts and centres are by address exactly as in Clusters
    // (joining voters would weight them by household), the figure by voter. The expressions come from the enum, never
    // from the request.
    private async Task<ClusterDto[]> ClustersWithMetric(decimal west, decimal east, decimal north, decimal south,
        int gridCells, ClusterMetric metric, DistrictType? districtType, string district, string districtWithin,
        CancellationToken ct)
    {
        var value = metric switch
        {
            // Unknown birth years (0) are left out of the median.
            ClusterMetric.MedianAge => "@year - APPROX_PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY NULLIF(v.YEAR_OF_BIRTH, 0))",
            ClusterMetric.PermanentAbsentee => "AVG(CASE WHEN v.IS_PERM_AV_BALLOT_VOTER = 1 THEN 100.0 ELSE 0 END)",
            ClusterMetric.RecentlyRegistered => "AVG(CASE WHEN v.REGISTRATION_DATE >= @since THEN 100.0 ELSE 0 END)",
            ClusterMetric.Female => "AVG(CASE WHEN v.GENDER = N'F' THEN 100.0 ELSE 0 END)",
            _ => throw new ArgumentOutOfRangeException(nameof(metric)),
        };
        var westEdge = (double)west;
        var width = west > east ? (double)(east - west) + 360 : (double)(east - west);
        var longitude = west > east ? "(Longitude <= @east OR Longitude >= @west)" : "Longitude <= @east AND Longitude >= @west";
        // A district, as SQL over the same Addresses columns InDistrict filters on (the column names come from the enum).
        var inDistrict = districtType is { } type ? " AND " + Districts.InDistrictSql(type) : "";

        var sql = $"""
            WITH a AS (
                SELECT ID, CAST(Latitude AS float) AS Lat,
                    CASE WHEN CAST(Longitude AS float) >= @westEdge THEN CAST(Longitude AS float) - @westEdge
                        ELSE CAST(Longitude AS float) + 360 - @westEdge END AS X
                FROM Addresses
                WHERE Latitude <= @north AND Latitude >= @south AND {longitude}{inDistrict}
            ), c AS (
                SELECT ID, Lat, X, FLOOR(X / @cellWidth) AS Col, FLOOR((Lat - @southEdge) / @cellHeight) AS Row FROM a
            ), cells AS (
                SELECT Col, Row, AVG(Lat) AS Latitude, AVG(X) AS X, COUNT(*) AS Count FROM c GROUP BY Col, Row
            ), metric AS (
                SELECT c.Col, c.Row, CAST({value} AS float) AS Value
                FROM c JOIN Voters v ON v.AddressID = c.ID
                GROUP BY c.Col, c.Row
            )
            SELECT cells.Latitude, cells.X, cells.Count, metric.Value
            FROM cells LEFT JOIN metric ON metric.Col = cells.Col AND metric.Row = cells.Row
            """;
        object[] parameters =
        [
            new SqlParameter("@west", west), new SqlParameter("@east", east),
            new SqlParameter("@north", north), new SqlParameter("@south", south),
            new SqlParameter("@westEdge", westEdge), new SqlParameter("@southEdge", (double)south),
            new SqlParameter("@cellWidth", width / gridCells), new SqlParameter("@cellHeight", (double)(north - south) / gridCells),
            new SqlParameter("@year", DateTime.Now.Year), new SqlParameter("@since", DateTime.Today.AddYears(-1)),
            new SqlParameter("@district", district), new SqlParameter("@districtWithin", districtWithin),
        ];
        // Every voter in the box joins in: Michigan took 8 to 11 s on 8M synthetic voters (1 to 2 s for a city), against
        // 1 to 2 s uncoloured, so past the default 30 s on a slower server. Once per view, then cached like the rest.
        _context.Database.SetCommandTimeout(TimeSpan.FromMinutes(2));
        var cells = await _context.Database.SqlQueryRaw<CellRow>(sql, parameters).ToArrayAsync(ct);

        return cells
            .Select(c => new ClusterDto
            {
                Latitude = c.Latitude,
                Longitude = westEdge + c.X > 180 ? westEdge + c.X - 360 : westEdge + c.X,
                Count = c.Count,
                Value = c.Value,
            })
            .ToArray();
    }

    private class CellRow
    {
        public double Latitude { get; set; }
        public double X { get; set; }
        public int Count { get; set; }
        public double? Value { get; set; }
    }

    [ProducesResponseType(typeof(VoterDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("Voter/{id}")]
    public async Task<IActionResult> Voter([FromRoute] Guid id, CancellationToken ct)
    {
        var voter = await _context.Voters
            .Where(v => v.ID == id)
            .Select(x => new VoterDetailRow()
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
                Zip = x.Address.ZIP_CODE,
                MailingAddressLineOne = x.MAILING_ADDRESS_LINE_ONE,
                MailingAddressLineTwo = x.MAILING_ADDRESS_LINE_TWO,
                MailingAddressLineThree = x.MAILING_ADDRESS_LINE_THREE,
                MailingAddressLineFour = x.MAILING_ADDRESS_LINE_FOUR,
                MailingAddressLineFive = x.MAILING_ADDRESS_LINE_FIVE,
            })
            .FirstOrDefaultAsync(ct);
        if (voter is null)
            return NotFound();
        return Ok(voter.ToDto());
    }

    [ProducesResponseType(typeof(VoterDetailDto[]), StatusCodes.Status200OK)]
    //[ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("Address/{id}")]
    public async Task<IActionResult> Address([FromRoute] Guid id, CancellationToken ct)
    {
        var voters = await _context.Voters
            .Where(v => v.AddressID == id)
            .Select(x => new VoterDetailRow()
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
                Zip = x.Address.ZIP_CODE,
                MailingAddressLineOne = x.MAILING_ADDRESS_LINE_ONE,
                MailingAddressLineTwo = x.MAILING_ADDRESS_LINE_TWO,
                MailingAddressLineThree = x.MAILING_ADDRESS_LINE_THREE,
                MailingAddressLineFour = x.MAILING_ADDRESS_LINE_FOUR,
                MailingAddressLineFive = x.MAILING_ADDRESS_LINE_FIVE,
            })
            .ToArrayAsync(ct);
        //if (voter is null)
        //    return NotFound();
        return Ok(voters.Select(v => v.ToDto()).ToArray());
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

    // "Not on the map": addresses the geocoder hasn't matched (Matched is false, or still null because a batch
    // run hasn't reached them yet), so Latitude/Longitude are null and they can't show on the globe or a map
    // preview. Ordered by ID like Voters, so pages don't overlap as the underlying data changes.
    [HttpGet("NotOnMap")]
    public async Task<PaginationResult<NotOnMapAddressDto>> NotOnMap(
        [FromQuery] PaginationFilter filter, string? city, string? county, CancellationToken ct)
    {
        var query = _context.Addresses.Where(x => x.Matched != true);
        if (!string.IsNullOrEmpty(city))
            query = query.Where(x => x.CITY == city);
        if (!string.IsNullOrEmpty(county))
            query = query.Where(x => x.COUNTY_NAME == county);

        var final = query.OrderBy(x => x.ID).Select(x => new NotOnMapAddressDto
        {
            Id = x.ID,
            FullStreetAddress = x.FullStreetAddress,
            City = x.CITY,
            State = x.STATE,
            ZipCode = x.ZIP_CODE,
            County = x.COUNTY_NAME,
            Matched = x.Matched,
        });

        return await final.PaginationResult(filter, ct);
    }

    // The breakdown behind NotOnMap's county filter: how many unmatched addresses are in each county, so the
    // page can show where the gaps are before anyone picks a filter.
    [HttpGet("NotOnMap/Counties")]
    public async Task<CountyCountDto[]> NotOnMapCounties(CancellationToken ct)
    {
        return await _context.Addresses
            .Where(x => x.Matched != true)
            .GroupBy(x => x.COUNTY_NAME)
            .Select(g => new CountyCountDto { County = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.County)
            .ToArrayAsync(ct);
    }

    // Retries the US Census Bureau's free geocoder for one address: the same service Headhunter.Geocoding's
    // batch run uses, but a single lookup instead of a batch file, so it's cheap enough to trigger from the UI.
    // No API key needed. Overwrites Matched/Latitude/Longitude with whatever this attempt finds.
    // Rate limited (RetryGeocodeRateLimitOptions, configured in Program.cs): the site has no login, so without a
    // cap a free-for-all client could hammer the Census geocoder through us.
    [ProducesResponseType(typeof(RetryGeocodeResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [EnableRateLimiting(RetryGeocodeRateLimitOptions.PolicyName)]
    [HttpPost("Address/{id}/RetryGeocode")]
    public async Task<IActionResult> RetryGeocode([FromRoute] Guid id, [FromServices] CensusGeocoder.GeocodingService geocoder, CancellationToken ct)
    {
        var address = await _context.Addresses.FirstOrDefaultAsync(a => a.ID == id, ct);
        if (address is null)
            return NotFound();

        CensusGeocoder.LocationDto location;
        try
        {
            location = await geocoder.AddressToLocation(address.FullStreetAddress, address.CITY, address.STATE, address.ZIP_CODE, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RetryGeocode {AddressId}: the Census geocoder was unreachable", id);
            return StatusCode(StatusCodes.Status502BadGateway);
        }

        var match = location.addressMatches.FirstOrDefault();
        if (match is null)
        {
            address.Matched = false;
            address.Latitude = null;
            address.Longitude = null;
        }
        else
        {
            address.Matched = true;
            address.Latitude = match.coordinates.y;
            address.Longitude = match.coordinates.x;
        }
        await _context.SaveChangesAsync(ct);

        return Ok(new RetryGeocodeResultDto
        {
            Matched = address.Matched ?? false,
            Latitude = address.Latitude,
            Longitude = address.Longitude,
        });
    }

    // The voters list's filters, shared by the page (Voters) and its download (VotersCsv). mailingAddressDiffers is
    // a separate query parameter (not part of the SearchFilterDto body) so it combines with whatever's already in
    // the search form; it seeks IX_Voters_HAS_MAILING_ADDRESS rather than scanning MAILING_ADDRESS_LINE_ONE (see
    // VoterConfiguration.HAS_MAILING_ADDRESS). The advanced conditions (SearchConditions) are ANDed on; a bad one
    // throws SearchConditionException here, before anything is queried.
    private IQueryable<Voter> Search(SearchFilterDto? dto, bool? mailingAddressDiffers, SearchOptions options)
    {
        var x = _context.Voters.AsQueryable();

        if (mailingAddressDiffers == true)
            x = x.Where(x => x.HAS_MAILING_ADDRESS);

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
            if (dto.DistrictType is { } districtType && dto.District != string.Empty)
            {
                var inDistrict = _context.Addresses.InDistrict(districtType, dto.District, dto.DistrictWithin).Select(a => a.ID);
                x = x.Where(x => inDistrict.Contains(x.AddressID));
            }
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

            if (dto.Conditions is { Count: > 0 } conditions)
                x = SearchConditions.Apply(x, conditions, options.MaxConditions, DateTime.Now);
        }

        return x;
    }

    // What the client shows for a search that ran out of Search:CommandTimeout (a 422).
    public const string TooBroadMessage = "This search is too broad to finish; add a narrower condition (e.g. a city or birth year)";

    // SqlClient's "Execution Timeout Expired".
    private static bool IsTimeout(Exception ex) => ex is SqlException { Number: -2 };

    private ObjectResult TooBroad() => Problem(title: "Search too broad", detail: TooBroadMessage,
        statusCode: StatusCodes.Status422UnprocessableEntity);

    private ObjectResult BadCondition(SearchConditionException ex) => Problem(title: "Invalid search condition", detail: ex.Message,
        statusCode: StatusCodes.Status400BadRequest);

    [HttpPost("Voters")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PaginationResult<VoterGridDto>>> Voters([FromQuery] PaginationFilter filter,
        [FromQuery] bool? mailingAddressDiffers, [FromBody] SearchFilterDto? dto, [FromServices] IOptions<SearchOptions> options,
        CancellationToken ct)
    {
        IQueryable<Voter> search;
        try
        {
            search = Search(dto, mailingAddressDiffers, options.Value);
        }
        catch (SearchConditionException ex)
        {
            return BadCondition(ex);
        }
        _context.Database.SetCommandTimeout(options.Value.CommandTimeout);

        var final = search.OrderBy(x => x.ID).Select(x => new VoterGridRow()
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
            MailingAddressLineOne = x.MAILING_ADDRESS_LINE_ONE,
            MailingAddressLineTwo = x.MAILING_ADDRESS_LINE_TWO,
            MailingAddressLineThree = x.MAILING_ADDRESS_LINE_THREE,
            MailingAddressLineFour = x.MAILING_ADDRESS_LINE_FOUR,
            MailingAddressLineFive = x.MAILING_ADDRESS_LINE_FIVE,
        });

        PaginationResult<VoterGridRow> page;
        try
        {
            page = await final.PaginationResult(filter, ct);
        }
        catch (Exception ex) when (IsTimeout(ex))
        {
            return TooBroad();
        }
        return new PaginationResult<VoterGridDto>
        {
            TotalCount = page.TotalCount,
            Results = page.Results.Select(r => r.ToDto()).ToArray(),
        };
    }

    // The voters list as a file: every page of the current search, written row by row as SQL Server reads them,
    // so a big area never sits in memory. A GET, so the browser downloads it itself. At most Export:MaxRows rows.
    // The advanced conditions come as conditions[0].field=..&conditions[0].operator=..&conditions[0].value=..
    [HttpGet("VotersCsv")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> VotersCsv([FromQuery] SearchFilterDto dto, [FromQuery] bool? mailingAddressDiffers,
        [FromServices] IOptions<ExportOptions> options, [FromServices] IOptions<SearchOptions> searchOptions, CancellationToken ct)
    {
        IQueryable<Voter> search;
        try
        {
            search = Search(dto, mailingAddressDiffers, searchOptions.Value);
        }
        catch (SearchConditionException ex)
        {
            return BadCondition(ex);
        }
        _context.Database.SetCommandTimeout(searchOptions.Value.CommandTimeout);

        var rows = search
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

        // The first row runs the query: a search that times out does so here, before anything is written, so it
        // can still answer 422 rather than a file cut short.
        await using var rowEnumerator = rows.GetAsyncEnumerator(ct);
        bool more;
        try
        {
            more = await rowEnumerator.MoveNextAsync();
        }
        catch (Exception ex) when (IsTimeout(ex))
        {
            return TooBroad();
        }

        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition = "attachment; filename=\"voters.csv\"";
        await using var writer = new StreamWriter(Response.Body, new UTF8Encoding(false), bufferSize: 64 * 1024);
        await writer.WriteLineAsync(Csv.Row("Name", "Birth Year", "Gender", "Address", "City", "Zip", "Registration Date"));
        for (; more; more = await rowEnumerator.MoveNextAsync())
        {
            var v = rowEnumerator.Current;
            var name = string.Join(' ', new[] { v.FIRST_NAME, v.MIDDLE_NAME, v.LAST_NAME }.Where(n => !string.IsNullOrEmpty(n)));
            await writer.WriteLineAsync(Csv.Row(name, v.YEAR_OF_BIRTH.ToString(CultureInfo.InvariantCulture), v.GENDER,
                v.FullStreetAddress, v.CITY, v.ZIP_CODE, v.REGISTRATION_DATE.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }
        return new EmptyResult();
    }

    // The advanced search's voter status choices (VOTER_STATUS_TYPE_CODE): whatever codes the voter file has (the
    // import copies them as they are). Distinct over every voter is a scan, so the list is kept for a day.
    [HttpGet("VoterStatuses")]
    public async Task<string[]> VoterStatuses([FromServices] IMemoryCache cache, CancellationToken ct)
    {
        var statuses = await cache.GetOrCreateAsync("VoterStatuses", entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
            return _context.Voters
                .Select(v => v.VOTER_STATUS_TYPE_CODE)
                .Where(s => s != "")
                .Distinct()
                .OrderBy(s => s)
                .ToArrayAsync(ct);
        });
        return statuses ?? [];
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
    /// <summary>With <see cref="District"/> (and <see cref="DistrictWithin"/> for precincts, wards and commissioner
    /// districts): only voters living in it.</summary>
    public DistrictType? DistrictType { get; set; }
    public string District { get; set; } = "";
    public string DistrictWithin { get; set; } = "";
    /// <summary>The advanced search (SearchConditions): ANDed with the fields above and with each other. Optional;
    /// without it the search is as it was.</summary>
    public List<SearchConditionDto>? Conditions { get; set; }
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
    /// <summary>Mail doesn't go to this voter's residence (MAILING_ADDRESS_LINE_ONE is non-empty).</summary>
    public required bool HasMailingAddress { get; set; }
    /// <summary>Mail goes somewhere outside <see cref="State"/>, best-effort from the mailing address's last line.</summary>
    public required bool MailingAddressOutOfState { get; set; }
}

/// <summary>
/// What <see cref="ApiController.Voters"/> projects from SQL for the grid: the raw mailing address lines, so
/// <see cref="ToDto"/> can turn them into flags in C# (the state/ZIP parsing isn't something LINQ can translate).
/// </summary>
public class VoterGridRow
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
    public required string MailingAddressLineOne { get; set; }
    public required string MailingAddressLineTwo { get; set; }
    public required string MailingAddressLineThree { get; set; }
    public required string MailingAddressLineFour { get; set; }
    public required string MailingAddressLineFive { get; set; }

    public VoterGridDto ToDto()
    {
        var lines = MailingAddress.NonEmptyLines(MailingAddressLineOne, MailingAddressLineTwo, MailingAddressLineThree, MailingAddressLineFour, MailingAddressLineFive);
        return new VoterGridDto
        {
            ID = ID,
            FirstName = FirstName,
            MiddleName = MiddleName,
            LastName = LastName,
            BirthYear = BirthYear,
            Gender = Gender,
            AddressId = AddressId,
            StreetNumberPrefix = StreetNumberPrefix,
            StreetNumber = StreetNumber,
            StreetNumberSuffix = StreetNumberSuffix,
            DirectionPrefix = DirectionPrefix,
            StreetName = StreetName,
            StreetType = StreetType,
            DirectionSuffix = DirectionSuffix,
            Extension = Extension,
            City = City,
            State = State,
            ZipCode = ZipCode,
            Latitude = Latitude,
            Longitude = Longitude,
            HasMailingAddress = lines.Length > 0,
            MailingAddressOutOfState = MailingAddress.IsOutOfState(lines, State),
        };
    }
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
    /// <summary>The asked-for <see cref="ClusterMetric"/> over the cell's voters; absent without one, or with no voters.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Value { get; set; }
}

/// <summary>What the globe can colour clusters by.</summary>
public enum ClusterMetric
{
    None,
    /// <summary>Years, by birth year.</summary>
    MedianAge,
    /// <summary>Percent of voters on the permanent absentee ballot list (IS_PERM_AV_BALLOT_VOTER).</summary>
    PermanentAbsentee,
    /// <summary>Percent of voters registered in the last 12 months (REGISTRATION_DATE).</summary>
    RecentlyRegistered,
    /// <summary>Percent of voters whose GENDER is F.</summary>
    Female,
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
    /// <summary>Mail doesn't go to this voter's residence (MAILING_ADDRESS_LINE_ONE is non-empty).</summary>
    public required bool HasMailingAddress { get; set; }
    /// <summary>Mail goes somewhere outside <see cref="State"/>, best-effort from the mailing address's last line.</summary>
    public required bool MailingAddressOutOfState { get; set; }
    /// <summary>The non-empty MAILING_ADDRESS_LINE_ONE..FIVE, in order. Empty when <see cref="HasMailingAddress"/> is false.</summary>
    public required string[] MailingAddressLines { get; set; }
}

/// <summary>
/// What the Voter and Address actions project from SQL: the raw mailing address lines, so <see cref="ToDto"/> can
/// turn them into flags and a clean line list in C# (the state/ZIP parsing isn't something LINQ can translate).
/// </summary>
public class VoterDetailRow
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
    public required string MailingAddressLineOne { get; set; }
    public required string MailingAddressLineTwo { get; set; }
    public required string MailingAddressLineThree { get; set; }
    public required string MailingAddressLineFour { get; set; }
    public required string MailingAddressLineFive { get; set; }

    public VoterDetailDto ToDto()
    {
        var lines = MailingAddress.NonEmptyLines(MailingAddressLineOne, MailingAddressLineTwo, MailingAddressLineThree, MailingAddressLineFour, MailingAddressLineFive);
        return new VoterDetailDto
        {
            Id = Id,
            Gender = Gender,
            Latitude = Latitude,
            Longitude = Longitude,
            FirstName = FirstName,
            MiddleName = MiddleName,
            LastName = LastName,
            BirthYear = BirthYear,
            RegistrationDate = RegistrationDate,
            AddressId = AddressId,
            FullStreetAddress = FullStreetAddress,
            City = City,
            State = State,
            Zip = Zip,
            MailingAddressLines = lines,
            HasMailingAddress = lines.Length > 0,
            MailingAddressOutOfState = MailingAddress.IsOutOfState(lines, State),
        };
    }
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

/// <summary>One row of the "Not on the map" list: an address the geocoder hasn't placed.</summary>
public class NotOnMapAddressDto
{
    public required Guid Id { get; set; }
    public required string FullStreetAddress { get; set; }
    public required string City { get; set; }
    public required string State { get; set; }
    public required string ZipCode { get; set; }
    public required string County { get; set; }
    public required bool? Matched { get; set; }
}

/// <summary>How many unmatched addresses are in a county, for NotOnMap's per-county breakdown.</summary>
public class CountyCountDto
{
    public required string County { get; set; }
    public required int Count { get; set; }
}

/// <summary>The result of a single-address retry against the Census geocoder.</summary>
public class RetryGeocodeResultDto
{
    public required bool Matched { get; set; }
    public required decimal? Latitude { get; set; }
    public required decimal? Longitude { get; set; }
}

/// <summary>
/// RetryGeocode's rate limit (configuration section "RetryGeocodeRateLimit"), one shared bucket for every
/// caller since there's no login to key a per-caller limit off. Defaults match what was asked for: 10 requests
/// a minute, no queueing (requests over the limit are rejected immediately with 429, not held).
/// </summary>
public class RetryGeocodeRateLimitOptions
{
    public const string PolicyName = "RetryGeocode";

    public int PermitLimit { get; set; } = 10;
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

public enum BeforeAfter
{
    Before,
    After,
    Unsure,
}