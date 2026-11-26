namespace Headhunter.Database;
public class Address
{
    public Guid ID { get; set; }
    public string STREET_NUMBER_PREFIX { get; set; }
    public string STREET_NUMBER { get; set; }
    public string STREET_NUMBER_SUFFIX { get; set; }
    public string DIRECTION_PREFIX { get; set; }
    public string STREET_NAME { get; set; }
    public string STREET_TYPE { get; set; }
    public string DIRECTION_SUFFIX { get; set; }
    public string EXTENSION { get; set; }
    public string FullStreetAddress { get; set; }
    public string CITY { get; set; }
    public string STATE { get; set; }
    public string ZIP_CODE { get; set; }
    public string? COUNTY_CODE { get; set; }
    public string? JURISDICTION_CODE { get; set; }
    public string PRECINCT { get; set; }
    public string WARD { get; set; }
    public string? SCHOOL_DISTRICT_CODE { get; set; }
    public string? STATE_HOUSE_DISTRICT_CODE { get; set; }
    public string? STATE_SENATE_DISTRICT_CODE { get; set; }
    public string? US_CONGRESS_DISTRICT_CODE { get; set; }
    public string? COUNTY_COMMISSIONER_DISTRICT_CODE { get; set; }
    public string VILLAGE_DISTRICT_CODE { get; set; }
    public string VILLAGE_DISTRICT_NAME { get; set; }
    public string VILLAGE_PRECINCT { get; set; }
    public string SCHOOL_PRECINCT { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    // True if matched by the geocoding service, false if not. NULL if not yet geocoded.
    public bool? Matched { get; set; }

    // The districts' names live in their own tables, by code. A blank code in the voter file is null here.
    public County? County { get; set; }
    public Jurisdiction? Jurisdiction { get; set; }
    public SchoolDistrict? SchoolDistrict { get; set; }
    public StateHouseDistrict? StateHouseDistrict { get; set; }
    public StateSenateDistrict? StateSenateDistrict { get; set; }
    public UsCongressDistrict? UsCongressDistrict { get; set; }
    public CountyCommissionerDistrict? CountyCommissionerDistrict { get; set; }
    public List<Voter> Voters { get; set; } = new();
}
