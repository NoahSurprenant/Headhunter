using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Headhunter.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:CollationDefinition:case_insensitive", "und-u-ks-level2,und-u-ks-level2,icu,False");

            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    STREET_NUMBER_PREFIX = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    STREET_NUMBER = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    STREET_NUMBER_SUFFIX = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    DIRECTION_PREFIX = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    STREET_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    STREET_TYPE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    DIRECTION_SUFFIX = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    EXTENSION = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    FullStreetAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    CITY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    STATE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    ZIP_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    COUNTY_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    COUNTY_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    JURISDICTION_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    JURISDICTION_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    PRECINCT = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    WARD = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    SCHOOL_DISTRICT_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    SCHOOL_DISTRICT_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    STATE_HOUSE_DISTRICT_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    STATE_HOUSE_DISTRICT_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    STATE_SENATE_DISTRICT_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    STATE_SENATE_DISTRICT_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    US_CONGRESS_DISTRICT_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    US_CONGRESS_DISTRICT_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    COUNTY_COMMISSIONER_DISTRICT_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    COUNTY_COMMISSIONER_DISTRICT_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    VILLAGE_DISTRICT_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    VILLAGE_DISTRICT_NAME = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    VILLAGE_PRECINCT = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    SCHOOL_PRECINCT = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    Latitude = table.Column<decimal>(type: "numeric(15,12)", precision: 15, scale: 12, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(15,12)", precision: 15, scale: 12, nullable: true),
                    Matched = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MichiganVoterRecords",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    LAST_NAME = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    FIRST_NAME = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    MIDDLE_NAME = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    NAME_SUFFIX = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    YEAR_OF_BIRTH = table.Column<int>(type: "integer", nullable: false),
                    GENDER = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    REGISTRATION_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    STREET_NUMBER_PREFIX = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    STREET_NUMBER = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    STREET_NUMBER_SUFFIX = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    DIRECTION_PREFIX = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    STREET_NAME = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    STREET_TYPE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    DIRECTION_SUFFIX = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    EXTENSION = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    CITY = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    STATE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    ZIP_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    MAILING_ADDRESS_LINE_ONE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    MAILING_ADDRESS_LINE_TWO = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    MAILING_ADDRESS_LINE_THREE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    MAILING_ADDRESS_LINE_FOUR = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    MAILING_ADDRESS_LINE_FIVE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    VOTER_IDENTIFICATION_NUMBER = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    COUNTY_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    COUNTY_NAME = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    JURISDICTION_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    JURISDICTION_NAME = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    PRECINCT = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    WARD = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    SCHOOL_DISTRICT_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    SCHOOL_DISTRICT_NAME = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    STATE_HOUSE_DISTRICT_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    STATE_HOUSE_DISTRICT_NAME = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    STATE_SENATE_DISTRICT_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    STATE_SENATE_DISTRICT_NAME = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    US_CONGRESS_DISTRICT_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    US_CONGRESS_DISTRICT_NAME = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    COUNTY_COMMISSIONER_DISTRICT_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    COUNTY_COMMISSIONER_DISTRICT_NAME = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    VILLAGE_DISTRICT_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    VILLAGE_DISTRICT_NAME = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    VILLAGE_PRECINCT = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    SCHOOL_PRECINCT = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    IS_PERM_AV_BALLOT_VOTER = table.Column<bool>(type: "boolean", nullable: false),
                    VOTER_STATUS_TYPE_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    UOCAVA_STATUS_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    UOCAVA_STATUS_NAME = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    IS_PERM_AV_APP_VOTER = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MichiganVoterRecords", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Voters",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    LAST_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    FIRST_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    MIDDLE_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    NAME_SUFFIX = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    YEAR_OF_BIRTH = table.Column<int>(type: "integer", nullable: false),
                    GENDER = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    REGISTRATION_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MAILING_ADDRESS_LINE_ONE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    MAILING_ADDRESS_LINE_TWO = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    MAILING_ADDRESS_LINE_THREE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    MAILING_ADDRESS_LINE_FOUR = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    MAILING_ADDRESS_LINE_FIVE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    HAS_MAILING_ADDRESS = table.Column<bool>(type: "boolean", nullable: false, computedColumnSql: "CASE WHEN length(\"MAILING_ADDRESS_LINE_ONE\") > 0 THEN TRUE ELSE FALSE END", stored: true),
                    VOTER_IDENTIFICATION_NUMBER = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    IS_PERM_AV_BALLOT_VOTER = table.Column<bool>(type: "boolean", nullable: false),
                    VOTER_STATUS_TYPE_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    UOCAVA_STATUS_CODE = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    UOCAVA_STATUS_NAME = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    IS_PERM_AV_APP_VOTER = table.Column<bool>(type: "boolean", nullable: false),
                    AddressID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Voters", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Voters_Addresses_AddressID",
                        column: x => x.AddressID,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Voters_MichiganVoterRecords_ID",
                        column: x => x.ID,
                        principalTable: "MichiganVoterRecords",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_CITY",
                table: "Addresses",
                column: "CITY");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_COUNTY_NAME_COUNTY_COMMISSIONER_DISTRICT_NAME",
                table: "Addresses",
                columns: new[] { "COUNTY_NAME", "COUNTY_COMMISSIONER_DISTRICT_NAME" })
                .Annotation("Npgsql:IndexInclude", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_FullStreetAddress",
                table: "Addresses",
                column: "FullStreetAddress");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_JURISDICTION_NAME_PRECINCT",
                table: "Addresses",
                columns: new[] { "JURISDICTION_NAME", "PRECINCT" })
                .Annotation("Npgsql:IndexInclude", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_JURISDICTION_NAME_WARD",
                table: "Addresses",
                columns: new[] { "JURISDICTION_NAME", "WARD" })
                .Annotation("Npgsql:IndexInclude", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_Latitude_Longitude",
                table: "Addresses",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_Matched",
                table: "Addresses",
                column: "Matched");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_SCHOOL_DISTRICT_NAME",
                table: "Addresses",
                column: "SCHOOL_DISTRICT_NAME")
                .Annotation("Npgsql:IndexInclude", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_STATE_HOUSE_DISTRICT_NAME",
                table: "Addresses",
                column: "STATE_HOUSE_DISTRICT_NAME")
                .Annotation("Npgsql:IndexInclude", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_STATE_SENATE_DISTRICT_NAME",
                table: "Addresses",
                column: "STATE_SENATE_DISTRICT_NAME")
                .Annotation("Npgsql:IndexInclude", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_STREET_NAME",
                table: "Addresses",
                column: "STREET_NAME")
                .Annotation("Npgsql:IndexInclude", new[] { "FullStreetAddress" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_US_CONGRESS_DISTRICT_NAME",
                table: "Addresses",
                column: "US_CONGRESS_DISTRICT_NAME")
                .Annotation("Npgsql:IndexInclude", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Voters_AddressID",
                table: "Voters",
                column: "AddressID")
                .Annotation("Npgsql:IndexInclude", new[] { "YEAR_OF_BIRTH", "GENDER", "IS_PERM_AV_BALLOT_VOTER", "REGISTRATION_DATE" });

            migrationBuilder.CreateIndex(
                name: "IX_Voters_FIRST_NAME",
                table: "Voters",
                column: "FIRST_NAME");

            migrationBuilder.CreateIndex(
                name: "IX_Voters_HAS_MAILING_ADDRESS",
                table: "Voters",
                column: "HAS_MAILING_ADDRESS");

            migrationBuilder.CreateIndex(
                name: "IX_Voters_LAST_NAME",
                table: "Voters",
                column: "LAST_NAME");

            migrationBuilder.CreateIndex(
                name: "IX_Voters_MIDDLE_NAME",
                table: "Voters",
                column: "MIDDLE_NAME");

            migrationBuilder.CreateIndex(
                name: "IX_Voters_YEAR_OF_BIRTH",
                table: "Voters",
                column: "YEAR_OF_BIRTH");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Voters");

            migrationBuilder.DropTable(
                name: "Addresses");

            migrationBuilder.DropTable(
                name: "MichiganVoterRecords");
        }
    }
}
