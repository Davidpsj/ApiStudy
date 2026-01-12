using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiStudy.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedMatchAndUserRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Matches",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Matches_UserId",
                table: "Matches",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Users_UserId",
                table: "Matches",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Users_UserId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_UserId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Matches");
        }
    }
}
