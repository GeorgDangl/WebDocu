using System;
using System.Collections.Generic;
using Microsoft.Data.Entity.Migrations;

namespace Dangl.WebDocumentation.Migrations
{
    public partial class ProjectId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE dbo.UserProjectAccess DROP CONSTRAINT PK_UserProjectAccess");
            migrationBuilder.DropForeignKey(name: "FK_UserProjectAccess_DocumentationProject_ProjectId", table: "UserProjectAccess");
            migrationBuilder.DropForeignKey(name: "FK_IdentityRoleClaim<string>_IdentityRole_RoleId", table: "AspNetRoleClaims");
            migrationBuilder.DropForeignKey(name: "FK_IdentityUserClaim<string>_ApplicationUser_UserId", table: "AspNetUserClaims");
            migrationBuilder.DropForeignKey(name: "FK_IdentityUserLogin<string>_ApplicationUser_UserId", table: "AspNetUserLogins");
            migrationBuilder.DropForeignKey(name: "FK_IdentityUserRole<string>_IdentityRole_RoleId", table: "AspNetUserRoles");
            migrationBuilder.DropForeignKey(name: "FK_IdentityUserRole<string>_ApplicationUser_UserId", table: "AspNetUserRoles");
            migrationBuilder.DropPrimaryKey(name: "PK_DocumentationProject", table: "DocumentationProject");
            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                table: "UserProjectAccess",
                nullable: false);
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "DocumentationProject",
                nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "DocumentationProject",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            migrationBuilder.AddPrimaryKey(
                name: "PK_DocumentationProject",
                table: "DocumentationProject",
                column: "Id");
            migrationBuilder.CreateIndex(
                name: "IX_DocumentationProject_Name",
                table: "DocumentationProject",
                column: "Name",
                unique: true);
            migrationBuilder.AddForeignKey(
                name: "FK_UserProjectAccess_DocumentationProject_ProjectId",
                table: "UserProjectAccess",
                column: "ProjectId",
                principalTable: "DocumentationProject",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "FK_IdentityRoleClaim<string>_IdentityRole_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "FK_IdentityUserClaim<string>_ApplicationUser_UserId",
                table: "AspNetUserClaims",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "FK_IdentityUserLogin<string>_ApplicationUser_UserId",
                table: "AspNetUserLogins",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "FK_IdentityUserRole<string>_IdentityRole_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "FK_IdentityUserRole<string>_ApplicationUser_UserId",
                table: "AspNetUserRoles",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_UserProjectAccess_DocumentationProject_ProjectId", table: "UserProjectAccess");
            migrationBuilder.DropForeignKey(name: "FK_IdentityRoleClaim<string>_IdentityRole_RoleId", table: "AspNetRoleClaims");
            migrationBuilder.DropForeignKey(name: "FK_IdentityUserClaim<string>_ApplicationUser_UserId", table: "AspNetUserClaims");
            migrationBuilder.DropForeignKey(name: "FK_IdentityUserLogin<string>_ApplicationUser_UserId", table: "AspNetUserLogins");
            migrationBuilder.DropForeignKey(name: "FK_IdentityUserRole<string>_IdentityRole_RoleId", table: "AspNetUserRoles");
            migrationBuilder.DropForeignKey(name: "FK_IdentityUserRole<string>_ApplicationUser_UserId", table: "AspNetUserRoles");
            migrationBuilder.DropPrimaryKey(name: "PK_DocumentationProject", table: "DocumentationProject");
            migrationBuilder.DropIndex(name: "IX_DocumentationProject_Name", table: "DocumentationProject");
            migrationBuilder.DropColumn(name: "Id", table: "DocumentationProject");
            migrationBuilder.AlterColumn<string>(
                name: "ProjectId",
                table: "UserProjectAccess",
                nullable: false);
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "DocumentationProject",
                nullable: false);
            migrationBuilder.AddPrimaryKey(
                name: "PK_DocumentationProject",
                table: "DocumentationProject",
                column: "Name");
            migrationBuilder.AddForeignKey(
                name: "FK_UserProjectAccess_DocumentationProject_ProjectId",
                table: "UserProjectAccess",
                column: "ProjectId",
                principalTable: "DocumentationProject",
                principalColumn: "Name",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_IdentityRoleClaim<string>_IdentityRole_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_IdentityUserClaim<string>_ApplicationUser_UserId",
                table: "AspNetUserClaims",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_IdentityUserLogin<string>_ApplicationUser_UserId",
                table: "AspNetUserLogins",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_IdentityUserRole<string>_IdentityRole_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_IdentityUserRole<string>_ApplicationUser_UserId",
                table: "AspNetUserRoles",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
