using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripWise.Migrations
{
    /// <inheritdoc />
    public partial class AddPassengersJsonToTrainOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Только добавление PassengersJson
            migrationBuilder.AddColumn<string>(
                name: "PassengersJson",
                table: "TrainOrders",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Только удаление PassengersJson
            migrationBuilder.DropColumn(
                name: "PassengersJson",
                table: "TrainOrders");
        }
    }
}