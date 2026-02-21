using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace ApiStudy.Migrations
{
    /// <inheritdoc />
    public partial class EmbeddingMoveToCardPrinting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Adiciona colunas de embedding em CardPrintings ─────────────
            migrationBuilder.AddColumn<Vector>(
                name: "Embedding",
                table: "CardPrintings",
                type: "vector(512)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmbeddingUpdatedAt",
                table: "CardPrintings",
                type: "timestamp with time zone",
                nullable: true);

            // ── 2. Copia embeddings existentes de OracleCards para a impressão mais recente ──
            //
            // ESTRATÉGIA:
            //   Cada OracleCard tem um embedding gerado a partir da imagem da IsLatestPrinting.
            //   Portanto, copiamos o embedding do OracleCard para o CardPrinting correspondente.
            //   As demais impressões (não-latest) ficam com Embedding = null e serão reprocessadas
            //   pelo SeedBackgroundService com o crop da arte específica de cada uma.
            //
            // NOTA: o embed copiado foi gerado da carta inteira (design anterior).
            //   Ele tem qualidade inferior ao novo embed (apenas arte).
            //   O SeedBackgroundService re-processará estas impressões em background,
            //   mas o sistema funcionará imediatamente após a migration com os dados existentes.
            migrationBuilder.Sql(@"
                UPDATE ""CardPrintings"" cp
                SET
                    ""Embedding"" = oc.""Embedding"",
                    ""EmbeddingUpdatedAt"" = oc.""EmbeddingUpdatedAt""
                FROM ""OracleCards"" oc
                WHERE cp.""OracleCardId"" = oc.""Id""
                  AND cp.""IsLatestPrinting"" = true
                  AND oc.""Embedding"" IS NOT NULL;
            ");

            // ── 3. Cria índice HNSW em CardPrintings.Embedding ────────────────
            //
            // Por que HNSW e não IVFFlat?
            //   HNSW (Hierarchical Navigable Small World) é mais preciso e não requer
            //   um número mínimo de vetores para construção do índice.
            //   IVFFlat requer pelo menos nlists×k vetores (tipicamente 10k+) para funcionar bem.
            //
            // vector_cosine_ops: usa distância cossenoidal (<=>) — adequada para embeddings
            //   de redes neurais onde a direção do vetor importa mais que a magnitude.
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS
                    ""IX_CardPrintings_Embedding_hnsw""
                ON ""CardPrintings""
                USING hnsw (""Embedding"" vector_cosine_ops);
            ");

            // ── 4. Remove o índice HNSW antigo de OracleCards ────────────────
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_OracleCards_Embedding"";
            ");

            // ── 5. Remove colunas de embedding de OracleCards ─────────────────
            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "OracleCards");

            migrationBuilder.DropColumn(
                name: "EmbeddingUpdatedAt",
                table: "OracleCards");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── Reverte: restaura colunas em OracleCards ──────────────────────
            migrationBuilder.AddColumn<Vector>(
                name: "Embedding",
                table: "OracleCards",
                type: "vector(512)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmbeddingUpdatedAt",
                table: "OracleCards",
                type: "timestamp with time zone",
                nullable: true);

            // Copia de volta da impressão mais recente para OracleCard
            migrationBuilder.Sql(@"
                UPDATE ""OracleCards"" oc
                SET
                    ""Embedding"" = cp.""Embedding"",
                    ""EmbeddingUpdatedAt"" = cp.""EmbeddingUpdatedAt""
                FROM ""CardPrintings"" cp
                WHERE cp.""OracleCardId"" = oc.""Id""
                  AND cp.""IsLatestPrinting"" = true
                  AND cp.""Embedding"" IS NOT NULL;
            ");

            // Recria índice HNSW em OracleCards
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS
                    ""IX_OracleCards_Embedding""
                ON ""OracleCards""
                USING hnsw (""Embedding"" vector_cosine_ops);
            ");

            // Remove de CardPrintings
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_CardPrintings_Embedding_hnsw"";
            ");

            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "EmbeddingUpdatedAt",
                table: "CardPrintings");
        }
    }
}