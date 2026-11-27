using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Headhunter.Database.Migrations
{
    /// <inheritdoc />
    public partial class DropVoteHistoryPlaceCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VoteHistory_Jurisdictions_CountyCode_JurisdictionCode",
                table: "VoteHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_VoteHistory_SchoolDistricts_SchoolDistrictCode",
                table: "VoteHistory");

            migrationBuilder.DropIndex(
                name: "IX_VoteHistory_CountyCode_JurisdictionCode",
                table: "VoteHistory");

            migrationBuilder.DropIndex(
                name: "IX_VoteHistory_SchoolDistrictCode",
                table: "VoteHistory");

            migrationBuilder.DropColumn(
                name: "CountyCode",
                table: "VoteHistory");

            migrationBuilder.DropColumn(
                name: "JurisdictionCode",
                table: "VoteHistory");

            migrationBuilder.DropColumn(
                name: "SchoolDistrictCode",
                table: "VoteHistory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CountyCode",
                table: "VoteHistory",
                type: "varchar(2)",
                unicode: false,
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JurisdictionCode",
                table: "VoteHistory",
                type: "varchar(5)",
                unicode: false,
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchoolDistrictCode",
                table: "VoteHistory",
                type: "varchar(5)",
                unicode: false,
                maxLength: 5,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VoteHistory_CountyCode_JurisdictionCode",
                table: "VoteHistory",
                columns: new[] { "CountyCode", "JurisdictionCode" });

            migrationBuilder.CreateIndex(
                name: "IX_VoteHistory_SchoolDistrictCode",
                table: "VoteHistory",
                column: "SchoolDistrictCode");

            migrationBuilder.AddForeignKey(
                name: "FK_VoteHistory_Jurisdictions_CountyCode_JurisdictionCode",
                table: "VoteHistory",
                columns: new[] { "CountyCode", "JurisdictionCode" },
                principalTable: "Jurisdictions",
                principalColumns: new[] { "CountyCode", "Code" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VoteHistory_SchoolDistricts_SchoolDistrictCode",
                table: "VoteHistory",
                column: "SchoolDistrictCode",
                principalTable: "SchoolDistricts",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
