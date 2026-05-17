using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monitoring.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowLogLineNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LineNumber",
                table: "WorkflowLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
                WITH numbered AS (
                    SELECT ""Id"", ROW_NUMBER() OVER (PARTITION BY ""WorkflowRunId"" ORDER BY ""Timestamp"", ""CreatedAt"", ""Id"") AS rn
                    FROM ""WorkflowLogs""
                )
                UPDATE ""WorkflowLogs"" l
                SET ""LineNumber"" = numbered.rn
                FROM numbered
                WHERE l.""Id"" = numbered.""Id"";");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowLogs_WorkflowRunId_LineNumber",
                table: "WorkflowLogs",
                columns: new[] { "WorkflowRunId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowLogs_WorkflowRunId_LineNumber_Id",
                table: "WorkflowLogs",
                columns: new[] { "WorkflowRunId", "LineNumber", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowLogs_WorkflowRunId_LineNumber",
                table: "WorkflowLogs");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowLogs_WorkflowRunId_LineNumber_Id",
                table: "WorkflowLogs");

            migrationBuilder.DropColumn(
                name: "LineNumber",
                table: "WorkflowLogs");
        }
    }
}
