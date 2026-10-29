using Headhunter.CSVDump;

namespace Headhunter.Tests;

/// <summary>Reading the voter file (Headhunter.CSVDump) with CsvHelper, as the import does.</summary>
public class VoterFileTests
{
    // The voter file's header (Headhunter.Database/MichiganVoterRecord.cs) and two records: Y/N flags, one empty, and
    // the registration date as MM/dd/yyyy and as yyyy-MM-dd, both of which the import read before CsvHelper 34.
    private const string Csv =
        "\"LAST_NAME\",\"FIRST_NAME\",\"MIDDLE_NAME\",\"NAME_SUFFIX\",\"YEAR_OF_BIRTH\",\"GENDER\",\"REGISTRATION_DATE\",\"STREET_NUMBER_PREFIX\",\"STREET_NUMBER\",\"STREET_NUMBER_SUFFIX\",\"DIRECTION_PREFIX\",\"STREET_NAME\",\"STREET_TYPE\",\"DIRECTION_SUFFIX\",\"EXTENSION\",\"CITY\",\"STATE\",\"ZIP_CODE\",\"MAILING_ADDRESS_LINE_ONE\",\"MAILING_ADDRESS_LINE_TWO\",\"MAILING_ADDRESS_LINE_THREE\",\"MAILING_ADDRESS_LINE_FOUR\",\"MAILING_ADDRESS_LINE_FIVE\",\"VOTER_IDENTIFICATION_NUMBER\",\"COUNTY_CODE\",\"COUNTY_NAME\",\"JURISDICTION_CODE\",\"JURISDICTION_NAME\",\"PRECINCT\",\"WARD\",\"SCHOOL_DISTRICT_CODE\",\"SCHOOL_DISTRICT_NAME\",\"STATE_HOUSE_DISTRICT_CODE\",\"STATE_HOUSE_DISTRICT_NAME\",\"STATE_SENATE_DISTRICT_CODE\",\"STATE_SENATE_DISTRICT_NAME\",\"US_CONGRESS_DISTRICT_CODE\",\"US_CONGRESS_DISTRICT_NAME\",\"COUNTY_COMMISSIONER_DISTRICT_CODE\",\"COUNTY_COMMISSIONER_DISTRICT_NAME\",\"VILLAGE_DISTRICT_CODE\",\"VILLAGE_DISTRICT_NAME\",\"VILLAGE_PRECINCT\",\"SCHOOL_PRECINCT\",\"IS_PERM_AV_BALLOT_VOTER\",\"VOTER_STATUS_TYPE_CODE\",\"UOCAVA_STATUS_CODE\",\"UOCAVA_STATUS_NAME\",\"IS_PERM_AV_APP_VOTER\"\r\n\"DOE\",\"JANE\",\"MID\",\"NAM\",\"1980\",\"GEN\",\"05/12/2019\",\"STR\",\"STR\",\"STR\",\"DIR\",\"STR\",\"STR\",\"DIR\",\"EXT\",\"CIT\",\"STA\",\"ZIP\",\"MAI\",\"MAI\",\"MAI\",\"MAI\",\"MAI\",\"VOT\",\"COU\",\"COU\",\"JUR\",\"JUR\",\"PRE\",\"WAR\",\"SCH\",\"SCH\",\"STA\",\"STA\",\"STA\",\"STA\",\"US_\",\"US_\",\"COU\",\"COU\",\"VIL\",\"VIL\",\"VIL\",\"SCH\",\"Y\",\"VOT\",\"UOC\",\"UOC\",\"N\"\r\n\"ROE\",\"JOHN\",\"MID\",\"NAM\",\"1975\",\"GEN\",\"2019-05-12\",\"STR\",\"STR\",\"STR\",\"DIR\",\"STR\",\"STR\",\"DIR\",\"EXT\",\"CIT\",\"STA\",\"ZIP\",\"MAI\",\"MAI\",\"MAI\",\"MAI\",\"MAI\",\"VOT\",\"COU\",\"COU\",\"JUR\",\"JUR\",\"PRE\",\"WAR\",\"SCH\",\"SCH\",\"STA\",\"STA\",\"STA\",\"STA\",\"US_\",\"US_\",\"COU\",\"COU\",\"VIL\",\"VIL\",\"VIL\",\"SCH\",\"\",\"VOT\",\"UOC\",\"UOC\",\"Y\"\r\n";

    private string _path = "";

    [SetUp]
    public void WriteFile()
    {
        _path = Path.Combine(Path.GetTempPath(), $"voters-{Guid.NewGuid():N}.csv");
        File.WriteAllText(_path, Csv);
    }

    [TearDown]
    public void DeleteFile() => File.Delete(_path);

    [Test]
    public void ReadsEveryColumnIntoRaw()
    {
        using var csv = VoterFile.Open(_path);
        var records = VoterFile.Records(csv).ToList();

        Assert.That(records, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(records[0].LAST_NAME, Is.EqualTo("DOE"));
            Assert.That(records[0].FIRST_NAME, Is.EqualTo("JANE"));
            Assert.That(records[0].YEAR_OF_BIRTH, Is.EqualTo(1980));
            Assert.That(records[1].YEAR_OF_BIRTH, Is.EqualTo(1975));
            Assert.That(records[0].CITY, Is.EqualTo("CIT"));
            Assert.That(records[0].VOTER_IDENTIFICATION_NUMBER, Is.EqualTo("VOT"));
        });
    }

    [Test]
    public void ReadsRegistrationDates()
    {
        using var csv = VoterFile.Open(_path);
        var records = VoterFile.Records(csv).ToList();

        Assert.That(records.Select(r => r.REGISTRATION_DATE.Date), Is.All.EqualTo(new DateTime(2019, 5, 12)));
    }

    // Only "Y" is true; "N" and an empty field are false, as before CsvHelper 34 (whose own parser would also take
    // y/n/yes/no and fail on empty).
    [Test]
    public void YIsTheOnlyTrue()
    {
        using var csv = VoterFile.Open(_path);
        var records = VoterFile.Records(csv).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(records[0].IS_PERM_AV_BALLOT_VOTER, Is.True);
            Assert.That(records[0].IS_PERM_AV_APP_VOTER, Is.False);
            Assert.That(records[1].IS_PERM_AV_BALLOT_VOTER, Is.False);
            Assert.That(records[1].IS_PERM_AV_APP_VOTER, Is.True);
        });
    }
}
