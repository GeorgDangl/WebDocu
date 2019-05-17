using Microsoft.EntityFrameworkCore.Migrations;

namespace Dangl.WebDocumentation.Migrations
{
    public partial class Changelog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MarkdownChangelog",
                table: "DocumentationProjectVersions",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarkdownChangelog",
                table: "DocumentationProjectVersions");
        }
    }
}
