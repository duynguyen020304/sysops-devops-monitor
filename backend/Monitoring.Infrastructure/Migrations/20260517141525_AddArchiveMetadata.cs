using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monitoring.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArchiveMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "Servers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Servers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MachineId",
                table: "Servers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "AgentInstallTokens",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "AgentInstallTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Servers_WorkspaceId_Hostname_ArchivedAt",
                table: "Servers",
                columns: new[] { "WorkspaceId", "Hostname", "ArchivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Servers_WorkspaceId_MachineId_ArchivedAt",
                table: "Servers",
                columns: new[] { "WorkspaceId", "MachineId", "ArchivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentInstallTokens_WorkspaceId_ServerName_ArchivedAt",
                table: "AgentInstallTokens",
                columns: new[] { "WorkspaceId", "ServerName", "ArchivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Servers_WorkspaceId_Hostname_ArchivedAt",
                table: "Servers");

            migrationBuilder.DropIndex(
                name: "IX_Servers_WorkspaceId_MachineId_ArchivedAt",
                table: "Servers");

            migrationBuilder.DropIndex(
                name: "IX_AgentInstallTokens_WorkspaceId_ServerName_ArchivedAt",
                table: "AgentInstallTokens");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "MachineId",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "AgentInstallTokens");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "AgentInstallTokens");
        }
    }
}
