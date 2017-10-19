using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace Dangl.WebDocumentation.Migrations
{
    public partial class DocVersions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_DocumentationProjects_Name",
                table: "DocumentationProjects",
                column: "Name");

            migrationBuilder.CreateTable(
                name: "DocumentationProjectVersionss",
                columns: table => new
                {
                    ProjectName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentationProjectVersionss", x => new { x.ProjectName, x.Version });
                    table.ForeignKey(
                        name: "FK_DocumentationProjectVersionss_DocumentationProjects_ProjectName",
                        column: x => x.ProjectName,
                        principalTable: "DocumentationProjects",
                        principalColumn: "Name",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentationProjectVersionss_ProjectName",
                table: "DocumentationProjectVersionss",
                column: "ProjectName");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentationProjectVersionss_Version",
                table: "DocumentationProjectVersionss",
                column: "Version");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentationProjectVersionss");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_DocumentationProjects_Name",
                table: "DocumentationProjects");
        }
    }
}
