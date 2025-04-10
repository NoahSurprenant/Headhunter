using Bogus;
using CountryData;
using Headhunter.Database;

namespace Headhunter.Seeder;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _scopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // This project is only for seeding fake data

        var michiganData = CountryLoader.LoadUnitedStatesLocationData()
            .States.FirstOrDefault(state => state.Name == "Michigan")
            ?? throw new Exception("Failed to load Michigan data");

        var generator = new Faker<Address>()
            .CustomInstantiator(f =>
            {
                var county = f.PickRandom(michiganData.Provinces.ToArray());
                var community = f.PickRandom(county.Communities.ToArray());
                var cityPostCode = f.PickRandom(community.Places.ToArray());
                var offset1 = f.Random.Decimal(-0.029M, 0.029M); // Offset for 2 miles (positive or negative)
                var offset2 = f.Random.Decimal(-0.029M, 0.029M);

                var address = new Address
                {
                    ID = Guid.NewGuid(),
                    CITY = cityPostCode.Name!,
                    STATE = michiganData.Code!,
                    ZIP_CODE = cityPostCode.PostCode,
                    COUNTY_CODE = county.Code!,
                    COUNTY_NAME = county.Name!,
                    Latitude = (decimal)cityPostCode.Location.Latitude + offset1,
                    Longitude = (decimal)cityPostCode.Location.Longitude + offset2,
                    Matched = true,
                    STREET_NUMBER_PREFIX = "",
                    STREET_NUMBER = "",
                    STREET_NUMBER_SUFFIX = "",
                    DIRECTION_PREFIX = "",
                    STREET_NAME = "",
                    STREET_TYPE = "",
                    DIRECTION_SUFFIX = "",
                    JURISDICTION_CODE = "",
                    JURISDICTION_NAME = "",
                    PRECINCT = "",
                    WARD = "",
                    SCHOOL_DISTRICT_CODE = "",
                    SCHOOL_DISTRICT_NAME = "",
                    STATE_HOUSE_DISTRICT_CODE = "",
                    STATE_HOUSE_DISTRICT_NAME = "",
                    STATE_SENATE_DISTRICT_CODE = "",
                    STATE_SENATE_DISTRICT_NAME = "",
                    US_CONGRESS_DISTRICT_CODE = "",
                    US_CONGRESS_DISTRICT_NAME = "",
                    COUNTY_COMMISSIONER_DISTRICT_CODE = "",
                    COUNTY_COMMISSIONER_DISTRICT_NAME = "",
                    VILLAGE_DISTRICT_CODE = "",
                    VILLAGE_DISTRICT_NAME = "",
                    VILLAGE_PRECINCT = "",
                    SCHOOL_PRECINCT = ""
                };
                return address;
            })
            .RuleFor(x => x.STREET_NUMBER, (f, a) => f.Random.Int(1, 9999).ToString())
            .RuleFor(x => x.STREET_NAME, (f, a) => f.Address.StreetName())
            .RuleFor(x => x.STATE, (f, a) => michiganData.Code);

        var addresses = generator.Generate(10000);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HeadhunterContext>();

        db.AddRange(addresses);
        await db.SaveChangesAsync();
    }
}
