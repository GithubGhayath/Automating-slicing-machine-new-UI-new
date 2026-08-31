using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ElementsInformation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DefaultLife = table.Column<double>(type: "float", nullable: false),
                    LifeUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Price = table.Column<double>(type: "float", nullable: false),
                    Image = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Catalog = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElementsInformation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Elements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    ConsumedLife = table.Column<double>(type: "float", nullable: false),
                    ImageOfElementAtMachine = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OrderOfElementAtMachine = table.Column<int>(type: "int", nullable: false),
                    FailureDetected = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ElementInformationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Elements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Elements_ElementsInformation_ElementInformationId",
                        column: x => x.ElementInformationId,
                        principalTable: "ElementsInformation",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Maintenance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoneBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Cost = table.Column<double>(type: "float", nullable: false),
                    ElementPrice = table.Column<double>(type: "float", nullable: false),
                    StoppingTimeCost = table.Column<double>(type: "float", nullable: false),
                    MaintenanceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ElementId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Maintenance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Maintenance_Elements_ElementId",
                        column: x => x.ElementId,
                        principalTable: "Elements",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Elements_ElementInformationId",
                table: "Elements",
                column: "ElementInformationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Elements_OrderOfElementAtMachine",
                table: "Elements",
                column: "OrderOfElementAtMachine",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Maintenance_ElementId",
                table: "Maintenance",
                column: "ElementId");

            migrationBuilder.CreateIndex(
                name: "IX_Maintenance_MaintenanceDate",
                table: "Maintenance",
                column: "MaintenanceDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Maintenance");

            migrationBuilder.DropTable(
                name: "Elements");

            migrationBuilder.DropTable(
                name: "ElementsInformation");
        }
    }
}
