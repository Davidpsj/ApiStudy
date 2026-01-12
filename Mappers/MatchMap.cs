using ApiStudy.Models.Match;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ApiStudy.Mappers;

public class MatchMap : IEntityTypeConfiguration<Match>
{
    // Define as opções de serialização/desserialização JSON (reutilizáveis)
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly JsonSerializerOptions _jsonDeserializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    // Método auxiliar genérico para aplicar o Value Converter JSON
    private void HasJsonConversion<T>(PropertyBuilder<T> propertyBuilder)
    {
        propertyBuilder.HasConversion(
            v => JsonSerializer.Serialize(v, typeof(T), _jsonSerializerOptions),
            v => JsonSerializer.Deserialize<T>(v, _jsonDeserializerOptions)!
        );

        propertyBuilder.Metadata.SetValueComparer(
            new ValueComparer<T>(
                // Lambda para Comparação: Serializa e compara as strings JSON
                (t1, t2) => JsonSerializer.Serialize(t1, _jsonSerializerOptions) == JsonSerializer.Serialize(t2, _jsonSerializerOptions),

                // Lambda para Hashing: Útil, embora não estritamente necessário para JSON
                t => t == null ? 0 : JsonSerializer.Serialize(t, _jsonSerializerOptions).GetHashCode(),

                // Lambda para Clonagem: Cria uma nova instância ao carregar (se T for um tipo de referência)
                t => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(t, _jsonSerializerOptions), _jsonDeserializerOptions)!
            )
        );
    }

    public void Configure(EntityTypeBuilder<Match> builder)
    {
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

        var jsonDeserializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        builder.HasKey(x => x.Id);
        // Propriedades adicionais do Match podem vir aqui (Players, MatchFormat, etc)

        // RELACIONAMENTO 1:N (User -> Match)
        builder.HasOne(u => u.User)
               .WithMany(m => m.Matches)
               .HasForeignKey(fk => fk.UserId);

        HasJsonConversion(builder.Property(x => x.Players));

        builder.Property(x => x.MatchFormat).IsRequired();
        builder.Property(x => x.MatchType).IsRequired();

        HasJsonConversion(builder.Property(x => x.Scores));

        HasJsonConversion(builder.Property(x => x.PlayerEffects));

        builder.ToTable("Matches");
    }
}
