using Microsoft.EntityFrameworkCore.Migrations;

namespace Dangl.WebDocumentation.Migrations
{
    public partial class SchemaCleanup : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentationProjectVersionss_DocumentationProjects_ProjectName",
                table: "DocumentationProjectVersionss");

            migrationBuilder.DropIndex(
                name: "IX_DocumentationProjects_Name",
                table: "DocumentationProjects");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DocumentationProjectVersionss",
                table: "DocumentationProjectVersionss");

            migrationBuilder.RenameTable(
                name: "DocumentationProjectVersionss",
                newName: "DocumentationProjectVersions");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentationProjectVersionss_Version",
                table: "DocumentationProjectVersions",
                newName: "IX_DocumentationProjectVersions_Version");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentationProjectVersionss_ProjectName",
                table: "DocumentationProjectVersions",
                newName: "IX_DocumentationProjectVersions_ProjectName");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DocumentationProjectVersions",
                table: "DocumentationProjectVersions",
                columns: new[] { "ProjectName", "Version" });

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentationProjectVersions_DocumentationProjects_ProjectName",
                table: "DocumentationProjectVersions",
                column: "ProjectName",
                principalTable: "DocumentationProjects",
                principalColumn: "Name",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentationProjectVersions_DocumentationProjects_ProjectName",
                table: "DocumentationProjectVersions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DocumentationProjectVersions",
                table: "DocumentationProjectVersions");

            migrationBuilder.RenameTable(
                name: "DocumentationProjectVersions",
                newName: "DocumentationProjectVersionss");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentationProjectVersions_Version",
                table: "DocumentationProjectVersionss",
                newName: "IX_DocumentationProjectVersionss_Version");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentationProjectVersions_ProjectName",
                table: "DocumentationProjectVersionss",
                newName: "IX_DocumentationProjectVersionss_ProjectName");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DocumentationProjectVersionss",
                table: "DocumentationProjectVersionss",
                columns: new[] { "ProjectName", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentationProjects_Name",
                table: "DocumentationProjects",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentationProjectVersionss_DocumentationProjects_ProjectName",
                table: "DocumentationProjectVersionss",
                column: "ProjectName",
                principalTable: "DocumentationProjects",
                principalColumn: "Name",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
