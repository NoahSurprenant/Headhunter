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

    // A block of Elm St, Elmville, in a line 11m apart (by latitude) for "walk the street": house numbers
    // 101-107 in order, so ElmAddressIds[i] is house "10{i+1}", and far enough from Maple/Oak/Meridian to
    // not collide with the Area/distance tests or the Relatives fixtures (Common sits at 41, -85).
    public static readonly Guid[] ElmAddressIds = Enumerable.Range(1, 7)
        .Select(i => Guid.Parse($"00000000-0000-0000-0000-00000000a10{i}"))
        .ToArray();
    // Far down the same street (same name/type/city/zip), well outside the default nearby search radius.
    public static readonly Guid ElmFarAddressId = Guid.Parse("00000000-0000-0000-0000-00000000a199");

    public static readonly Guid AdaId = Guid.Parse("00000000-0000-0000-0000-00000000b001");
    public static readonly Guid AdalineId = Guid.Parse("00000000-0000-0000-0000-00000000b002");
    public static readonly Guid AdaOakId = Guid.Parse("00000000-0000-0000-0000-00000000b003");
    public static readonly Guid BrunoId = Guid.Parse("00000000-0000-0000-0000-00000000b004");
    public static readonly Guid CleoId = Guid.Parse("00000000-0000-0000-0000-00000000b005");

    // Possible relatives (ApiController.Relatives): RelativeRef's household (same address, excluded), a match
    // ~1.4 km away on a different street, a match on the same street far away, and a negative control (same
    // last name, but neither close nor on the same street - like Bruno/Ada above, but kept separate so this
    // doesn't disturb the "Fakename" fixtures other tests already rely on).
    public static readonly Guid RelativeRefAddressId = Guid.Parse("00000000-0000-0000-0000-00000000a005");
    public static readonly Guid RelativeNearAddressId = Guid.Parse("00000000-0000-0000-0000-00000000a006");
    public static readonly Guid RelativeStreetAddressId = Guid.Parse("00000000-0000-0000-0000-00000000a007");
    public static readonly Guid RelativeFarAddressId = Guid.Parse("00000000-0000-0000-0000-00000000a008");
    public static readonly Guid RelativeRefId = Guid.Parse("00000000-0000-0000-0000-00000000b006");
    public static readonly Guid RelativeHouseholdId = Guid.Parse("00000000-0000-0000-0000-00000000b007");
    public static readonly Guid RelativeNearId = Guid.Parse("00000000-0000-0000-0000-00000000b008");
    public static readonly Guid RelativeStreetId = Guid.Parse("00000000-0000-0000-0000-00000000b009");
    public static readonly Guid RelativeFarId = Guid.Parse("00000000-0000-0000-0000-00000000b010");

    // A common-surname scenario, with NoiseCount other "Commonname" voters scattered far away, to show the
    // query stays bounded and fast regardless of how common the name is.
    public static readonly Guid CommonAddressId = Guid.Parse("00000000-0000-0000-0000-00000000a009");
    public static readonly Guid CommonNearAddressId = Guid.Parse("00000000-0000-0000-0000-00000000a010");
    public static readonly Guid CommonStreetAddressId = Guid.Parse("00000000-0000-0000-0000-00000000a011");
    public static readonly Guid CommonRefId = Guid.Parse("00000000-0000-0000-0000-00000000b011");
    public static readonly Guid CommonNearId = Guid.Parse("00000000-0000-0000-0000-00000000b012");
    public static readonly Guid CommonStreetId = Guid.Parse("00000000-0000-0000-0000-00000000b013");
    public const int NoiseCount = 100;

    public static async Task SeedAsync(HeadhunterContext context)
    {
        var maple = NewAddress(MapleAddressId, "100", "Maple", "St", "Testville", 42.500000000001m, -83.500000000001m);
        var oak = NewAddress(OakAddressId, "200", "Oak", "Ave", "Testburg", 43.0m, -84.0m);
        var datelineEast = NewAddress(DatelineEastAddressId, "1", "Meridian", "Rd", "Eastedge", 10.0m, 179.5m);
        var datelineWest = NewAddress(DatelineWestAddressId, "2", "Meridian", "Rd", "Westedge", 10.0m, -179.5m);
        var relativeRef = NewAddress(RelativeRefAddressId, "10", "Birch", "Ln", "Kinfolk", 44.000000000001m, -85.000000000001m);
        var relativeNear = NewAddress(RelativeNearAddressId, "20", "Cedar", "Ave", "Kinwood", 44.010000000001m, -85.010000000001m);
        var relativeStreet = NewAddress(RelativeStreetAddressId, "90", "Birch", "Ln", "Kinfolk", 6.0m, 6.0m);
        var relativeFar = NewAddress(RelativeFarAddressId, "30", "Spruce", "Rd", "Farplace", 7.0m, 7.0m);
        var common = NewAddress(CommonAddressId, "10", "Elm", "Dr", "Groveport", 41.000000000001m, -85.000000000001m);
        var commonNear = NewAddress(CommonNearAddressId, "20", "Cedar", "Ln", "Fernwood", 41.010000000001m, -85.010000000001m);
        var commonStreet = NewAddress(CommonStreetAddressId, "30", "Elm", "Dr", "Groveport", 5.0m, 5.0m);

        context.Addresses.AddRange(maple, oak, datelineEast, datelineWest,
            relativeRef, relativeNear, relativeStreet, relativeFar, common, commonNear, commonStreet);

        AddVoter(context, AdaId, maple, "Ada", "Q", "Fakename", ThisYear - 30);
        AddVoter(context, AdalineId, maple, "Adaline", "R", "Fakename", ThisYear - 31);
        AddVoter(context, AdaOakId, oak, "Ada", "S", "Placeholder", ThisYear - 50);
        AddVoter(context, BrunoId, oak, "Bruno", "T", "Fakename", ThisYear - 50);
        AddVoter(context, CleoId, datelineEast, "Cleo", "U", "Sample", ThisYear - 70);
        AddVoter(context, RelativeRefId, relativeRef, "Kin", "V", "Testkin", ThisYear - 45);
        AddVoter(context, RelativeHouseholdId, relativeRef, "Kin2", "W", "Testkin", ThisYear - 46);
        AddVoter(context, RelativeNearId, relativeNear, "Noor", "X", "Testkin", ThisYear - 40);
        AddVoter(context, RelativeStreetId, relativeStreet, "Sam", "Y", "Testkin", ThisYear - 60);
        AddVoter(context, RelativeFarId, relativeFar, "Robin", "Z", "Testkin", ThisYear - 55);
        AddVoter(context, CommonRefId, common, "CommonRef", "", "Commonname", ThisYear - 35);
        AddVoter(context, CommonNearId, commonNear, "CommonNear", "", "Commonname", ThisYear - 36);
        AddVoter(context, CommonStreetId, commonStreet, "CommonStreet", "", "Commonname", ThisYear - 37);

        // Scattered noise, nowhere near Common/CommonNear/CommonStreet and on none of their streets: proves
        // the query's cost comes from geography, not from how many voters share the surname.
        for (var i = 0; i < NoiseCount; i++)
        {
            var noiseAddress = NewAddress(
                Guid.Parse($"00000000-0000-0000-00c1-{i:D12}"),
                (100 + i).ToString(), $"NoiseSt{i:D3}", "Rd", $"NoiseCity{i:D3}",
                20.0m + i * 0.5m, -60.0m - i * 0.3m);
            context.Addresses.Add(noiseAddress);
            AddVoter(context, Guid.Parse($"00000000-0000-0000-00c2-{i:D12}"), noiseAddress, $"Noise{i:D3}", "", "Commonname", ThisYear - 48);
        }

        // Elm St: houses 101-107, 11m apart, plus a different street nearby (not Elm, so it never shows up
        // as a "walk the street" neighbor) for the nearby-radius widening test.
        var elm = ElmAddressIds
            .Select((id, i) => NewAddress(id, (101 + i).ToString(), "Elm", "St", "Elmville", 45.0m + 0.0001m * i, -86.0m))
            .ToArray();
        var elmFar = NewAddress(ElmFarAddressId, "1", "Birch", "St", "Elmville", 45.05m, -86.0m);
        context.Addresses.AddRange(elm);
        context.Addresses.Add(elmFar);

        // Voter counts vary (0 to 2) so the nearby list's counts can be checked, not just its order.
        var elmVoterCounts = new[] { 2, 0, 1, 0, 1, 0, 1 };
        for (var i = 0; i < elm.Length; i++)
        {
            for (var v = 0; v < elmVoterCounts[i]; v++)
            {
                AddVoter(context, Guid.Parse($"00000000-0000-0000-0000-00000000c1{i}{v}"), elm[i], "Elm", v.ToString(), "Walker", ThisYear - 40);
            }
        }

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
