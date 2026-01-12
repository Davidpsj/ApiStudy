using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ApiStudy.Models.Auth;
using ApiStudy.Models.Cards;

namespace ApiStudy.Mappers;

public class CollectionMap: IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Description).HasMaxLength(255);

        // RELACIONAMENTO 1:N (User -> Collection)
        builder.HasOne(c => c.User)
               .WithMany(u => u.Collections)
               .HasForeignKey(c => c.UserId);

        // RELACIONAMENTO N:N (Collection <-> Card)
        // Cria tabela de junção "CollectionCards"
        builder.HasMany(c => c.Cards)
               .WithMany(card => card.Collections)
               .UsingEntity(j => j.ToTable("CollectionCards"));

        builder.ToTable("Collections");
    }
}
