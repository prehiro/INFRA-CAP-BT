using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRackSizeU : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SizeU",
                table: "NetworkRacks",
                type: "int",
                nullable: false,
                // Existing racks predate the column, so they must come back as 42U cabinets rather
                // than 0U ones - a 0U rack would make the 3D viewer render a zero-height cabinet.
                defaultValue: 42);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SizeU",
                table: "NetworkRacks");
        }
    }
}
