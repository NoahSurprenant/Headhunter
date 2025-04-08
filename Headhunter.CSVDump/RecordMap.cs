using CsvHelper.Configuration;
using Headhunter.Database;
using System.Globalization;

namespace Headhunter.CSVDump;
public class RecordMap : ClassMap<MichiganVoterRecord>
{
    public RecordMap()
    {
        AutoMap(CultureInfo.InvariantCulture);

        Map(m => m.ID).Ignore();
        Map(m => m.IS_PERM_AV_BALLOT_VOTER).TypeConverter<BooleanConverter>();
        Map(m => m.IS_PERM_AV_APP_VOTER).TypeConverter<BooleanConverter>();
    }
}
