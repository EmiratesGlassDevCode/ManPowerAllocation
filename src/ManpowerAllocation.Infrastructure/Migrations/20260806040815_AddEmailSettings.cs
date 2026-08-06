using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManpowerAllocation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Port = table.Column<int>(type: "int", nullable: false),
                    Security = table.Column<int>(type: "int", nullable: false),
                    FromAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FromName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Username = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PasswordProtected = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ClientId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ClientSecretProtected = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    SenderMailbox = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Recipients = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Cc = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Bcc = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ReplyTo = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SendAtLocal = table.Column<TimeSpan>(type: "time", nullable: false),
                    AttachPdf = table.Column<bool>(type: "bit", nullable: false),
                    LastSentOperationalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByObjectId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailSettings");
        }
    }
}
