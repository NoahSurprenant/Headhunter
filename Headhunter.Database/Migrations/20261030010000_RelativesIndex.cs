using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Headhunter.Database.Migrations
{
    /// <inheritdoc />
    public partial class RelativesIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Addresses_STREET_NAME_CITY",
                table: "Addresses",
                columns: new[] { "STREET_NAME", "CITY" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Addresses_STREET_NAME_CITY",
                table: "Addresses");
        }
    }
}
