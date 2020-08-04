using Microsoft.EntityFrameworkCore.Migrations;

namespace Dangl.WebDocumentation.Migrations
{
    public partial class IdPClaimProjectAccess : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SetFromIdentityProviderClaim",
                table: "UserProjects",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SetFromIdentityProviderClaim",
                table: "UserProjects");
        }
    }
}
