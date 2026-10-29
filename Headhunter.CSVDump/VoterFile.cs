using CsvHelper;
using Headhunter.Database;

namespace Headhunter.CSVDump;

/// <summary>Reads Michigan's voter file (Qualified Voter File export): UTF-8, comma-separated, with a header.</summary>
public static class VoterFile
{
    /// <summary>The file's records, mapped by <see cref="RecordMap"/>. Enumerate them before disposing the reader.</summary>
    public static CsvReader Open(string path) => CsvReader.FromFile(path, new CsvReaderOptions { Separator = ',' });

    public static IEnumerable<Raw> Records(CsvReader reader) => reader.GetRecords<Raw>();
}
