using System.Text;
using Headhunter.Database;

namespace Headhunter.Tests;

/// <summary>
/// A fake voter file in the shape of Michigan's real one (all fields quoted, upper case, ISO registration dates,
/// 9-digit voter numbers, Y/N flags, mostly empty mailing and village columns, apartments, directions, multi-word
/// surnames), but every person, street and number is made up. The same seed gives the same file. Households are
/// unique by street address, so <see cref="Households"/> is the number of rows the import's address step must make.
/// </summary>
public sealed class FakeVoterFile
{
    private sealed record Place(string County, string CountyCode, string City, string Zip, string JurisdictionCode,
        string House, string Senate, string Congress, string Commissioner, string School, string SchoolCode);

    private static readonly Place[] Places =
    [
        new("WAYNE", "82", "DETROIT", "48201", "22000", "10", "02", "13", "03", "DETROIT CITY SCHOOL DISTRICT", "82010"),
        new("WAYNE", "82", "LIVONIA", "48152", "22570", "19", "06", "11", "09", "LIVONIA PUBLIC SCHOOLS", "82110"),
        new("OAKLAND", "63", "ROYAL OAK", "48067", "63760", "27", "09", "11", "11", "ROYAL OAK SCHOOL DISTRICT", "63150"),
        new("OAKLAND", "63", "TROY", "48084", "63860", "41", "13", "11", "05", "TROY SCHOOL DISTRICT", "63170"),
        new("MACOMB", "50", "STERLING HEIGHTS", "48310", "50780", "25", "10", "10", "02", "UTICA COMMUNITY SCHOOLS", "50150"),
        new("KENT", "41", "GRAND RAPIDS", "49503", "41340", "75", "29", "03", "07", "GRAND RAPIDS PUBLIC SCHOOLS", "41010"),
        new("WASHTENAW", "81", "ANN ARBOR", "48104", "81010", "55", "15", "06", "04", "ANN ARBOR PUBLIC SCHOOLS", "81010"),
        new("INGHAM", "33", "LANSING", "48912", "33520", "73", "21", "07", "06", "LANSING PUBLIC SCHOOL DISTRICT", "33100"),
        new("KALAMAZOO", "39", "PORTAGE", "49002", "39620", "42", "20", "05", "01", "PORTAGE PUBLIC SCHOOLS", "39100"),
        new("MARQUETTE", "52", "MARQUETTE", "49855", "52510", "109", "38", "01", "03", "MARQUETTE AREA PUBLIC SCHOOLS", "52010"),
    ];

    private static readonly string[] LastNames =
    [
        "SMITH", "JOHNSON", "WILLIAMS", "BROWN", "JONES", "MILLER", "DAVIS", "WILSON", "ANDERSON", "TAYLOR",
        "KOWALSKI", "NOWAK", "SCHMIDT", "MULLER", "O'BRIEN", "MURPHY", "VAN DYKE", "DE JONG", "NGUYEN", "PATEL",
        "WASHINGTON", "JACKSON", "GARCIA", "MARTINEZ", "SMITH-JONES", "LAFLEUR", "BEAUDOIN", "HOFFMAN", "PETERSON", "KARAM",
    ];

    private static readonly string[] FirstNames =
    [
        "JAMES", "MARY", "ROBERT", "PATRICIA", "JOHN", "JENNIFER", "MICHAEL", "LINDA", "DAVID", "ELIZABETH",
        "WILLIAM", "BARBARA", "RICHARD", "SUSAN", "JOSEPH", "JESSICA", "THOMAS", "SARAH", "CHARLES", "KAREN",
        "DANIEL", "NANCY", "MATTHEW", "LISA", "ANTHONY", "BETTY", "MARK", "MARGARET", "DONALD", "SANDRA",
        "ZAINAB", "OMAR", "MAI", "DIEGO", "ADAEZE", "SOPHIA", "NOAH", "LIAM", "OLIVIA", "AVA",
    ];

