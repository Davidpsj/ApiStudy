using ApiStudy.Models.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApiStudy.Mappers;

public class FeatureMap
{
    public void Configure(EntityTypeBuilder<Feature> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(f => f.Name).IsRequired().HasMaxLength(255);

        builder.ToTable("Features");
    }
}
