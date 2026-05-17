using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripWise.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightDetailsToFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Baggage",
                table: "FavoriteFlights",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FlightClass",
                table: "FavoriteFlights",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandLuggage",
                table: "FavoriteFlights",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Meal",
                table: "FavoriteFlights",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Baggage",
                table: "FavoriteFlights");

            migrationBuilder.DropColumn(
                name: "FlightClass",
                table: "FavoriteFlights");

            migrationBuilder.DropColumn(
                name: "HandLuggage",
                table: "FavoriteFlights");

            migrationBuilder.DropColumn(
                name: "Meal",
                table: "FavoriteFlights");
        }
    }
}
