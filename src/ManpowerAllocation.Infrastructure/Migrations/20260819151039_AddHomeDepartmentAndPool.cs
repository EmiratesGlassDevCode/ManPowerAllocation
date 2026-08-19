using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManpowerAllocation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeDepartmentAndPool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HomeDepartmentId",
                table: "Employees",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPool",
                table: "Departments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_HomeDepartmentId",
                table: "Employees",
                column: "HomeDepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Departments_HomeDepartmentId",
                table: "Employees",
                column: "HomeDepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Departments_HomeDepartmentId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_HomeDepartmentId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "HomeDepartmentId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IsPool",
                table: "Departments");
        }
    }
}
