using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace ApiStudy.Migrations
{
    /// <inheritdoc />
    public partial class ScannerSchemaRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MagicCard");

            migrationBuilder.CreateTable(
                name: "OracleCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(512)", nullable: true),
                    EmbeddingUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OracleCards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CardPrintings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OracleCardId = table.Column<Guid>(type: "uuid", nullable: false),
                    SetCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CollectorNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsLatestPrinting = table.Column<bool>(type: "boolean", nullable: false),
                    SetType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardPrintings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardPrintings_OracleCards_OracleCardId",
                        column: x => x.OracleCardId,
                        principalTable: "OracleCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardPrintings_OracleCardId_IsLatestPrinting",
                table: "CardPrintings",
                columns: new[] { "OracleCardId", "IsLatestPrinting" });

            migrationBuilder.CreateIndex(
                name: "IX_CardPrintings_SetCode_CollectorNumber",
                table: "CardPrintings",
                columns: new[] { "SetCode", "CollectorNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_OracleCards_Embedding",
                table: "OracleCards",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_OracleCards_Name",
                table: "OracleCards",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardPrintings");

            migrationBuilder.DropTable(
                name: "OracleCards");

            migrationBuilder.CreateTable(
                name: "MagicCard",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectorNumber = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(512)", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ScryfallId = table.Column<string>(type: "text", nullable: false),
                    SetCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MagicCard", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MagicCard_Embedding",
                table: "MagicCard",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_MagicCard_SetCode_CollectorNumber",
                table: "MagicCard",
                columns: new[] { "SetCode", "CollectorNumber" });
        }
    }
}
