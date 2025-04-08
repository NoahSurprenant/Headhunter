using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Headhunter.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MichiganVoterRecords",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LAST_NAME = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FIRST_NAME = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MIDDLE_NAME = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NAME_SUFFIX = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    YEAR_OF_BIRTH = table.Column<int>(type: "int", nullable: false),
                    GENDER = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    REGISTRATION_DATE = table.Column<DateTime>(type: "datetime2", nullable: false),
                    STREET_NUMBER_PREFIX = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    STREET_NUMBER = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    STREET_NUMBER_SUFFIX = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DIRECTION_PREFIX = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    STREET_NAME = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    STREET_TYPE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DIRECTION_SUFFIX = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EXTENSION = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CITY = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    STATE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ZIP_CODE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MAILING_ADDRESS_LINE_ONE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MAILING_ADDRESS_LINE_TWO = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MAILING_ADDRESS_LINE_THREE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MAILING_ADDRESS_LINE_FOUR = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MAILING_ADDRESS_LINE_FIVE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VOTER_IDENTIFICATION_NUMBER = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    COUNTY_CODE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    COUNTY_NAME = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JURISDICTION_CODE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JURISDICTION_NAME = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PRECINCT = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WARD = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SCHOOL_DISTRICT_CODE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SCHOOL_DISTRICT_NAME = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    STATE_HOUSE_DISTRICT_CODE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    STATE_HOUSE_DISTRICT_NAME = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    STATE_SENATE_DISTRICT_CODE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    STATE_SENATE_DISTRICT_NAME = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    US_CONGRESS_DISTRICT_CODE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    US_CONGRESS_DISTRICT_NAME = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    COUNTY_COMMISSIONER_DISTRICT_CODE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    COUNTY_COMMISSIONER_DISTRICT_NAME = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VILLAGE_DISTRICT_CODE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VILLAGE_DISTRICT_NAME = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VILLAGE_PRECINCT = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SCHOOL_PRECINCT = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IS_PERM_AV_BALLOT_VOTER = table.Column<bool>(type: "bit", nullable: false),
                    VOTER_STATUS_TYPE_CODE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UOCAVA_STATUS_CODE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UOCAVA_STATUS_NAME = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IS_PERM_AV_APP_VOTER = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MichiganVoterRecords", x => x.ID);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MichiganVoterRecords");
        }
    }
}
