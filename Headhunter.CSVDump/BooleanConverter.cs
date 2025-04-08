using CsvHelper.Configuration;
using CsvHelper;
using CsvHelper.TypeConversion;

namespace Headhunter.CSVDump;
public class BooleanConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        return text == "Y";
    }
}