    private static readonly string[] Middles = ["A", "B", "L", "M", "R", "ANN", "LEE", "MARIE", "JAMES", "J", "E", "D", "LOUISE"];
    private static readonly string[] Suffixes = ["JR", "SR", "II", "III"];
    private static readonly string[] Streets =
    [
        "MAIN", "OAK", "MAPLE", "CEDAR", "ELM", "WASHINGTON", "LAKE", "HILL", "PARK", "WALNUT", "PINE", "CHURCH",
        "MICHIGAN", "WOODWARD", "GRAND RIVER", "JEFFERSON", "MOUND", "MIDDLEBELT", "FORD", "5 MILE", "12 MILE", "NORTHLAND",
    ];
    private static readonly string[] StreetTypes = ["ST", "AVE", "RD", "DR", "LN", "CT", "BLVD", "PKWY", "WAY", "CIR"];
    private static readonly string[] Directions = ["N", "S", "E", "W", "NE", "NW", "SE", "SW"];

    public string Csv { get; }
    public IReadOnlyList<Raw> Records { get; }
    public int Voters => Records.Count;
    public int Households { get; }
    public int PermAvBallotVoters => Records.Count(r => r.IS_PERM_AV_BALLOT_VOTER);

    private FakeVoterFile(IReadOnlyList<Raw> records, int households)
    {
        Records = records;
        Households = households;
        Csv = Serialize(records);
    }

    public static string FullStreetAddress(Raw r) => string.Join(' ', new[]
    {
        r.STREET_NUMBER_PREFIX, r.STREET_NUMBER, r.STREET_NUMBER_SUFFIX, r.DIRECTION_PREFIX, r.STREET_NAME,
        r.STREET_TYPE, r.DIRECTION_SUFFIX, r.EXTENSION,
    }.Where(p => p.Length > 0));

