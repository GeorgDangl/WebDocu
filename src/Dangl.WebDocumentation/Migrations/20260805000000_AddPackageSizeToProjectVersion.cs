using Microsoft.EntityFrameworkCore.Migrations;

namespace Dangl.WebDocumentation.Migrations
{
    public partial class AddPackageSizeToProjectVersion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PackageSizeInBytes",
                table: "DocumentationProjectVersions",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PackageSizeInBytes",
                table: "DocumentationProjectVersions");
        }
    }
}
