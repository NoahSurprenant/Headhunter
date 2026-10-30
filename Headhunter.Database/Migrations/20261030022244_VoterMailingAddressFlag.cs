using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Headhunter.Database.Migrations
{
    /// <inheritdoc />
    public partial class VoterMailingAddressFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HAS_MAILING_ADDRESS",
                table: "Voters",
                type: "bit",
                nullable: false,
                computedColumnSql: "(CASE WHEN [MAILING_ADDRESS_LINE_ONE] <> N'' THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END)",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Voters_HAS_MAILING_ADDRESS",
                table: "Voters",
                column: "HAS_MAILING_ADDRESS");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Voters_HAS_MAILING_ADDRESS",
                table: "Voters");

            migrationBuilder.DropColumn(
                name: "HAS_MAILING_ADDRESS",
                table: "Voters");
        }
    }
}
