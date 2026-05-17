using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monitoring.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixPm2Logs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Fingerprint",
                table: "PM2Logs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
WITH canonical AS (
    SELECT "Id", "ServerId", "Name",
           FIRST_VALUE("Id") OVER (PARTITION BY "ServerId", "Name" ORDER BY "UpdatedAt" DESC, "CreatedAt" DESC, "Id" DESC) AS "CanonicalId"
    FROM "PM2Processes"
), moved AS (
    UPDATE "PM2Logs" l
    SET "ProcessId" = c."CanonicalId"
    FROM canonical c
    WHERE l."ProcessId" = c."Id" AND c."Id" <> c."CanonicalId"
)
DELETE FROM "PM2Processes" p
USING canonical c
WHERE p."Id" = c."Id" AND c."Id" <> c."CanonicalId";
");

            migrationBuilder.Sql(@"
UPDATE "PM2Logs"
SET "Fingerprint" = md5("ServerId"::text || '|' || "ProcessId"::text || '|' || "StreamType"::text || '|' || "Timestamp"::text || '|' || "RawMessage")
WHERE "Fingerprint" = '';
");

            migrationBuilder.Sql(@"
DELETE FROM "PM2Logs" a
USING "PM2Logs" b
WHERE a."Fingerprint" = b."Fingerprint" AND a."Id" > b."Id";
");

            migrationBuilder.CreateIndex(
                name: "IX_PM2Processes_ServerId_Name",
                table: "PM2Processes",
                columns: new[] { "ServerId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PM2Logs_Fingerprint",
                table: "PM2Logs",
                column: "Fingerprint",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PM2Processes_ServerId_Name",
                table: "PM2Processes");

            migrationBuilder.DropIndex(
                name: "IX_PM2Logs_Fingerprint",
                table: "PM2Logs");

            migrationBuilder.DropColumn(
                name: "Fingerprint",
                table: "PM2Logs");
        }
    }
}
