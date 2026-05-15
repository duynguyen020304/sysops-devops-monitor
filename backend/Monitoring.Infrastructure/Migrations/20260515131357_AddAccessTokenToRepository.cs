using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monitoring.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessTokenToRepository : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessToken",
                table: "Repositories",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessToken",
                table: "Repositories");
        }
    }
}
