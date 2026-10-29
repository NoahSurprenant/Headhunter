using CsvHelper;
using Headhunter.Database;

namespace Headhunter.CSVDump;

/// <summary>
/// How CsvHelper reads <see cref="Raw"/> (in Headhunter.Database, which doesn't reference CsvHelper): its
/// source generator writes the mapper from this map class. Members not repeated here are read by name,
/// as before; the two Y/N flags go through <see cref="BooleanConverter"/>.
/// </summary>
[CsvRecordMap(typeof(Raw))]
internal partial class RecordMap
{
    [CsvConverter(typeof(BooleanConverter))]
    public bool IS_PERM_AV_APP_VOTER { get; set; }

    [CsvConverter(typeof(BooleanConverter))]
    public bool IS_PERM_AV_BALLOT_VOTER { get; set; }
}
