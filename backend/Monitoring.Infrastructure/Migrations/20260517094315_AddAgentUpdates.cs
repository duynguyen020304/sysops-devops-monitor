using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monitoring.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgentBuildId",
                table: "Servers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentCapabilitiesJson",
                table: "Servers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentUpdateStatus",
                table: "Servers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentUpdateReleases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BuildId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Channel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GitSha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ManifestJson = table.Column<string>(type: "text", nullable: false),
                    ManifestSignature = table.Column<string>(type: "text", nullable: false),
                    PublicKeyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ArtifactPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ArtifactSha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ArtifactSize = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentUpdateReleases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentUpdateAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReleaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FromVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    FromBuildId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentUpdateAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentUpdateAssignments_AgentUpdateReleases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "AgentUpdateReleases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentUpdateAssignments_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentUpdateEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentUpdateEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentUpdateEvents_AgentUpdateAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "AgentUpdateAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentUpdateAssignments_ReleaseId",
                table: "AgentUpdateAssignments",
                column: "ReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentUpdateAssignments_ServerId_Status",
                table: "AgentUpdateAssignments",
                columns: new[] { "ServerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentUpdateEvents_AssignmentId",
                table: "AgentUpdateEvents",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentUpdateEvents_ServerId_Timestamp",
                table: "AgentUpdateEvents",
                columns: new[] { "ServerId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentUpdateReleases_WorkspaceId_Channel_IsActive",
                table: "AgentUpdateReleases",
                columns: new[] { "WorkspaceId", "Channel", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentUpdateEvents");

            migrationBuilder.DropTable(
                name: "AgentUpdateAssignments");

            migrationBuilder.DropTable(
                name: "AgentUpdateReleases");

            migrationBuilder.DropColumn(
                name: "AgentBuildId",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "AgentCapabilitiesJson",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "AgentUpdateStatus",
                table: "Servers");
        }
    }
}
