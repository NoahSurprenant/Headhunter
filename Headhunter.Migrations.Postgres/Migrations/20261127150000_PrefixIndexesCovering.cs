using Headhunter.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Headhunter.Migrations.Postgres.Migrations
{
    // Expression indexes EF cannot model, so the model snapshot does not change.
    /// <inheritdoc />
    [DbContext(typeof(HeadhunterContext))]
    [Migration("20261127150000_PrefixIndexesCovering")]
    public partial class PrefixIndexesCovering : Migration
    {
        private static readonly (string Index, string Table, string Column, string Include)[] Indexes =
        [
            ("IX_Voters_FIRST_NAME_Prefix", "Voters", "FIRST_NAME", "FIRST_NAME"),
            ("IX_Voters_MIDDLE_NAME_Prefix", "Voters", "MIDDLE_NAME", "MIDDLE_NAME"),
            ("IX_Voters_LAST_NAME_Prefix", "Voters", "LAST_NAME", "LAST_NAME"),
            ("IX_Addresses_CITY_Prefix", "Addresses", "CITY", "CITY"),
            ("IX_Addresses_FullStreetAddress_Prefix", "Addresses", "FullStreetAddress", "FullStreetAddress"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // An index on upper(col) cannot be scanned alone unless col is included: a common prefix
            // ("MAR", 566k voters) then read the table for every match (3.4 s); with it, 0.2 ms.
            foreach (var (index, table, column, include) in Indexes)
            {
                migrationBuilder.Sql($"DROP INDEX IF EXISTS \"{index}\"");
                migrationBuilder.Sql($"CREATE INDEX \"{index}\" ON \"{table}\" ((upper(\"{column}\") COLLATE \"C\")) INCLUDE (\"{include}\")");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (index, table, column, _) in Indexes)
            {
                migrationBuilder.Sql($"DROP INDEX IF EXISTS \"{index}\"");
                migrationBuilder.Sql($"CREATE INDEX \"{index}\" ON \"{table}\" ((upper(\"{column}\") COLLATE \"C\"))");
            }
        }
    }
}
