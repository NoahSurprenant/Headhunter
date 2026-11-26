using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Headhunter.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class VoteHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Counties",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(2)", unicode: false, maxLength: 2, nullable: false, collation: "C"),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive")
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
                    CodeId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Elections", x => x.Date);
                });

            migrationBuilder.CreateTable(
                name: "SchoolDistricts",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(5)", unicode: false, maxLength: 5, nullable: false, collation: "C"),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolDistricts", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Jurisdictions",
                columns: table => new
                {
                    CountyCode = table.Column<string>(type: "character varying(2)", unicode: false, maxLength: 2, nullable: false, collation: "C"),
                    Code = table.Column<string>(type: "character varying(5)", unicode: false, maxLength: 5, nullable: false, collation: "C"),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive")
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
                    VoterID = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CountyCode = table.Column<string>(type: "character varying(2)", unicode: false, maxLength: 2, nullable: true, collation: "C"),
                    JurisdictionCode = table.Column<string>(type: "character varying(5)", unicode: false, maxLength: 5, nullable: true, collation: "C"),
                    SchoolDistrictCode = table.Column<string>(type: "character varying(5)", unicode: false, maxLength: 5, nullable: true, collation: "C")
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
