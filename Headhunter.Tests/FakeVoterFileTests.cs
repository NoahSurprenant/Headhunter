using Headhunter.CSVDump;

namespace Headhunter.Tests;

public class FakeVoterFileTests
{
    // The real file's header, in its order (Headhunter.Database/MichiganVoterRecord.cs).
    private const string RealHeader =
        "\"LAST_NAME\",\"FIRST_NAME\",\"MIDDLE_NAME\",\"NAME_SUFFIX\",\"YEAR_OF_BIRTH\",\"GENDER\",\"REGISTRATION_DATE\",\"STREET_NUMBER_PREFIX\",\"STREET_NUMBER\",\"STREET_NUMBER_SUFFIX\",\"DIRECTION_PREFIX\",\"STREET_NAME\",\"STREET_TYPE\",\"DIRECTION_SUFFIX\",\"EXTENSION\",\"CITY\",\"STATE\",\"ZIP_CODE\",\"MAILING_ADDRESS_LINE_ONE\",\"MAILING_ADDRESS_LINE_TWO\",\"MAILING_ADDRESS_LINE_THREE\",\"MAILING_ADDRESS_LINE_FOUR\",\"MAILING_ADDRESS_LINE_FIVE\",\"VOTER_IDENTIFICATION_NUMBER\",\"COUNTY_CODE\",\"COUNTY_NAME\",\"JURISDICTION_CODE\",\"JURISDICTION_NAME\",\"PRECINCT\",\"WARD\",\"SCHOOL_DISTRICT_CODE\",\"SCHOOL_DISTRICT_NAME\",\"STATE_HOUSE_DISTRICT_CODE\",\"STATE_HOUSE_DISTRICT_NAME\",\"STATE_SENATE_DISTRICT_CODE\",\"STATE_SENATE_DISTRICT_NAME\",\"US_CONGRESS_DISTRICT_CODE\",\"US_CONGRESS_DISTRICT_NAME\",\"COUNTY_COMMISSIONER_DISTRICT_CODE\",\"COUNTY_COMMISSIONER_DISTRICT_NAME\",\"VILLAGE_DISTRICT_CODE\",\"VILLAGE_DISTRICT_NAME\",\"VILLAGE_PRECINCT\",\"SCHOOL_PRECINCT\",\"IS_PERM_AV_BALLOT_VOTER\",\"VOTER_STATUS_TYPE_CODE\",\"UOCAVA_STATUS_CODE\",\"UOCAVA_STATUS_NAME\",\"IS_PERM_AV_APP_VOTER\"";

    [Test]
    public void HasTheRealFilesHeader_AndIsTheSameEveryTime()
    {
        var fake = FakeVoterFile.Create(300);

        Assert.That(fake.Csv.Split("\r\n")[0], Is.EqualTo(RealHeader));
        Assert.That(FakeVoterFile.Create(300).Csv, Is.EqualTo(fake.Csv));
        Assert.That(FakeVoterFile.Create(300, seed: 2).Csv, Is.Not.EqualTo(fake.Csv));
    }

    [Test]
    public void ReadsBackThroughTheImportsReader_WithUniqueVoterNumbers_AndFewerHouseholdsThanVoters()
    {
        var fake = FakeVoterFile.Create(2000);
        var path = Path.Combine(Path.GetTempPath(), $"fake-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, fake.Csv);
        try
        {
            using var csv = VoterFile.Open(path);
            var records = VoterFile.Records(csv).ToList();

            Assert.That(records, Has.Count.EqualTo(2000));
            Assert.That(records.Select(r => r.VOTER_IDENTIFICATION_NUMBER).Distinct().Count(), Is.EqualTo(2000));
            Assert.That(records.Select(r => r.REGISTRATION_DATE), Is.EqualTo(fake.Records.Select(r => r.REGISTRATION_DATE)));
            Assert.That(records.Count(r => r.IS_PERM_AV_BALLOT_VOTER), Is.EqualTo(fake.PermAvBallotVoters));
            Assert.That(fake.Households, Is.InRange(600, 1400));
        }
        finally { File.Delete(path); }
    }
}
