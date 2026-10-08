using Headhunter.Database;

namespace Headhunter.Tests;

/// <summary>
/// Small synthetic data set. Every name and address is made up; never put real voter data here.
/// </summary>
public static class TestData
{
    public static readonly int ThisYear = DateTime.Now.Year;

    public static readonly Guid MapleAddressId = Guid.Parse("00000000-0000-0000-0000-00000000a001");
    public static readonly Guid OakAddressId = Guid.Parse("00000000-0000-0000-0000-00000000a002");
    public static readonly Guid DatelineEastAddressId = Guid.Parse("00000000-0000-0000-0000-00000000a003");
    public static readonly Guid DatelineWestAddressId = Guid.Parse("00000000-0000-0000-0000-00000000a004");

    public static readonly Guid AdaId = Guid.Parse("00000000-0000-0000-0000-00000000b001");
    public static readonly Guid AdalineId = Guid.Parse("00000000-0000-0000-0000-00000000b002");
    public static readonly Guid AdaOakId = Guid.Parse("00000000-0000-0000-0000-00000000b003");
    public static readonly Guid BrunoId = Guid.Parse("00000000-0000-0000-0000-00000000b004");
    public static readonly Guid CleoId = Guid.Parse("00000000-0000-0000-0000-00000000b005");

    public static async Task SeedAsync(HeadhunterContext context)
    {
        var maple = NewAddress(MapleAddressId, "100", "Maple", "St", "Testville", 42.500000000001m, -83.500000000001m);
        var oak = NewAddress(OakAddressId, "200", "Oak", "Ave", "Testburg", 43.0m, -84.0m);
        var datelineEast = NewAddress(DatelineEastAddressId, "1", "Meridian", "Rd", "Eastedge", 10.0m, 179.5m);
        var datelineWest = NewAddress(DatelineWestAddressId, "2", "Meridian", "Rd", "Westedge", 10.0m, -179.5m);

        context.Addresses.AddRange(maple, oak, datelineEast, datelineWest);

        AddVoter(context, AdaId, maple, "Ada", "Q", "Fakename", ThisYear - 30);
        AddVoter(context, AdalineId, maple, "Adaline", "R", "Fakename", ThisYear - 31);
        AddVoter(context, AdaOakId, oak, "Ada", "S", "Placeholder", ThisYear - 50);
        AddVoter(context, BrunoId, oak, "Bruno", "T", "Fakename", ThisYear - 50);
        AddVoter(context, CleoId, datelineEast, "Cleo", "U", "Sample", ThisYear - 70);

        await context.SaveChangesAsync();
    }

    private static Address NewAddress(Guid id, string number, string street, string type, string city, decimal latitude, decimal longitude)
    {
        var address = Blank<Address>();
        address.ID = id;
        address.STREET_NUMBER = number;
        address.STREET_NAME = street;
        address.STREET_TYPE = type;
        address.FullStreetAddress = $"{number} {street} {type}";
        address.CITY = city;
        address.STATE = "MI";
        address.ZIP_CODE = "00000";
        address.Latitude = latitude;
        address.Longitude = longitude;
        address.Matched = true;
        return address;
    }

    private static void AddVoter(HeadhunterContext context, Guid id, Address address, string first, string middle, string last, int birthYear)
    {
        // Voters share their primary key with the raw MichiganVoterRecords row they came from.
        var record = Blank<MichiganVoterRecord>();
        record.ID = id;
        record.FIRST_NAME = first;
        record.MIDDLE_NAME = middle;
        record.LAST_NAME = last;
        record.YEAR_OF_BIRTH = birthYear;
        record.REGISTRATION_DATE = new DateTime(2020, 1, 15);
        record.CITY = address.CITY;
        record.STATE = address.STATE;
        context.MichiganVoterRecords.Add(record);

        var voter = Blank<Voter>();
        voter.ID = id;
        voter.FIRST_NAME = first;
        voter.MIDDLE_NAME = middle;
        voter.LAST_NAME = last;
        voter.YEAR_OF_BIRTH = birthYear;
        voter.GENDER = "X";
        voter.REGISTRATION_DATE = new DateTime(2020, 1, 15);
        voter.Address = address;
        context.Voters.Add(voter);
    }

    /// <summary>Creates an entity with every string column set to "" (they are all NOT NULL).</summary>
    private static T Blank<T>() where T : new()
    {
        var entity = new T();
        foreach (var property in typeof(T).GetProperties().Where(p => p.PropertyType == typeof(string) && p.CanWrite))
        {
            property.SetValue(entity, "");
        }
        return entity;
    }
}
