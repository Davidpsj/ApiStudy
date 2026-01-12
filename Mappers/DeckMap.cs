using ApiStudy.Models.Cards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApiStudy.Mappers;

public class DeckMap
{
    public void Configure(EntityTypeBuilder<Deck> builder) {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).IsRequired().HasMaxLength(50);
        builder.Property(d => d.Description).HasMaxLength(255);
        builder.Property(d => d.GameFormat).IsRequired().HasMaxLength(25);

        // RELACIONAMENTO 1:N (User -> Deck)
        builder.HasOne(d => d.User)
               .WithMany(u => u.Decks)
               .HasForeignKey(d => d.UserId);

        // RELACIONAMENTO N:N (Deck <-> Card)
        // Cria tabela de junção "DeckCards"
        builder.HasMany(d => d.Cards)
               .WithMany(c => c.Decks)
               .UsingEntity(j => j.ToTable("DeckCards"));

        builder.ToTable("Decks");
    }
}
