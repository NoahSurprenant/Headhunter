namespace Headhunter.Database;
public class Voter
{
    public Guid ID { get; set; }
    public string LAST_NAME { get; set; }
    public string FIRST_NAME { get; set; }
    public string MIDDLE_NAME { get; set; }
    public string NAME_SUFFIX { get; set; }
    public int YEAR_OF_BIRTH { get; set; }
    public string GENDER { get; set; }
    public DateTime REGISTRATION_DATE { get; set; }
    public string MAILING_ADDRESS_LINE_ONE { get; set; }
    public string MAILING_ADDRESS_LINE_TWO { get; set; }
    public string MAILING_ADDRESS_LINE_THREE { get; set; }
    public string MAILING_ADDRESS_LINE_FOUR { get; set; }
    public string MAILING_ADDRESS_LINE_FIVE { get; set; }
    public string VOTER_IDENTIFICATION_NUMBER { get; set; }
    public bool IS_PERM_AV_BALLOT_VOTER { get; set; }
    public string VOTER_STATUS_TYPE_CODE { get; set; }
    public string UOCAVA_STATUS_CODE { get; set; }
    public string UOCAVA_STATUS_NAME { get; set; }
    public bool IS_PERM_AV_APP_VOTER { get; set; }
    public Guid AddressID { get; set; }
    public Address Address { get; set; } = null!;
    public MichiganVoterRecord MichiganVoterRecord { get; set; } = null!;
}
