using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monitoring.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentUpdateRetryMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastFailureCode",
                table: "AgentUpdateAssignments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                table: "AgentUpdateAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "AgentUpdateAssignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AgentUpdateAssignments_ServerId_ReleaseId_Status",
                table: "AgentUpdateAssignments",
                columns: new[] { "ServerId", "ReleaseId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentUpdateAssignments_ServerId_ReleaseId_Status",
                table: "AgentUpdateAssignments");

            migrationBuilder.DropColumn(
                name: "LastFailureCode",
                table: "AgentUpdateAssignments");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "AgentUpdateAssignments");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "AgentUpdateAssignments");
        }
    }
}
