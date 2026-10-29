using CsvHelper;

namespace Headhunter.CSVDump;

/// <summary>The voter file's Y/N flags: "Y" is true, anything else (N, empty) false.</summary>
public sealed class BooleanConverter : ICsvConverter<bool>
{
    public bool Read(CsvReader reader, int index) => reader.GetString(index) == "Y";

    public void Write(CsvWriter writer, bool value) => writer.WriteField(value ? "Y" : "N");
}
