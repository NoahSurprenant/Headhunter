using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Headhunter.Database.Migrations
{
    /// <inheritdoc />
    public partial class VoteHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Addresses_COUNTY_NAME_COUNTY_COMMISSIONER_DISTRICT_NAME",
                table: "Addresses");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_JURISDICTION_NAME_PRECINCT",
                table: "Addresses");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_JURISDICTION_NAME_WARD",
                table: "Addresses");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_SCHOOL_DISTRICT_NAME",
                table: "Addresses");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_STATE_HOUSE_DISTRICT_NAME",
                table: "Addresses");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_STATE_SENATE_DISTRICT_NAME",
                table: "Addresses");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_US_CONGRESS_DISTRICT_NAME",
                table: "Addresses");

            migrationBuilder.AlterColumn<string>(
                name: "US_CONGRESS_DISTRICT_CODE",
                table: "Addresses",
                type: "varchar(5)",
                unicode: false,
                maxLength: 5,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "STATE_SENATE_DISTRICT_CODE",
                table: "Addresses",
                type: "varchar(5)",
                unicode: false,
                maxLength: 5,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "STATE_HOUSE_DISTRICT_CODE",
                table: "Addresses",
                type: "varchar(5)",
                unicode: false,
                maxLength: 5,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "SCHOOL_DISTRICT_CODE",
                table: "Addresses",
                type: "varchar(5)",
                unicode: false,
                maxLength: 5,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "JURISDICTION_CODE",
                table: "Addresses",
                type: "varchar(5)",
                unicode: false,
                maxLength: 5,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "COUNTY_COMMISSIONER_DISTRICT_CODE",
                table: "Addresses",
                type: "varchar(5)",
                unicode: false,
                maxLength: 5,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "COUNTY_CODE",
                table: "Addresses",
                type: "varchar(2)",
                unicode: false,
                maxLength: 2,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "Counties",
                columns: table => new
                {
                    Code = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Counties", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Elections",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CodeId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Elections", x => x.Date);
                });

            migrationBuilder.CreateTable(
                name: "SchoolDistricts",
                columns: table => new
                {
                    Code = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolDistricts", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "StateHouseDistricts",
                columns: table => new
                {
                    Code = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StateHouseDistricts", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "StateSenateDistricts",
                columns: table => new
                {
                    Code = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StateSenateDistricts", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "UsCongressDistricts",
                columns: table => new
                {
                    Code = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsCongressDistricts", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "CountyCommissionerDistricts",
                columns: table => new
                {
                    CountyCode = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: false),
                    Code = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountyCommissionerDistricts", x => new { x.CountyCode, x.Code });
                    table.ForeignKey(
                        name: "FK_CountyCommissionerDistricts_Counties_CountyCode",
                        column: x => x.CountyCode,
                        principalTable: "Counties",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Jurisdictions",
                columns: table => new
                {
                    CountyCode = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: false),
                    Code = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jurisdictions", x => new { x.CountyCode, x.Code });
                    table.ForeignKey(
                        name: "FK_Jurisdictions_Counties_CountyCode",
                        column: x => x.CountyCode,
                        principalTable: "Counties",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VoteHistory",
                columns: table => new
                {
                    VoterID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ElectionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CountyCode = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    JurisdictionCode = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    SchoolDistrictCode = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoteHistory", x => new { x.VoterID, x.ElectionDate });
                    table.ForeignKey(
                        name: "FK_VoteHistory_Elections_ElectionDate",
                        column: x => x.ElectionDate,
                        principalTable: "Elections",
                        principalColumn: "Date",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoteHistory_Jurisdictions_CountyCode_JurisdictionCode",
                        columns: x => new { x.CountyCode, x.JurisdictionCode },
                        principalTable: "Jurisdictions",
                        principalColumns: new[] { "CountyCode", "Code" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoteHistory_SchoolDistricts_SchoolDistrictCode",
                        column: x => x.SchoolDistrictCode,
                        principalTable: "SchoolDistricts",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoteHistory_Voters_VoterID",
                        column: x => x.VoterID,
                        principalTable: "Voters",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            // Blank codes were stored as '' (and the names beside them); the lookup tables hold each code's name once.
            foreach (var column in new[] { "COUNTY_CODE", "JURISDICTION_CODE", "SCHOOL_DISTRICT_CODE", "STATE_HOUSE_DISTRICT_CODE",
                         "STATE_SENATE_DISTRICT_CODE", "US_CONGRESS_DISTRICT_CODE", "COUNTY_COMMISSIONER_DISTRICT_CODE" })
                migrationBuilder.Sql($"UPDATE [Addresses] SET [{column}] = NULL WHERE [{column}] = ''");

            migrationBuilder.Sql(Fill("Counties", null, "COUNTY_CODE", "COUNTY_NAME"));
            migrationBuilder.Sql(Fill("SchoolDistricts", null, "SCHOOL_DISTRICT_CODE", "SCHOOL_DISTRICT_NAME"));
            migrationBuilder.Sql(Fill("StateHouseDistricts", null, "STATE_HOUSE_DISTRICT_CODE", "STATE_HOUSE_DISTRICT_NAME"));
            migrationBuilder.Sql(Fill("StateSenateDistricts", null, "STATE_SENATE_DISTRICT_CODE", "STATE_SENATE_DISTRICT_NAME"));
            migrationBuilder.Sql(Fill("UsCongressDistricts", null, "US_CONGRESS_DISTRICT_CODE", "US_CONGRESS_DISTRICT_NAME"));
            migrationBuilder.Sql(Fill("Jurisdictions", "COUNTY_CODE", "JURISDICTION_CODE", "JURISDICTION_NAME"));
            migrationBuilder.Sql(Fill("CountyCommissionerDistricts", "COUNTY_CODE", "COUNTY_COMMISSIONER_DISTRICT_CODE", "COUNTY_COMMISSIONER_DISTRICT_NAME"));

            migrationBuilder.DropColumn(
                name: "COUNTY_COMMISSIONER_DISTRICT_NAME",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "COUNTY_NAME",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "JURISDICTION_NAME",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "SCHOOL_DISTRICT_NAME",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "STATE_HOUSE_DISTRICT_NAME",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "STATE_SENATE_DISTRICT_NAME",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "US_CONGRESS_DISTRICT_NAME",
                table: "Addresses");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_COUNTY_CODE_COUNTY_COMMISSIONER_DISTRICT_CODE",
                table: "Addresses",
                columns: new[] { "COUNTY_CODE", "COUNTY_COMMISSIONER_DISTRICT_CODE" })
                .Annotation("SqlServer:Include", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_COUNTY_CODE_JURISDICTION_CODE_PRECINCT",
                table: "Addresses",
                columns: new[] { "COUNTY_CODE", "JURISDICTION_CODE", "PRECINCT" })
                .Annotation("SqlServer:Include", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_COUNTY_CODE_JURISDICTION_CODE_WARD",
                table: "Addresses",
                columns: new[] { "COUNTY_CODE", "JURISDICTION_CODE", "WARD" })
                .Annotation("SqlServer:Include", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_SCHOOL_DISTRICT_CODE",
                table: "Addresses",
                column: "SCHOOL_DISTRICT_CODE")
                .Annotation("SqlServer:Include", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_STATE_HOUSE_DISTRICT_CODE",
                table: "Addresses",
                column: "STATE_HOUSE_DISTRICT_CODE")
                .Annotation("SqlServer:Include", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_STATE_SENATE_DISTRICT_CODE",
                table: "Addresses",
                column: "STATE_SENATE_DISTRICT_CODE")
                .Annotation("SqlServer:Include", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_US_CONGRESS_DISTRICT_CODE",
                table: "Addresses",
                column: "US_CONGRESS_DISTRICT_CODE")
                .Annotation("SqlServer:Include", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Counties_Name",
                table: "Counties",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CountyCommissionerDistricts_Name",
                table: "CountyCommissionerDistricts",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Jurisdictions_Name",
                table: "Jurisdictions",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolDistricts_Name",
                table: "SchoolDistricts",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_StateHouseDistricts_Name",
                table: "StateHouseDistricts",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_StateSenateDistricts_Name",
                table: "StateSenateDistricts",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_UsCongressDistricts_Name",
                table: "UsCongressDistricts",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_VoteHistory_CountyCode_JurisdictionCode",
                table: "VoteHistory",
                columns: new[] { "CountyCode", "JurisdictionCode" });

            migrationBuilder.CreateIndex(
                name: "IX_VoteHistory_ElectionDate",
                table: "VoteHistory",
                column: "ElectionDate");

            migrationBuilder.CreateIndex(
                name: "IX_VoteHistory_SchoolDistrictCode",
                table: "VoteHistory",
                column: "SchoolDistrictCode");

            migrationBuilder.AddForeignKey(
                name: "FK_Addresses_Counties_COUNTY_CODE",
                table: "Addresses",
                column: "COUNTY_CODE",
                principalTable: "Counties",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Addresses_CountyCommissionerDistricts_COUNTY_CODE_COUNTY_COMMISSIONER_DISTRICT_CODE",
                table: "Addresses",
                columns: new[] { "COUNTY_CODE", "COUNTY_COMMISSIONER_DISTRICT_CODE" },
                principalTable: "CountyCommissionerDistricts",
                principalColumns: new[] { "CountyCode", "Code" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Addresses_Jurisdictions_COUNTY_CODE_JURISDICTION_CODE",
                table: "Addresses",
                columns: new[] { "COUNTY_CODE", "JURISDICTION_CODE" },
                principalTable: "Jurisdictions",
                principalColumns: new[] { "CountyCode", "Code" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Addresses_SchoolDistricts_SCHOOL_DISTRICT_CODE",
                table: "Addresses",
                column: "SCHOOL_DISTRICT_CODE",
                principalTable: "SchoolDistricts",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Addresses_StateHouseDistricts_STATE_HOUSE_DISTRICT_CODE",
                table: "Addresses",
                column: "STATE_HOUSE_DISTRICT_CODE",
                principalTable: "StateHouseDistricts",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Addresses_StateSenateDistricts_STATE_SENATE_DISTRICT_CODE",
                table: "Addresses",
                column: "STATE_SENATE_DISTRICT_CODE",
                principalTable: "StateSenateDistricts",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Addresses_UsCongressDistricts_US_CONGRESS_DISTRICT_CODE",
                table: "Addresses",
                column: "US_CONGRESS_DISTRICT_CODE",
                principalTable: "UsCongressDistricts",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        private static string Fill(string table, string countyColumn, string codeColumn, string nameColumn)
        {
            var county = countyColumn is null ? "" : $"[CountyCode], ";
            var countySelect = countyColumn is null ? "" : $"[{countyColumn}], ";
            var countyWhere = countyColumn is null ? "" : $" AND [{countyColumn}] IS NOT NULL";
            var groupBy = countyColumn is null ? $"[{codeColumn}]" : $"[{countyColumn}], [{codeColumn}]";
            return $"INSERT INTO [{table}] ({county}[Code], [Name]) " +
                   $"SELECT {countySelect}[{codeColumn}], LEFT(COALESCE(MIN(NULLIF([{nameColumn}], '')), [{codeColumn}]), 100) " +
                   $"FROM [Addresses] WHERE [{codeColumn}] IS NOT NULL{countyWhere} GROUP BY {groupBy}";
        }

        private static string Restore(string nameColumn, string table, string countyColumn, string codeColumn)
        {
            var county = countyColumn is null ? "" : $" AND l.[CountyCode] = [Addresses].[{countyColumn}]";
            return $"UPDATE [Addresses] SET [{nameColumn}] = COALESCE((SELECT l.[Name] FROM [{table}] l " +
                   $"WHERE l.[Code] = [Addresses].[{codeColumn}]{county}), '')";
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "COUNTY_COMMISSIONER_DISTRICT_NAME",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "COUNTY_NAME",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "JURISDICTION_NAME",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SCHOOL_DISTRICT_NAME",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "STATE_HOUSE_DISTRICT_NAME",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "STATE_SENATE_DISTRICT_NAME",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "US_CONGRESS_DISTRICT_NAME",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // The names go back onto the addresses before the lookup tables that hold them are dropped.
            migrationBuilder.Sql(Restore("COUNTY_NAME", "Counties", null, "COUNTY_CODE"));
            migrationBuilder.Sql(Restore("JURISDICTION_NAME", "Jurisdictions", "COUNTY_CODE", "JURISDICTION_CODE"));
            migrationBuilder.Sql(Restore("SCHOOL_DISTRICT_NAME", "SchoolDistricts", null, "SCHOOL_DISTRICT_CODE"));
            migrationBuilder.Sql(Restore("STATE_HOUSE_DISTRICT_NAME", "StateHouseDistricts", null, "STATE_HOUSE_DISTRICT_CODE"));
            migrationBuilder.Sql(Restore("STATE_SENATE_DISTRICT_NAME", "StateSenateDistricts", null, "STATE_SENATE_DISTRICT_CODE"));
            migrationBuilder.Sql(Restore("US_CONGRESS_DISTRICT_NAME", "UsCongressDistricts", null, "US_CONGRESS_DISTRICT_CODE"));
            migrationBuilder.Sql(Restore("COUNTY_COMMISSIONER_DISTRICT_NAME", "CountyCommissionerDistricts", "COUNTY_CODE", "COUNTY_COMMISSIONER_DISTRICT_CODE"));

            migrationBuilder.DropForeignKey(
                name: "FK_Addresses_Counties_COUNTY_CODE",
                table: "Addresses");

            migrationBuilder.DropForeignKey(
                name: "FK_Addresses_CountyCommissionerDistricts_COUNTY_CODE_COUNTY_COMMISSIONER_DISTRICT_CODE",
                table: "Addresses");

            migrationBuilder.DropForeignKey(
                name: "FK_Addresses_Jurisdictions_COUNTY_CODE_JURISDICTION_CODE",
                table: "Addresses");

            migrationBuilder.DropForeignKey(
                name: "FK_Addresses_SchoolDistricts_SCHOOL_DISTRICT_CODE",
                table: "Addresses");

            migrationBuilder.DropForeignKey(
                name: "FK_Addresses_StateHouseDistricts_STATE_HOUSE_DISTRICT_CODE",
                table: "Addresses");

            migrationBuilder.DropForeignKey(
                name: "FK_Addresses_StateSenateDistricts_STATE_SENATE_DISTRICT_CODE",
                table: "Addresses");

            migrationBuilder.DropForeignKey(
                name: "FK_Addresses_UsCongressDistricts_US_CONGRESS_DISTRICT_CODE",
                table: "Addresses");

            migrationBuilder.DropTable(
                name: "CountyCommissionerDistricts");

            migrationBuilder.DropTable(
                name: "StateHouseDistricts");

            migrationBuilder.DropTable(
                name: "StateSenateDistricts");

            migrationBuilder.DropTable(
                name: "UsCongressDistricts");

            migrationBuilder.DropTable(
                name: "VoteHistory");

            migrationBuilder.DropTable(
                name: "Elections");

            migrationBuilder.DropTable(
                name: "Jurisdictions");

            migrationBuilder.DropTable(
                name: "SchoolDistricts");

            migrationBuilder.DropTable(
                name: "Counties");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_COUNTY_CODE_COUNTY_COMMISSIONER_DISTRICT_CODE",
                table: "Addresses");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_COUNTY_CODE_JURISDICTION_CODE_PRECINCT",
                table: "Addresses");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_COUNTY_CODE_JURISDICTION_CODE_WARD",
                table: "Addresses");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_SCHOOL_DISTRICT_CODE",
                table: "Addresses");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_STATE_HOUSE_DISTRICT_CODE",
                table: "Addresses");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_STATE_SENATE_DISTRICT_CODE",
                table: "Addresses");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_US_CONGRESS_DISTRICT_CODE",
                table: "Addresses");

            migrationBuilder.AlterColumn<string>(
                name: "US_CONGRESS_DISTRICT_CODE",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(5)",
                oldUnicode: false,
                oldMaxLength: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "STATE_SENATE_DISTRICT_CODE",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(5)",
                oldUnicode: false,
                oldMaxLength: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "STATE_HOUSE_DISTRICT_CODE",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(5)",
                oldUnicode: false,
                oldMaxLength: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SCHOOL_DISTRICT_CODE",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(5)",
                oldUnicode: false,
                oldMaxLength: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "JURISDICTION_CODE",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(5)",
                oldUnicode: false,
                oldMaxLength: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "COUNTY_COMMISSIONER_DISTRICT_CODE",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(5)",
                oldUnicode: false,
                oldMaxLength: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "COUNTY_CODE",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(2)",
                oldUnicode: false,
                oldMaxLength: 2,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_COUNTY_NAME_COUNTY_COMMISSIONER_DISTRICT_NAME",
                table: "Addresses",
                columns: new[] { "COUNTY_NAME", "COUNTY_COMMISSIONER_DISTRICT_NAME" })
                .Annotation("SqlServer:Include", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_JURISDICTION_NAME_PRECINCT",
                table: "Addresses",
                columns: new[] { "JURISDICTION_NAME", "PRECINCT" })
                .Annotation("SqlServer:Include", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_JURISDICTION_NAME_WARD",
                table: "Addresses",
                columns: new[] { "JURISDICTION_NAME", "WARD" })
                .Annotation("SqlServer:Include", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_SCHOOL_DISTRICT_NAME",
                table: "Addresses",
                column: "SCHOOL_DISTRICT_NAME")
                .Annotation("SqlServer:Include", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_STATE_HOUSE_DISTRICT_NAME",
                table: "Addresses",
                column: "STATE_HOUSE_DISTRICT_NAME")
                .Annotation("SqlServer:Include", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_STATE_SENATE_DISTRICT_NAME",
                table: "Addresses",
                column: "STATE_SENATE_DISTRICT_NAME")
                .Annotation("SqlServer:Include", new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_US_CONGRESS_DISTRICT_NAME",
                table: "Addresses",
                column: "US_CONGRESS_DISTRICT_NAME")
                .Annotation("SqlServer:Include", new[] { "Latitude", "Longitude" });
        }
    }
}
