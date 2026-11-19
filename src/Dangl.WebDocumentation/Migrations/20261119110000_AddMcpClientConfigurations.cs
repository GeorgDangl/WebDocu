using System;
using Dangl.WebDocumentation.Models;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Dangl.WebDocumentation.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261119110000_AddMcpClientConfigurations")]
public partial class _20261119110000_AddMcpClientConfigurations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "McpClientConfigurations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ClientId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Instructions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                ConfigurationTemplate = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_McpClientConfigurations", x => x.Id));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "McpClientConfigurations");
    }
}
