using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNetworkFloorMaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NetworkFloorMaps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ImageContentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ImageBytes = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkFloorMaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NetworkAccessPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FloorMapId = table.Column<int>(type: "int", nullable: false),
                    Hostname = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    X = table.Column<double>(type: "float", nullable: false),
                    Y = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkAccessPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkAccessPoints_NetworkFloorMaps_FloorMapId",
                        column: x => x.FloorMapId,
                        principalTable: "NetworkFloorMaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NetworkAccessPoints_FloorMapId",
                table: "NetworkAccessPoints",
                column: "FloorMapId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NetworkAccessPoints");

            migrationBuilder.DropTable(
                name: "NetworkFloorMaps");
        }
    }
}
