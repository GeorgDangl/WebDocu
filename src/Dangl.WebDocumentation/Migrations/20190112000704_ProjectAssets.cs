using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dangl.WebDocumentation.Migrations
{
    public partial class ProjectAssets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectVersionAssetFiles",
                columns: table => new
                {
                    FileId = table.Column<Guid>(nullable: false),
                    ProjectName = table.Column<string>(maxLength: 60, nullable: true),
                    Version = table.Column<string>(maxLength: 40, nullable: true),
                    FileName = table.Column<string>(maxLength: 256, nullable: false),
                    FileSizeInBytes = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectVersionAssetFiles", x => x.FileId);
                    table.ForeignKey(
                        name: "FK_ProjectVersionAssetFiles_DocumentationProjectVersions_ProjectName_Version",
                        columns: x => new { x.ProjectName, x.Version },
                        principalTable: "DocumentationProjectVersions",
                        principalColumns: new[] { "ProjectName", "Version" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersionAssetFiles_FileName",
                table: "ProjectVersionAssetFiles",
                column: "FileName");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersionAssetFiles_ProjectName",
                table: "ProjectVersionAssetFiles",
                column: "ProjectName");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersionAssetFiles_Version",
                table: "ProjectVersionAssetFiles",
                column: "Version");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersionAssetFiles_ProjectName_Version",
                table: "ProjectVersionAssetFiles",
                columns: new[] { "ProjectName", "Version" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectVersionAssetFiles");
        }
    }
}
