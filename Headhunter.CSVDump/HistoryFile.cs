using System.IO.Compression;
using CsvHelper;

namespace Headhunter.CSVDump;

/// <summary>One row of Michigan's voter history file (Qualified Voter History export): a voter voted in an election.</summary>
public class HistoryRecord
{
    public string VOTER_IDENTIFICATION_NUMBER { get; set; } = "";
    public string COUNTY_CODE { get; set; } = "";
    public string COUNTY_NAME { get; set; } = "";
    public string JURISDICTION_CODE { get; set; } = "";
    public string JURISDICTION_NAME { get; set; } = "";
    public string SCHOOL_DISTRICT_CODE { get; set; } = "";
    public string SCHOOL_DISTRICT_NAME { get; set; } = "";
    public DateTime ELECTION_DATE { get; set; }
}

[CsvRecordMap(typeof(HistoryRecord))]
internal partial class HistoryRecordMap
{
}

/// <summary>Reads the history file: the CSV itself, or the zip the state sends it in.</summary>
public static class HistoryFile
{
    public static IEnumerable<HistoryRecord> Records(string path)
    {
        if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var file = CsvReader.FromFile(path, new CsvReaderOptions { Separator = ',' });
            foreach (var record in file.GetRecords<HistoryRecord>()) yield return record;
            yield break;
        }

        // CsvHelper reads a file path or a whole string, not a stream, so the CSV is unpacked to a temporary file.
        var temp = Path.Combine(Path.GetTempPath(), $"history-{Guid.NewGuid():N}.csv");
        try
        {
            using (var archive = ZipFile.OpenRead(path))
            {
                var entry = archive.Entries.Single(e => e.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));
                entry.ExtractToFile(temp);
            }

            using var unzipped = CsvReader.FromFile(temp, new CsvReaderOptions { Separator = ',' });
            foreach (var record in unzipped.GetRecords<HistoryRecord>()) yield return record;
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
