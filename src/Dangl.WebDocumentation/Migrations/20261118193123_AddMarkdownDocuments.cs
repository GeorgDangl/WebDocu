using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dangl.WebDocumentation.Migrations;

/// <inheritdoc />
public partial class _20261118193123_AddMarkdownDocuments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "MarkdownRevision",
            table: "DocumentationProjectVersions",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "MarkdownDocuments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                Version = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Path = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                Content = table.Column<string>(type: "nvarchar(max)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MarkdownDocuments", x => x.Id);
                table.ForeignKey(
                    name: "FK_MarkdownDocuments_DocumentationProjectVersions_ProjectName_Version",
                    columns: x => new { x.ProjectName, x.Version },
                    principalTable: "DocumentationProjectVersions",
                    principalColumns: new[] { "ProjectName", "Version" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MarkdownDocuments_ProjectName_Version_Path",
            table: "MarkdownDocuments",
            columns: new[] { "ProjectName", "Version", "Path" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MarkdownDocuments");
        migrationBuilder.DropColumn(
            name: "MarkdownRevision",
            table: "DocumentationProjectVersions");
    }
}
