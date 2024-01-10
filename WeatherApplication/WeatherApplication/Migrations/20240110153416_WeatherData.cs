using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeatherApplication.Migrations
{
    /// <inheritdoc />
    public partial class WeatherData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "WeatherData",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "email",
                table: "WeatherData");
        }
    }
}