    public static FakeVoterFile Create(int voters, int seed = 1)
    {
        var random = new Random(seed);
        var records = new List<Raw>(voters);
        var ids = new HashSet<long>();
        var households = 0;
        var nextNumber = 100;

        while (records.Count < voters)
        {
            var place = Places[random.Next(Places.Length)];
            var streetName = Streets[random.Next(Streets.Length)];
            var streetType = StreetTypes[random.Next(StreetTypes.Length)];
            var directionPrefix = random.Next(10) < 2 ? Directions[random.Next(4)] : "";
            // A building (apartments, many households on one number) or a single house.
            var apartments = random.Next(12) == 0 ? random.Next(3, 9) : 1;
            var number = (nextNumber += random.Next(1, 40)).ToString();

            for (var unit = 1; unit <= apartments && records.Count < voters; unit++)
            {
                households++;
                var surname = LastNames[random.Next(LastNames.Length)];
                var size = new[] { 1, 1, 2, 2, 2, 3, 4 }[random.Next(7)];
                var precinct = random.Next(1, 40).ToString();
                for (var member = 0; member < size && records.Count < voters; member++)
                {
                    var born = random.Next(1930, 2007);
                    var registeredFrom = Math.Max(born + 18, 1970);
                    var registered = new DateTime(random.Next(registeredFrom, 2025), random.Next(1, 13), random.Next(1, 29));
                    long id;
                    do id = random.NextInt64(100_000_000, 999_999_999); while (!ids.Add(id));
                    var mailing = random.Next(25) == 0;

                    records.Add(new Raw
                    {
                        LAST_NAME = member == 0 || random.Next(8) > 0 ? surname : LastNames[random.Next(LastNames.Length)],
                        FIRST_NAME = FirstNames[random.Next(FirstNames.Length)],
                        MIDDLE_NAME = random.Next(10) < 7 ? Middles[random.Next(Middles.Length)] : "",
                        NAME_SUFFIX = random.Next(25) == 0 ? Suffixes[random.Next(Suffixes.Length)] : "",
                        YEAR_OF_BIRTH = born,
                        GENDER = random.Next(2) == 0 ? "F" : "M",
                        REGISTRATION_DATE = registered,
                        STREET_NUMBER_PREFIX = "",
                        STREET_NUMBER = number,
                        STREET_NUMBER_SUFFIX = random.Next(40) == 0 ? "1/2" : "",
                        DIRECTION_PREFIX = directionPrefix,
                        STREET_NAME = streetName,
                        STREET_TYPE = streetType,
                        DIRECTION_SUFFIX = "",
                        EXTENSION = apartments > 1 ? $"APT {unit}" : "",
                        CITY = place.City,
                        STATE = "MI",
                        ZIP_CODE = place.Zip,
                        MAILING_ADDRESS_LINE_ONE = mailing ? $"PO BOX {random.Next(1, 9999)}" : "",
                        MAILING_ADDRESS_LINE_TWO = mailing ? $"{place.City}, MI {place.Zip}" : "",
                        MAILING_ADDRESS_LINE_THREE = "",
                        MAILING_ADDRESS_LINE_FOUR = "",
                        MAILING_ADDRESS_LINE_FIVE = "",
                        VOTER_IDENTIFICATION_NUMBER = id.ToString(),
                        COUNTY_CODE = place.CountyCode,
                        COUNTY_NAME = place.County,
                        JURISDICTION_CODE = place.JurisdictionCode,
                        JURISDICTION_NAME = $"CITY OF {place.City}",
                        PRECINCT = precinct,
                        WARD = place.County == "WAYNE" ? (1 + int.Parse(precinct) % 7).ToString() : "",
                        SCHOOL_DISTRICT_CODE = place.SchoolCode,
                        SCHOOL_DISTRICT_NAME = place.School,
                        STATE_HOUSE_DISTRICT_CODE = place.House,
                        STATE_HOUSE_DISTRICT_NAME = $"{Ordinal(place.House)} DISTRICT",
                        STATE_SENATE_DISTRICT_CODE = place.Senate,
                        STATE_SENATE_DISTRICT_NAME = $"{Ordinal(place.Senate)} DISTRICT",
                        US_CONGRESS_DISTRICT_CODE = place.Congress,
                        US_CONGRESS_DISTRICT_NAME = $"{Ordinal(place.Congress)} DISTRICT",
                        COUNTY_COMMISSIONER_DISTRICT_CODE = place.Commissioner,
                        COUNTY_COMMISSIONER_DISTRICT_NAME = $"{Ordinal(place.Commissioner)} DISTRICT",
                        VILLAGE_DISTRICT_CODE = "",
                        VILLAGE_DISTRICT_NAME = "",
                        VILLAGE_PRECINCT = "",
                        SCHOOL_PRECINCT = "",
                        IS_PERM_AV_BALLOT_VOTER = random.Next(6) == 0,
                        VOTER_STATUS_TYPE_CODE = "A",
                        UOCAVA_STATUS_CODE = "N",
                        UOCAVA_STATUS_NAME = random.Next(200) == 0 ? "MILITARY" : "NON-UOCAVA",
                        IS_PERM_AV_APP_VOTER = random.Next(8) == 0,
                    });
                }
            }
        }

        return new FakeVoterFile(records, households);
    }

    private static string Ordinal(string number)
    {
        var n = int.Parse(number);
        var suffix = n % 100 is >= 11 and <= 13 ? "TH" : (n % 10) switch { 1 => "ST", 2 => "ND", 3 => "RD", _ => "TH" };
        return $"{n}{suffix}";
    }

    // The header and every field quoted, one line per voter, CRLF: Raw's own column order, as the real file's.
    private static string Serialize(IReadOnlyList<Raw> records)
    {
        var columns = typeof(Raw).GetProperties();
        var csv = new StringBuilder();
        csv.Append(string.Join(",", columns.Select(c => $"\"{c.Name}\""))).Append("\r\n");
        foreach (var record in records)
        {
            csv.Append(string.Join(",", columns.Select(c => c.GetValue(record) switch
            {
                bool flag => flag ? "\"Y\"" : "\"N\"",
                DateTime date => $"\"{date:yyyy-MM-dd}\"",
                var value => $"\"{value}\"",
            }))).Append("\r\n");
        }
        return csv.ToString();
    }
}
