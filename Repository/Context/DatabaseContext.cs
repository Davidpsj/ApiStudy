using ApiStudy.Mappers;
using ApiStudy.Models.Auth;
using ApiStudy.Models.Cards;
using ApiStudy.Models.Match;
using ApiStudy.Models.Scanner;
using Microsoft.EntityFrameworkCore;
using System;

namespace ApiStudy.Repository.Context;

public class DatabaseContext : DbContext
{
    public DatabaseContext() { }
    public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }

    #region DbSets

    public DbSet<User> Users { get; set; }
    public DbSet<Card> Cards { get; set; }
    public DbSet<Deck> Decks { get; set; }
    public DbSet<Collection> Collections { get; set; }
    public DbSet<Feature> Features { get; set; }
    public DbSet<Match> Matches { get; set; }
    // ── Scanner (novo schema) ─────────────────────────────────────
    /// <summary>
    /// Identidade canônica das cartas (1 por oracle_id do Scryfall).
    /// Contém o embedding de IA para busca por similaridade.
    /// </summary>
    public DbSet<OracleCard> OracleCards { get; set; }

    /// <summary>
    /// Impressões específicas de cada carta em sets diferentes.
    /// Uma OracleCard pode ter N CardPrintings.
    /// </summary>
    public DbSet<CardPrinting> CardPrintings { get; set; }

    #endregion

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        // Esta linha mágica encontra todas as classes que implementam 
        // IEntityTypeConfiguration e as aplica automaticamente.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DatabaseContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
