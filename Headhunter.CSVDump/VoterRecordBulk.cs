using System.Data;
using Headhunter.Database;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace Headhunter.CSVDump;

/// <summary>
/// Loads registration rows with the database's bulk path (Postgres COPY, SQL Server SqlBulkCopy) instead of EF's
/// batched INSERTs, which cost a statement per few hundred rows. The columns come from EF's model, so a new column
/// on <see cref="MichiganVoterRecord"/> is loaded without a change here.
/// </summary>
public static class VoterRecordBulk
{
    private const string Table = "MichiganVoterRecords";

    public static async Task InsertAsync(HeadhunterContext context, IReadOnlyList<MichiganVoterRecord> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return;

        var table = StoreObjectIdentifier.Table(Table, null);
        var columns = context.Model.FindEntityType(typeof(MichiganVoterRecord))!.GetProperties()
            .Select(p => (Property: p, Name: p.GetColumnName(table)!, Converter: p.GetValueConverter()))
            .ToList();

        object? ValueOf((IProperty Property, string Name, Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter? Converter) column, MichiganVoterRecord row)
        {
            var value = column.Property.PropertyInfo!.GetValue(row);
            return value is not null && column.Converter is not null ? column.Converter.ConvertToProvider(value) : value;
        }

        await context.Database.OpenConnectionAsync(ct);
        try
        {
            if (context.Database.IsNpgsql())
            {
                var connection = (NpgsqlConnection)context.Database.GetDbConnection();
                var names = string.Join(", ", columns.Select(c => $"\"{c.Name}\""));
                var types = columns.Select(c => c.Property.GetRelationalTypeMapping().StoreTypeNameBase!).ToList();
                await using var importer = await connection.BeginBinaryImportAsync($"COPY \"{Table}\" ({names}) FROM STDIN (FORMAT BINARY)", ct);
                foreach (var row in rows)
                {
                    await importer.StartRowAsync(ct);
                    for (var i = 0; i < columns.Count; i++)
                    {
                        var value = ValueOf(columns[i], row);
                        if (value is null) await importer.WriteNullAsync(ct);
                        else await importer.WriteAsync(value, types[i], ct);
                    }
                }

                await importer.CompleteAsync(ct);
            }
            else
            {
                var connection = (SqlConnection)context.Database.GetDbConnection();
                var data = new DataTable();
                foreach (var column in columns)
                {
                    var clr = column.Converter?.ProviderClrType ?? column.Property.ClrType;
                    data.Columns.Add(column.Name, Nullable.GetUnderlyingType(clr) ?? clr);
                }

                foreach (var row in rows) data.Rows.Add(columns.Select(c => ValueOf(c, row) ?? DBNull.Value).ToArray());

                using var bulk = new SqlBulkCopy(connection) { DestinationTableName = Table, BulkCopyTimeout = 0 };
                foreach (var column in columns) bulk.ColumnMappings.Add(column.Name, column.Name);
                await bulk.WriteToServerAsync(data, ct);
            }
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
