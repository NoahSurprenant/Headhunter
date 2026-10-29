using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Headhunter.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSuggestionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MIDDLE_NAME",
                table: "Voters",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "LAST_NAME",
                table: "Voters",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "FIRST_NAME",
                table: "Voters",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "CITY",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Voters_FIRST_NAME",
                table: "Voters",
                column: "FIRST_NAME");

            migrationBuilder.CreateIndex(
                name: "IX_Voters_LAST_NAME",
                table: "Voters",
                column: "LAST_NAME");

            migrationBuilder.CreateIndex(
                name: "IX_Voters_MIDDLE_NAME",
                table: "Voters",
                column: "MIDDLE_NAME");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_CITY",
                table: "Addresses",
                column: "CITY");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Voters_FIRST_NAME",
                table: "Voters");

            migrationBuilder.DropIndex(
                name: "IX_Voters_LAST_NAME",
                table: "Voters");

            migrationBuilder.DropIndex(
                name: "IX_Voters_MIDDLE_NAME",
                table: "Voters");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_CITY",
                table: "Addresses");

            migrationBuilder.AlterColumn<string>(
                name: "MIDDLE_NAME",
                table: "Voters",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "LAST_NAME",
                table: "Voters",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "FIRST_NAME",
                table: "Voters",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "CITY",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
