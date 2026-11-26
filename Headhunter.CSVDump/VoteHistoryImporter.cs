using System.Data;
using Headhunter.Database;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Headhunter.CSVDump;

public sealed record VoteHistoryResult(long Rows, long Inserted);

/// <summary>
/// Loads a voter history file: the rows are bulk-copied into a staging table (codes only, the names go to the lookup
/// tables), then inserted into VoteHistory one election at a time, joined to Voters on the voter number. Rows
/// for a voter that isn't registered, or already in VoteHistory, are skipped, so a file can be loaded again, and the
/// two halves of the state's file can be loaded in either order.
/// </summary>
public sealed class VoteHistoryImporter(HeadhunterContext context)
{
    private const int BatchSize = 50_000;
    private const string Staging = "VoteHistoryStaging";

    public async Task<VoteHistoryResult> ImportAsync(string path, CancellationToken ct = default)
    {
        var postgres = context.Database.IsNpgsql();
        context.Database.SetCommandTimeout(TimeSpan.FromHours(4));
        await context.Database.OpenConnectionAsync(ct);
        try
        {
            await context.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS \"{Staging}\"", ct);
            await context.Database.ExecuteSqlRawAsync(postgres
                ? $"""
                  CREATE UNLOGGED TABLE "{Staging}" ("VoterNumber" varchar(38) COLLATE "C" NOT NULL, "ElectionDate" date NOT NULL,
                      "CountyCode" varchar(2) COLLATE "C" NULL, "JurisdictionCode" varchar(5) COLLATE "C" NULL,
                      "SchoolDistrictCode" varchar(5) COLLATE "C" NULL)
                  """
                : $"""
                  CREATE TABLE "{Staging}" ("VoterNumber" varchar(38) NOT NULL, "ElectionDate" date NOT NULL,
                      "CountyCode" varchar(2) NULL, "JurisdictionCode" varchar(5) NULL, "SchoolDistrictCode" varchar(5) NULL)
                  """, ct);

            var counties = new Dictionary<string, string>();
            var jurisdictions = new Dictionary<(string, string), string>();
            var schools = new Dictionary<string, string>();
            var dates = new HashSet<DateOnly>();
            var rows = 0L;

            await using (IStagingWriter writer = postgres
                             ? new PostgresWriter((NpgsqlConnection)context.Database.GetDbConnection())
                             : new SqlServerWriter((SqlConnection)context.Database.GetDbConnection()))
            {
                foreach (var record in HistoryFile.Records(path))
                {
                    ct.ThrowIfCancellationRequested();
                    var county = NullIfBlank(record.COUNTY_CODE);
                    var jurisdiction = NullIfBlank(record.JURISDICTION_CODE);
                    var school = NullIfBlank(record.SCHOOL_DISTRICT_CODE);
                    var date = DateOnly.FromDateTime(record.ELECTION_DATE);

                    if (county is not null) counties.TryAdd(county, record.COUNTY_NAME.Trim());
                    if (county is not null && jurisdiction is not null)
                        jurisdictions.TryAdd((county, jurisdiction), record.JURISDICTION_NAME.Trim());
                    if (school is not null) schools.TryAdd(school, record.SCHOOL_DISTRICT_NAME.Trim());
                    dates.Add(date);

                    // A jurisdiction without its county can't be a foreign key; keep neither.
                    if (county is null) jurisdiction = null;
                    await writer.AddAsync(record.VOTER_IDENTIFICATION_NUMBER.Trim(), date, county, jurisdiction, school, ct);
                    rows++;
                }
                await writer.CompleteAsync(ct);
            }

            await Lookups.UpsertAsync(context,
                counties.Select(c => (c.Key, c.Value)),
                jurisdictions.Select(j => (j.Key.Item1, j.Key.Item2, j.Value)),
                schools.Select(s => (s.Key, s.Value)),
                dates.Select(d => (d, (int?)null, "")),
                overwrite: false, ct);

            await context.Database.ExecuteSqlRawAsync($"CREATE INDEX \"IX_{Staging}_ElectionDate\" ON \"{Staging}\" (\"ElectionDate\")", ct);

            var inserted = 0L;
            foreach (var date in dates.Order())
                inserted += await context.Database.ExecuteSqlRawAsync(InsertSql(postgres), [date], ct);

            await context.Database.ExecuteSqlRawAsync($"DROP TABLE \"{Staging}\"", ct);
            return new VoteHistoryResult(rows, inserted);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    // Postgres can't hash-join the voter number on its case-insensitive collation (see Worker.InsertVoters).
    private static string InsertSql(bool postgres)
    {
        var collate = postgres ? " COLLATE \"C\"" : "";
        return $$"""
        INSERT INTO "VoteHistory" ("VoterID", "ElectionDate", "CountyCode", "JurisdictionCode", "SchoolDistrictCode")
        SELECT x."VoterID", x."ElectionDate", x."CountyCode", x."JurisdictionCode", x."SchoolDistrictCode"
        FROM (
            SELECT v."ID" AS "VoterID", s."ElectionDate", s."CountyCode", s."JurisdictionCode", s."SchoolDistrictCode",
                   ROW_NUMBER() OVER (PARTITION BY v."ID" ORDER BY s."CountyCode", s."JurisdictionCode", s."SchoolDistrictCode") AS rn
            FROM "{{Staging}}" AS s
            JOIN "Voters" AS v ON v."VOTER_IDENTIFICATION_NUMBER"{{collate}} = s."VoterNumber"
            WHERE s."ElectionDate" = {0}
        ) AS x
        WHERE x.rn = 1
          AND NOT EXISTS (SELECT 1 FROM "VoteHistory" AS h WHERE h."VoterID" = x."VoterID" AND h."ElectionDate" = x."ElectionDate")
        """;
    }

    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private interface IStagingWriter : IAsyncDisposable
    {
        Task AddAsync(string voterNumber, DateOnly date, string? county, string? jurisdiction, string? school, CancellationToken ct);
        Task CompleteAsync(CancellationToken ct);
    }

    private sealed class SqlServerWriter : IStagingWriter
    {
        private readonly SqlBulkCopy _bulk;
        private readonly DataTable _table = new();

        public SqlServerWriter(SqlConnection connection)
        {
            _bulk = new SqlBulkCopy(connection) { DestinationTableName = Staging, BulkCopyTimeout = 0 };
            _table.Columns.Add("VoterNumber", typeof(string));
            _table.Columns.Add("ElectionDate", typeof(DateTime));
            _table.Columns.Add("CountyCode", typeof(string));
            _table.Columns.Add("JurisdictionCode", typeof(string));
            _table.Columns.Add("SchoolDistrictCode", typeof(string));
            foreach (DataColumn column in _table.Columns) _bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        public async Task AddAsync(string voterNumber, DateOnly date, string? county, string? jurisdiction, string? school, CancellationToken ct)
        {
            _table.Rows.Add(voterNumber, date.ToDateTime(TimeOnly.MinValue), (object?)county ?? DBNull.Value,
                (object?)jurisdiction ?? DBNull.Value, (object?)school ?? DBNull.Value);
            if (_table.Rows.Count >= BatchSize) await FlushAsync(ct);
        }

        private async Task FlushAsync(CancellationToken ct)
        {
            await _bulk.WriteToServerAsync(_table, ct);
            _table.Clear();
        }

        public Task CompleteAsync(CancellationToken ct) => _table.Rows.Count > 0 ? FlushAsync(ct) : Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            ((IDisposable)_bulk).Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class PostgresWriter(NpgsqlConnection connection) : IStagingWriter
    {
        private NpgsqlBinaryImporter? _importer;

        private async Task<NpgsqlBinaryImporter> ImporterAsync(CancellationToken ct) => _importer ??= await connection.BeginBinaryImportAsync(
            $"COPY \"{Staging}\" (\"VoterNumber\", \"ElectionDate\", \"CountyCode\", \"JurisdictionCode\", \"SchoolDistrictCode\") FROM STDIN (FORMAT BINARY)", ct);

        public async Task AddAsync(string voterNumber, DateOnly date, string? county, string? jurisdiction, string? school, CancellationToken ct)
        {
            var importer = await ImporterAsync(ct);
            await importer.StartRowAsync(ct);
            await importer.WriteAsync(voterNumber, NpgsqlDbType.Varchar, ct);
            await importer.WriteAsync(date, NpgsqlDbType.Date, ct);
            await WriteTextAsync(importer, county, ct);
            await WriteTextAsync(importer, jurisdiction, ct);
            await WriteTextAsync(importer, school, ct);
        }

        private static Task WriteTextAsync(NpgsqlBinaryImporter importer, string? value, CancellationToken ct) =>
            value is null ? importer.WriteNullAsync(ct) : importer.WriteAsync(value, NpgsqlDbType.Varchar, ct);

        public async Task CompleteAsync(CancellationToken ct)
        {
            if (_importer is null) return;
            await _importer.CompleteAsync(ct);
            await _importer.DisposeAsync();
            _importer = null;
        }

        public async ValueTask DisposeAsync()
        {
            if (_importer is not null) await _importer.DisposeAsync();
        }
    }
}
