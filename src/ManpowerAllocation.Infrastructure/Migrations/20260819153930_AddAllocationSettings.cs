using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManpowerAllocation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAllocationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AllocationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    AutoShiftResetEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LastMasterUploadUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastResetMarker = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    LastResetAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByObjectId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllocationSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllocationSettings");
        }
    }
}
