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