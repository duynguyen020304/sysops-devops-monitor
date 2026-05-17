using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monitoring.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemdMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemdServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LoadState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActiveState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SubState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    FragmentPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    MainPid = table.Column<int>(type: "integer", nullable: true),
                    MemoryCurrent = table.Column<long>(type: "bigint", nullable: true),
                    CpuUsageNSec = table.Column<long>(type: "bigint", nullable: true),
                    RestartCount = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemdServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemdServices_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemdLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: true),
                    Level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    RawJson = table.Column<string>(type: "text", nullable: false),
                    Cursor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    BootId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemdLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemdLogs_SystemdServices_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "SystemdServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemdLogs_Cursor",
                table: "SystemdLogs",
                column: "Cursor");

            migrationBuilder.CreateIndex(
                name: "IX_SystemdLogs_Fingerprint",
                table: "SystemdLogs",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemdLogs_ServerId_Timestamp",
                table: "SystemdLogs",
                columns: new[] { "ServerId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemdLogs_ServiceId_Timestamp",
                table: "SystemdLogs",
                columns: new[] { "ServiceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemdServices_ServerId_Name",
                table: "SystemdServices",
                columns: new[] { "ServerId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemdLogs");

            migrationBuilder.DropTable(
                name: "SystemdServices");
        }
    }
}
