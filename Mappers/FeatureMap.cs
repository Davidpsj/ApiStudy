using ApiStudy.Models.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApiStudy.Mappers;

/// <summary>
/// Configuração do Entity Framework para a entidade Feature.
///
/// IMPORTANTE: Esta classe DEVE implementar IEntityTypeConfiguration&lt;Feature&gt;
/// para ser detectada automaticamente pelo DatabaseContext via:
///   modelBuilder.ApplyConfigurationsFromAssembly(typeof(DatabaseContext).Assembly)
///
/// A versão anterior havia omitido a implementação da interface, fazendo com que
/// a configuração desta tabela fosse silenciosamente ignorada pelo EF Core —
/// nenhum erro em compilação ou runtime, mas a tabela "Features" não recebia
/// nenhuma das configurações definidas aqui (índices, constraints, etc.).
/// </summary>
public class FeatureMap : IEntityTypeConfiguration<Feature>
{
    public void Configure(EntityTypeBuilder<Feature> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(255);

        // Índice para busca de features por nome (ex: verificação de permissões)
        builder.HasIndex(f => f.Name).IsUnique();

        builder.ToTable("Features");
    }
}