using ApiStudy.Models.Scanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApiStudy.Mappers;

public class OracleCardMap : IEntityTypeConfiguration<OracleCard>
{
    public void Configure(EntityTypeBuilder<OracleCard> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("OracleCards");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(255);

        // Índice para FindByNameAsync (busca por nome exato pós-OCR)
        builder.HasIndex(x => x.Name);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        // Relacionamento 1:N com CardPrinting
        builder.HasMany(x => x.Printings)
            .WithOne(p => p.OracleCard)
            .HasForeignKey(p => p.OracleCardId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}