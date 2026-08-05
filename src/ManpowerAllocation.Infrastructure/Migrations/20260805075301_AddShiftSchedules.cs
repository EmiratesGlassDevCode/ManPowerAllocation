using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ManpowerAllocation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ShiftScheduleId",
                table: "Departments",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "ShiftSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DayStart = table.Column<TimeSpan>(type: "time", nullable: false),
                    GraceMinutes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftSchedules", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ShiftSchedules",
                columns: new[] { "Id", "DayStart", "GraceMinutes", "Name" },
                values: new object[,]
                {
                    { 1, new TimeSpan(0, 7, 0, 0, 0), 60, "07:00 – 19:00" },
                    { 2, new TimeSpan(0, 6, 0, 0, 0), 60, "06:00 – 18:00" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Departments_ShiftScheduleId",
                table: "Departments",
                column: "ShiftScheduleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_ShiftSchedules_ShiftScheduleId",
                table: "Departments",
                column: "ShiftScheduleId",
                principalTable: "ShiftSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_ShiftSchedules_ShiftScheduleId",
                table: "Departments");

            migrationBuilder.DropTable(
                name: "ShiftSchedules");

            migrationBuilder.DropIndex(
                name: "IX_Departments_ShiftScheduleId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "ShiftScheduleId",
                table: "Departments");
        }
    }
}
