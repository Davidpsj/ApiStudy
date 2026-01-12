using ApiStudy.Models.Cards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApiStudy.Mappers;

public class CardMap : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.HasKey(x => x.Id);

        // Propriedades adicionais do Card podem vir aqui se houver (Name, ManaCost, etc)

        builder.ToTable("Cards");
    }
}
