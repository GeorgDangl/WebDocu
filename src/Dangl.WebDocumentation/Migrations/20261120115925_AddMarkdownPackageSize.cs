using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dangl.WebDocumentation.Migrations;

/// <inheritdoc />
public partial class _20261120115925_AddMarkdownPackageSize : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "MarkdownPackageSizeInBytes",
            table: "DocumentationProjectVersions",
            type: "bigint",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MarkdownPackageSizeInBytes",
            table: "DocumentationProjectVersions");
    }
}
