using Headhunter.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Headhunter.Migrations.Postgres.Migrations
{
    // Expression indexes EF cannot model, so the model snapshot does not change.
    /// <inheritdoc />
    [DbContext(typeof(HeadhunterContext))]
    [Migration("20261127120000_PrefixIndexes")]
    public partial class PrefixIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The autocomplete: LIKE 'X%' cannot seek a btree on the nondeterministic case-insensitive collation
            // (a full index scan, 1 to 2.4 s on 7M voters), but it can on upper(column) in the "C" collation.
            migrationBuilder.Sql("CREATE INDEX \"IX_Voters_FIRST_NAME_Prefix\" ON \"Voters\" ((upper(\"FIRST_NAME\") COLLATE \"C\"))");
            migrationBuilder.Sql("CREATE INDEX \"IX_Voters_MIDDLE_NAME_Prefix\" ON \"Voters\" ((upper(\"MIDDLE_NAME\") COLLATE \"C\"))");
            migrationBuilder.Sql("CREATE INDEX \"IX_Voters_LAST_NAME_Prefix\" ON \"Voters\" ((upper(\"LAST_NAME\") COLLATE \"C\"))");
            migrationBuilder.Sql("CREATE INDEX \"IX_Addresses_CITY_Prefix\" ON \"Addresses\" ((upper(\"CITY\") COLLATE \"C\"))");
            migrationBuilder.Sql("CREATE INDEX \"IX_Addresses_FullStreetAddress_Prefix\" ON \"Addresses\" ((upper(\"FullStreetAddress\") COLLATE \"C\"))");
            migrationBuilder.Sql("CREATE INDEX \"IX_Addresses_STREET_NAME_Prefix\" ON \"Addresses\" ((upper(\"STREET_NAME\") COLLATE \"C\")) INCLUDE (\"FullStreetAddress\")");

            // The voter search by city and birth year or age: joins the city's addresses to the year's voters,
            // each from an index alone (2 s to 40 ms for Ann Arbor, 1985).
            migrationBuilder.Sql("CREATE INDEX \"IX_Addresses_CITY_ID\" ON \"Addresses\" (\"CITY\") INCLUDE (\"ID\")");
            migrationBuilder.Sql("CREATE INDEX \"IX_Voters_YEAR_OF_BIRTH_AddressID\" ON \"Voters\" (\"YEAR_OF_BIRTH\", \"AddressID\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var index in new[]
            {
                "IX_Voters_FIRST_NAME_Prefix", "IX_Voters_MIDDLE_NAME_Prefix", "IX_Voters_LAST_NAME_Prefix",
                "IX_Addresses_CITY_Prefix", "IX_Addresses_FullStreetAddress_Prefix", "IX_Addresses_STREET_NAME_Prefix",
                "IX_Addresses_CITY_ID", "IX_Voters_YEAR_OF_BIRTH_AddressID",
            })
                migrationBuilder.Sql($"DROP INDEX \"{index}\"");
        }
    }
}
