using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManpowerAllocation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAllocationSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AllocationSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Shift = table.Column<int>(type: "int", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OnRoll = table.Column<int>(type: "int", nullable: false),
                    Present = table.Column<int>(type: "int", nullable: false),
                    Absent = table.Column<int>(type: "int", nullable: false),
                    OnVacation = table.Column<int>(type: "int", nullable: false),
                    SupplyPresent = table.Column<int>(type: "int", nullable: false),
                    TotalPresent = table.Column<int>(type: "int", nullable: false),
                    Required = table.Column<int>(type: "int", nullable: false),
                    Variance = table.Column<int>(type: "int", nullable: false),
                    ShortageDepartmentCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllocationSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AllocationSnapshotDepartments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SnapshotId = table.Column<long>(type: "bigint", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    Division = table.Column<int>(type: "int", nullable: false),
                    DepartmentName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Required = table.Column<int>(type: "int", nullable: false),
                    OnRoll = table.Column<int>(type: "int", nullable: false),
                    Present = table.Column<int>(type: "int", nullable: false),
                    Absent = table.Column<int>(type: "int", nullable: false),
                    OnVacation = table.Column<int>(type: "int", nullable: false),
                    SupplyPresent = table.Column<int>(type: "int", nullable: false),
                    TotalPresent = table.Column<int>(type: "int", nullable: false),
                    Variance = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllocationSnapshotDepartments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllocationSnapshotDepartments_AllocationSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "AllocationSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AllocationSnapshotEmployees",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SnapshotId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BadgeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Division = table.Column<int>(type: "int", nullable: false),
                    DepartmentName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Shift = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsSupply = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllocationSnapshotEmployees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllocationSnapshotEmployees_AllocationSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "AllocationSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AllocationSnapshots_OperationalDate_Shift",
                table: "AllocationSnapshots",
                columns: new[] { "OperationalDate", "Shift" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AllocationSnapshotDepartments_SnapshotId",
                table: "AllocationSnapshotDepartments",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationSnapshotEmployees_SnapshotId",
                table: "AllocationSnapshotEmployees",
                column: "SnapshotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllocationSnapshotDepartments");

            migrationBuilder.DropTable(
                name: "AllocationSnapshotEmployees");

            migrationBuilder.DropTable(
                name: "AllocationSnapshots");
        }
    }
}
