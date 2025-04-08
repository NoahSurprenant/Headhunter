namespace Headhunter.Database;

//"LAST_NAME","FIRST_NAME","MIDDLE_NAME","NAME_SUFFIX","YEAR_OF_BIRTH","GENDER","REGISTRATION_DATE","STREET_NUMBER_PREFIX","STREET_NUMBER","STREET_NUMBER_SUFFIX","DIRECTION_PREFIX","STREET_NAME","STREET_TYPE","DIRECTION_SUFFIX","EXTENSION","CITY","STATE","ZIP_CODE","MAILING_ADDRESS_LINE_ONE","MAILING_ADDRESS_LINE_TWO","MAILING_ADDRESS_LINE_THREE","MAILING_ADDRESS_LINE_FOUR","MAILING_ADDRESS_LINE_FIVE","VOTER_IDENTIFICATION_NUMBER","COUNTY_CODE","COUNTY_NAME","JURISDICTION_CODE","JURISDICTION_NAME","PRECINCT","WARD","SCHOOL_DISTRICT_CODE","SCHOOL_DISTRICT_NAME","STATE_HOUSE_DISTRICT_CODE","STATE_HOUSE_DISTRICT_NAME","STATE_SENATE_DISTRICT_CODE","STATE_SENATE_DISTRICT_NAME","US_CONGRESS_DISTRICT_CODE","US_CONGRESS_DISTRICT_NAME","COUNTY_COMMISSIONER_DISTRICT_CODE","COUNTY_COMMISSIONER_DISTRICT_NAME","VILLAGE_DISTRICT_CODE","VILLAGE_DISTRICT_NAME","VILLAGE_PRECINCT","SCHOOL_PRECINCT","IS_PERM_AV_BALLOT_VOTER","VOTER_STATUS_TYPE_CODE","UOCAVA_STATUS_CODE","UOCAVA_STATUS_NAME","IS_PERM_AV_APP_VOTER"

public class MichiganVoterRecord
{
    public Guid ID { get; set; } = Guid.NewGuid();
    public string LAST_NAME { get; set; }
    public string FIRST_NAME { get; set; }
    public string MIDDLE_NAME { get; set; }
    public string NAME_SUFFIX { get; set; }
    public int YEAR_OF_BIRTH { get; set; }
    public string GENDER { get; set; }
    public DateTime REGISTRATION_DATE { get; set; }
    public string STREET_NUMBER_PREFIX { get; set; }
    public string STREET_NUMBER { get; set; }
    public string STREET_NUMBER_SUFFIX { get; set; }
    public string DIRECTION_PREFIX { get; set; }
    public string STREET_NAME { get; set; }
    public string STREET_TYPE { get; set; }
    public string DIRECTION_SUFFIX { get; set; }
    public string EXTENSION { get; set; }
    public string CITY { get; set; }
    public string STATE { get; set; }
    public string ZIP_CODE { get; set; }
    public string MAILING_ADDRESS_LINE_ONE { get; set; }
    public string MAILING_ADDRESS_LINE_TWO { get; set; }
    public string MAILING_ADDRESS_LINE_THREE { get; set; }
    public string MAILING_ADDRESS_LINE_FOUR { get; set; }
    public string MAILING_ADDRESS_LINE_FIVE { get; set; }
    public string VOTER_IDENTIFICATION_NUMBER { get; set; }
    public string COUNTY_CODE { get; set; }
    public string COUNTY_NAME { get; set; }
    public string JURISDICTION_CODE { get; set; }
    public string JURISDICTION_NAME { get; set; }
    public string PRECINCT { get; set; }
    public string WARD { get; set; }
    public string SCHOOL_DISTRICT_CODE { get; set; }
    public string SCHOOL_DISTRICT_NAME { get; set; }
    public string STATE_HOUSE_DISTRICT_CODE { get; set; }
    public string STATE_HOUSE_DISTRICT_NAME { get; set; }
    public string STATE_SENATE_DISTRICT_CODE { get; set; }
    public string STATE_SENATE_DISTRICT_NAME { get; set; }
    public string US_CONGRESS_DISTRICT_CODE { get; set; }
    public string US_CONGRESS_DISTRICT_NAME { get; set; }
    public string COUNTY_COMMISSIONER_DISTRICT_CODE { get; set; }
    public string COUNTY_COMMISSIONER_DISTRICT_NAME { get; set; }
    public string VILLAGE_DISTRICT_CODE { get; set; }
    public string VILLAGE_DISTRICT_NAME { get; set; }
    public string VILLAGE_PRECINCT { get; set; }
    public string SCHOOL_PRECINCT { get; set; }
    public bool IS_PERM_AV_BALLOT_VOTER { get; set; }
    public string VOTER_STATUS_TYPE_CODE { get; set; }
    public string UOCAVA_STATUS_CODE { get; set; }
    public string UOCAVA_STATUS_NAME { get; set; }
    public bool IS_PERM_AV_APP_VOTER { get; set; }
}
