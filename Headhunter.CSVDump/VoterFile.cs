using System.IO.Compression;
using CsvHelper;
using Headhunter.Database;

namespace Headhunter.CSVDump;

/// <summary>Reads Michigan's voter file (Qualified Voter File export): UTF-8, comma-separated, with a header.</summary>
public static class VoterFile
{
    /// <summary>The file's records, mapped by <see cref="RecordMap"/>. Enumerate them before disposing the reader.</summary>
    public static CsvReader Open(string path) => CsvReader.FromFile(path, new CsvReaderOptions { Separator = ',' });

    public static IEnumerable<Raw> Records(CsvReader reader) => reader.GetRecords<Raw>();

    /// <summary>The file's records: the CSV itself, or the zip the state sends it in.</summary>
    public static IEnumerable<Raw> Records(string path)
    {
        if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var file = Open(path);
            foreach (var record in Records(file)) yield return record;
            yield break;
        }

        // CsvHelper reads a file path or a whole string, not a stream, so the CSV is unpacked to a temporary file.
        var temp = Path.Combine(Path.GetTempPath(), $"voters-{Guid.NewGuid():N}.csv");
        try
        {
            using (var archive = ZipFile.OpenRead(path))
            {
                var entry = archive.Entries.Single(e => e.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));
                entry.ExtractToFile(temp);
            }

            using var unzipped = Open(temp);
            foreach (var record in Records(unzipped)) yield return record;
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
