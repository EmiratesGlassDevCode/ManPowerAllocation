using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManpowerAllocation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MasterSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CapturedByObjectId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CapturedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EmployeeCount = table.Column<int>(type: "int", nullable: false),
                    DepartmentCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MasterSnapshotDepartments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SnapshotId = table.Column<long>(type: "bigint", nullable: false),
                    DepartmentName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Division = table.Column<int>(type: "int", nullable: false),
                    RequiredDay = table.Column<int>(type: "int", nullable: false),
                    RequiredNight = table.Column<int>(type: "int", nullable: false),
                    IsPool = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterSnapshotDepartments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MasterSnapshotDepartments_MasterSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "MasterSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MasterSnapshotEmployees",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SnapshotId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BadgeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Division = table.Column<int>(type: "int", nullable: false),
                    HomeDepartmentName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Shift = table.Column<int>(type: "int", nullable: false),
                    IsSupply = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterSnapshotEmployees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MasterSnapshotEmployees_MasterSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "MasterSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MasterSnapshotDepartments_SnapshotId",
                table: "MasterSnapshotDepartments",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_MasterSnapshotEmployees_SnapshotId",
                table: "MasterSnapshotEmployees",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_MasterSnapshots_CapturedAtUtc",
                table: "MasterSnapshots",
                column: "CapturedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MasterSnapshotDepartments");

            migrationBuilder.DropTable(
                name: "MasterSnapshotEmployees");

            migrationBuilder.DropTable(
                name: "MasterSnapshots");
        }
    }
}
