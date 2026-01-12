using ApiStudy.Models;
using ApiStudy.Models.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApiStudy.Mappers;

public class UsersMap: IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Name).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(150);

        // RELACIONAMENTO N:N (User <-> Feature)
        // Cria tabela de junção "UserFeatures"
        builder.HasMany(u => u.Features)
               .WithMany(f => f.Users)
               .UsingEntity(j => j.ToTable("UserFeatures"));

        // RELACIONAMENTO 1:N (User -> Collections)
        // (Geralmente configuramos o lado do 'muitos' no arquivo do filho, mas a navegação começa aqui)
        builder.HasMany(u => u.Collections)
               .WithOne(c => c.User)
               .HasForeignKey(c => c.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        // RELACIONAMENTO 1:N (User -> Decks)
        builder.HasMany(u => u.Decks)
               .WithOne(d => d.User)
               .HasForeignKey(d => d.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        // RELACIONAMENTO 1:N (User -> Match)
        builder.HasMany(u => u.Matches)
            .WithOne(d => d.User)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("Users");
    }
}
