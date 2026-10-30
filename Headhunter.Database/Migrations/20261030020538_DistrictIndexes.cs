using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Headhunter.Database.Migrations
{
    /// <inheritdoc />
    public partial class DistrictIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "WARD",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "US_CONGRESS_DISTRICT_NAME",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "STATE_SENATE_DISTRICT_NAME",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "STATE_HOUSE_DISTRICT_NAME",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "SCHOOL_DISTRICT_NAME",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "PRECINCT",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "JURISDICTION_NAME",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "COUNTY_NAME",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "COUNTY_COMMISSIONER_DISTRICT_NAME",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                name: "WARD",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "US_CONGRESS_DISTRICT_NAME",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "STATE_SENATE_DISTRICT_NAME",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "STATE_HOUSE_DISTRICT_NAME",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "SCHOOL_DISTRICT_NAME",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "PRECINCT",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "JURISDICTION_NAME",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "COUNTY_NAME",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "COUNTY_COMMISSIONER_DISTRICT_NAME",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
