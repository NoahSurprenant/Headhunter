using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Headhunter.Database.Migrations
{
    /// <inheritdoc />
    public partial class VoterAddressIndexCoversClusterMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Voters_AddressID",
                table: "Voters");

            migrationBuilder.CreateIndex(
                name: "IX_Voters_AddressID",
                table: "Voters",
                column: "AddressID")
                .Annotation("SqlServer:Include", new[] { "YEAR_OF_BIRTH", "GENDER", "IS_PERM_AV_BALLOT_VOTER", "REGISTRATION_DATE" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Voters_AddressID",
                table: "Voters");

            migrationBuilder.CreateIndex(
                name: "IX_Voters_AddressID",
                table: "Voters",
                column: "AddressID");
        }
    }
}
