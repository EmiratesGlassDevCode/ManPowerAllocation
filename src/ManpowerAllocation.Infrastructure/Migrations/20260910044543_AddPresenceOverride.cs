using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManpowerAllocation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPresenceOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PresenceOverrideReason",
                table: "Employees",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PresenceOverrideUntil",
                table: "Employees",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PresenceOverrideReason",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PresenceOverrideUntil",
                table: "Employees");
        }
    }
}
