namespace Headhunter.Database;

public class County
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public class Jurisdiction
{
    public string CountyCode { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public County County { get; set; } = null!;
}

public class SchoolDistrict
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public class StateHouseDistrict
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public class StateSenateDistrict
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public class UsCongressDistrict
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>Numbered within a county ("01" in every county that has one), so it is its county and its code.</summary>
public class CountyCommissionerDistrict
{
    public string CountyCode { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public County County { get; set; } = null!;
}

/// <summary>An election day. Description is empty for a date the election codes file doesn't list.</summary>
public class Election
{
    public DateOnly Date { get; set; }
    public int? CodeId { get; set; }
    public string Description { get; set; } = "";
}

/// <summary>
/// That a voter voted in an election. The jurisdiction and school district are the voter's current ones, repeated on
/// every row by the state's file (identical to their address in a 610k-row sample), not where they voted then.
/// </summary>
public class VoteHistory
{
    public Guid VoterID { get; set; }
    public DateOnly ElectionDate { get; set; }
    public string? CountyCode { get; set; }
    public string? JurisdictionCode { get; set; }
    public string? SchoolDistrictCode { get; set; }
    public Voter Voter { get; set; } = null!;
    public Election Election { get; set; } = null!;
    public Jurisdiction? Jurisdiction { get; set; }
    public SchoolDistrict? SchoolDistrict { get; set; }
}
