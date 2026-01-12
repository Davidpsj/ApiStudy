using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiStudy.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchTableStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Players = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MatchFormat = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MatchType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Scores = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlayerEffects = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Matches");
        }
    }
}
