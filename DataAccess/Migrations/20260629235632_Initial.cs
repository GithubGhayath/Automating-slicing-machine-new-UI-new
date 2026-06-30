using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConstantValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ElectrcityPricePerKiloWatt = table.Column<double>(type: "float", nullable: false),
                    FeesPerCubicMetter = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstantValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CriticalValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StadiedTheta = table.Column<double>(type: "float", nullable: false),
                    CuttingForce = table.Column<double>(type: "float", nullable: false),
                    ActiveForce = table.Column<double>(type: "float", nullable: false),
                    FrictionForceOnRake = table.Column<double>(type: "float", nullable: false),
                    ThrustForce = table.Column<double>(type: "float", nullable: false),
                    ShearForce = table.Column<double>(type: "float", nullable: false),
                    NormalForceToShear = table.Column<double>(type: "float", nullable: false),
                    NormalForceToRake = table.Column<double>(type: "float", nullable: false),
                    CuttingMoment = table.Column<double>(type: "float", nullable: false),
                    FrictionAngle = table.Column<double>(type: "float", nullable: false),
                    ShearAngle = table.Column<double>(type: "float", nullable: false),
                    FrictionCorrectionCofficient = table.Column<double>(type: "float", nullable: false),
                    EnterAngle = table.Column<double>(type: "float", nullable: false),
                    LeavingAngle = table.Column<double>(type: "float", nullable: false),
                    CenterAngle = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriticalValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationConditions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CuttingSpeed = table.Column<double>(type: "float", nullable: false),
                    FeedRate = table.Column<double>(type: "float", nullable: false),
                    SheftSpeed = table.Column<double>(type: "float", nullable: false),
                    ConsumedElectricity = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationConditions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionConditions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductWidth = table.Column<int>(type: "int", nullable: false),
                    ProductHeight = table.Column<int>(type: "int", nullable: false),
                    ProductionVolume = table.Column<double>(type: "float", nullable: false),
                    TotalFees = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionConditions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WoodTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ShearYieldStressInMpa = table.Column<double>(type: "float", nullable: false),
                    SpecificWorkToSurfaceSeparationJoulPerMeter2 = table.Column<double>(type: "float", nullable: false),
                    CoefficientOfFriction = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WoodTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationsProcesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WoodTypeId = table.Column<int>(type: "int", nullable: false),
                    OperationConditionId = table.Column<int>(type: "int", nullable: false),
                    ProductionConditionId = table.Column<int>(type: "int", nullable: false),
                    CriticalValuesId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationsProcesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationsProcesses_CriticalValues_CriticalValuesId",
                        column: x => x.CriticalValuesId,
                        principalTable: "CriticalValues",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OperationsProcesses_OperationConditions_OperationConditionId",
                        column: x => x.OperationConditionId,
                        principalTable: "OperationConditions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OperationsProcesses_ProductionConditions_ProductionConditionId",
                        column: x => x.ProductionConditionId,
                        principalTable: "ProductionConditions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OperationsProcesses_WoodTypes_WoodTypeId",
                        column: x => x.WoodTypeId,
                        principalTable: "WoodTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationsProcesses_CriticalValuesId",
                table: "OperationsProcesses",
                column: "CriticalValuesId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationsProcesses_OperationConditionId",
                table: "OperationsProcesses",
                column: "OperationConditionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationsProcesses_ProductionConditionId",
                table: "OperationsProcesses",
                column: "ProductionConditionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationsProcesses_WoodTypeId",
                table: "OperationsProcesses",
                column: "WoodTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WoodTypes_Type_Category",
                table: "WoodTypes",
                columns: new[] { "Type", "Category" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConstantValues");

            migrationBuilder.DropTable(
                name: "OperationsProcesses");

            migrationBuilder.DropTable(
                name: "CriticalValues");

            migrationBuilder.DropTable(
                name: "OperationConditions");

            migrationBuilder.DropTable(
                name: "ProductionConditions");

            migrationBuilder.DropTable(
                name: "WoodTypes");
        }
    }
}
