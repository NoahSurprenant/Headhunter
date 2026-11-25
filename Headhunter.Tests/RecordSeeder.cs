using Headhunter.Database;
using Microsoft.EntityFrameworkCore;

namespace Headhunter.Tests;

/// <summary>
/// Fills MichiganVoterRecords (the table the CSV import loads, and the import SQL reads) with synthetic rows,
/// generated inside the database so a million of them take seconds. Two voters per address; every name,
/// street and city is made up ("Last123", "Street7", "City42").
/// </summary>
public static class RecordSeeder
{
    public static async Task SeedAsync(HeadhunterContext context, int voters)
    {
        var postgres = DatabaseFixture.Provider == DatabaseProvider.Postgres;
        var household = "(n / 2)";
        var street = $"({household} / 1000)";

        var overrides = new Dictionary<string, string>
        {
            ["ID"] = postgres ? "gen_random_uuid()" : "NEWID()",
            ["LAST_NAME"] = "CONCAT('Last', n % 5000)",
            ["FIRST_NAME"] = "CONCAT('First', n % 2000)",
            ["MIDDLE_NAME"] = "CONCAT('Mid', n % 26)",
            ["YEAR_OF_BIRTH"] = "1940 + n % 60",
            ["GENDER"] = "'F'",
            ["STREET_NUMBER"] = $"CAST({household} % 1000 + 1 AS varchar(10))",
            ["STREET_NAME"] = $"CONCAT('Street', {street})",
            ["STREET_TYPE"] = "'St'",
            ["CITY"] = $"CONCAT('City', {street} % 300)",
            ["STATE"] = "'MI'",
            ["ZIP_CODE"] = $"CAST(48000 + {street} % 1000 AS varchar(10))",
            ["COUNTY_NAME"] = $"CONCAT('County', {street} % 83)",
            ["JURISDICTION_NAME"] = $"CONCAT('CITY OF CITY', {street} % 300)",
            ["VOTER_IDENTIFICATION_NUMBER"] = "CAST(n AS varchar(20))",
            ["VOTER_STATUS_TYPE_CODE"] = "'A'",
        };

        var columns = context.Model.FindEntityType(typeof(MichiganVoterRecord))!.GetProperties()
            .Select(p => (Name: p.GetColumnName(), p.ClrType))
            .Where(c => c.Name is not null)
            .ToList();

        string Value((string? Name, Type ClrType) c)
        {
            if (overrides.TryGetValue(c.Name!, out var expression)) return expression;
            var type = Nullable.GetUnderlyingType(c.ClrType) ?? c.ClrType;
            if (type == typeof(string)) return "''";
            if (type == typeof(bool)) return postgres ? "false" : "0";
            if (type == typeof(DateTime)) return "CAST('2000-01-01' AS date)";
            return "0";
        }

        var rows = postgres
            ? $"generate_series(1::bigint, {voters}::bigint) AS g(n)"
            : $"(SELECT TOP ({voters}) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS bigint) AS n " +
              "FROM sys.all_objects a CROSS JOIN sys.all_objects b CROSS JOIN sys.all_objects c) AS g";

        var sql = $"INSERT INTO \"MichiganVoterRecords\" ({string.Join(", ", columns.Select(c => $"\"{c.Name}\""))}) " +
                  $"SELECT {string.Join(", ", columns.Select(Value))} FROM {rows}";

        context.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
        await context.Database.ExecuteSqlRawAsync(sql);
    }
}
