using ApiStudy.Models.Scanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApiStudy.Mappers;

public class CardPrintingMap : IEntityTypeConfiguration<CardPrinting>
{
    public void Configure(EntityTypeBuilder<CardPrinting> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("CardPrintings");

        builder.Property(x => x.SetCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.CollectorNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.ImageUrl).IsRequired(false);
        builder.Property(x => x.SetType).HasMaxLength(50);
        builder.Property(x => x.ReleasedAt).IsRequired();
        builder.Property(x => x.IsLatestPrinting).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.EmbeddingUpdatedAt).IsRequired(false);

        // Tipo do vetor: 512 dimensões (ResNet18).
        // Deve bater exatamente com a saída do modelo ONNX no VectorService.
        builder.Property(x => x.Embedding)
            .HasColumnType("vector(512)");

        // Índice HNSW com similaridade cossenoidal.
        // ESSENCIAL: sem ele cada busca vetorial faz full scan em todas as impressões.
        // Com HNSW, a busca em 88k+ impressões é feita em tempo logarítmico (~5–20ms).
        // O índice é construído de forma incremental conforme embeddings são inseridos.
        builder.HasIndex(x => x.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");

        // Índice composto para busca por set + número (identificação exata)
        builder.HasIndex(x => new { x.SetCode, x.CollectorNumber });

        // Índice para busca da impressão mais recente por OracleCard
        builder.HasIndex(x => new { x.OracleCardId, x.IsLatestPrinting });

        // FK para OracleCard
        builder.HasOne(x => x.OracleCard)
            .WithMany(o => o.Printings)
            .HasForeignKey(x => x.OracleCardId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}