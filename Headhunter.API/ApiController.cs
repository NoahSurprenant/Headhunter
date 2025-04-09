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
        var address = await _context.Addresses
            .Where(x => x.Latitude <= north && x.Latitude >= south)
            .Where(x => x.Longitude <= east && x.Longitude >= west)
            .Select(x => new AddressDto()
            {
                ID = x.ID,
                STREET_NUMBER = x.STREET_NUMBER,
                STREET_NAME = x.STREET_NAME,
                CITY = x.CITY,
                STATE = x.STATE,
                ZIP_CODE = x.ZIP_CODE,
                Latitude = (decimal)x.Latitude!,
                Longitude = (decimal)x.Longitude!,
            })
            .OrderBy(a => a.ID)
            .Take(1000)
            .ToListAsync(ct);

        return Ok(address);
    }
}

public class AddressDto
{
    public required Guid ID { get; set; }
    public required string STREET_NUMBER { get; set; }
    public required string STREET_NAME { get; set; }
    public required string CITY { get; set; }
    public required string STATE { get; set; }
    public required string ZIP_CODE { get; set; }
    public required decimal Latitude { get; set; }
    public required decimal Longitude { get; set; }
}
