using ApiStudy.Models.Scanner;
using ApiStudy.Repository.Context;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Pgvector;

namespace ApiStudy.Repository;

public class ScannerRepository : IScannerRepository
{
    private readonly DatabaseContext _context;

    public ScannerRepository(DatabaseContext context)
    {
        _context = context;
    }

    // ── IDENTIFICAÇÃO ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IList<VectorSearchResult>> FindClosestCardsAsync(float[] embedding, int topK = 10)
    {
        var vectorString = "[" + string.Join(",",
            embedding.Select(f => f.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";

        // Busca vetorial em CardPrintings.Embedding — retorna a impressão EXATA cuja arte
        // mais se aproxima da foto. Não é necessariamente a IsLatestPrinting.
        //
        // Exemplo: se o usuário fotografar Plains ONE (full art Phyrexiana),
        // esta query retorna a impressão ONE #267, não a última impressão de Plains.
        // Isso permite identificar o set e número corretos para colecionadores.
        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = @"
            SELECT
                cp.""Id""               AS PrintingId,
                cp.""SetCode""          AS SetCode,
                cp.""CollectorNumber""  AS CollectorNumber,
                cp.""ImageUrl""         AS ImageUrl,
                cp.""ReleasedAt""       AS ReleasedAt,
                cp.""IsLatestPrinting"" AS IsLatestPrinting,
                cp.""OracleCardId""     AS OracleCardId,
                oc.""Name""             AS OracleName,
                (cp.""Embedding"" <=> @embedding::vector) AS Distance
            FROM ""CardPrintings"" cp
            INNER JOIN ""OracleCards"" oc ON oc.""Id"" = cp.""OracleCardId""
            WHERE cp.""Embedding"" IS NOT NULL
            ORDER BY Distance ASC
            LIMIT @topK";

        var embeddingParam = command.CreateParameter();
        embeddingParam.ParameterName = "@embedding";
        embeddingParam.Value = vectorString;
        command.Parameters.Add(embeddingParam);

        var topKParam = command.CreateParameter();
        topKParam.ParameterName = "@topK";
        topKParam.Value = topK;
        command.Parameters.Add(topKParam);

        if (command.Connection!.State != System.Data.ConnectionState.Open)
            await command.Connection.OpenAsync();

        var results = new List<VectorSearchResult>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var oracleCard = new OracleCard
            {
                Id = reader.GetGuid(reader.GetOrdinal("OracleCardId")),
                Name = reader.GetString(reader.GetOrdinal("OracleName")),
            };

            // FoundPrinting = impressão identificada pelo vetor (pode não ser a latest).
            // SetCode e CollectorNumber desta impressão são o que o colecionador quer saber.
            var foundPrinting = new CardPrinting
            {
                Id = reader.GetGuid(reader.GetOrdinal("PrintingId")),
                SetCode = reader.GetString(reader.GetOrdinal("SetCode")),
                CollectorNumber = reader.GetString(reader.GetOrdinal("CollectorNumber")),
                ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl"))
                                       ? null
                                       : reader.GetString(reader.GetOrdinal("ImageUrl")),
                ReleasedAt = reader.GetDateTime(reader.GetOrdinal("ReleasedAt")),
                IsLatestPrinting = reader.GetBoolean(reader.GetOrdinal("IsLatestPrinting")),
                OracleCardId = oracleCard.Id,
            };

            results.Add(new VectorSearchResult
            {
                OracleCard = oracleCard,
                // Campo chamado LatestPrinting por compatibilidade com DecisionEngine/ScannerService,
                // mas aqui contém a impressão ENCONTRADA pelo vetor, não necessariamente a mais nova.
                LatestPrinting = foundPrinting,
                Distance = Convert.ToSingle(reader.GetDouble(reader.GetOrdinal("Distance")))
            });
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<VectorSearchResult?> FindByNameAsync(string name)
    {
        var nameLower = name.ToLowerInvariant().Trim();

        // Busca exata primeiro (caso mais comum quando OCR acerta)
        var oracleCard = await _context.OracleCards
            .AsNoTracking()
            .FirstOrDefaultAsync(oc => oc.Name.ToLower() == nameLower);

        // Fallback: busca por prefixo — útil quando OCR corta a última letra.
        // Exemplo: "Felidar Guardia" → encontra "Felidar Guardian".
        // Mínimo de 4 caracteres para evitar falsos positivos em nomes curtos.
        if (oracleCard == null && nameLower.Length >= 4)
        {
            oracleCard = await _context.OracleCards
                .AsNoTracking()
                .FirstOrDefaultAsync(oc => oc.Name.ToLower().StartsWith(nameLower));
        }

        if (oracleCard == null) return null;

        // Retorna a impressão mais recente como representante da carta encontrada por nome.
        // O set exato será refinado pela busca vetorial (embedding) quando disponível.
        var printing = await _context.CardPrintings
            .AsNoTracking()
            .Where(cp => cp.OracleCardId == oracleCard.Id && cp.IsLatestPrinting)
            .FirstOrDefaultAsync();

        Console.WriteLine(
            $"[ScannerRepository] FindByNameAsync: \"{name}\" → \"{oracleCard.Name}\" " +
            $"(set: {printing?.SetCode ?? "N/A"})");

        // Distance = 0 representa confiança máxima (nome bateu exatamente ou por prefixo)
        return new VectorSearchResult
        {
            OracleCard = oracleCard,
            LatestPrinting = printing,
            Distance = 0f,
        };
    }

    // ── SEED ───────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<bool> SetExistsAsync(string setCode)
    {
        // Compara em uppercase — AddOrUpdateBatchAsync normaliza para uppercase ao inserir
        return await _context.CardPrintings
            .AnyAsync(cp => cp.SetCode == setCode.ToUpper());
    }

    /// <inheritdoc/>
    public async Task AddOrUpdateBatchAsync(JArray cardsData)
    {
        foreach (var cardToken in cardsData)
        {
            var oracleIdStr = cardToken["oracle_id"]?.ToString();
            var scryfallIdStr = cardToken["id"]?.ToString();

            if (string.IsNullOrEmpty(oracleIdStr) || string.IsNullOrEmpty(scryfallIdStr))
                continue;

            if (!Guid.TryParse(oracleIdStr, out Guid oracleId)) continue;
            if (!Guid.TryParse(scryfallIdStr, out Guid scryfallId)) continue;

            var cardName = cardToken["name"]?.ToString() ?? "Unknown";
            var setCode = cardToken["set"]?.ToString()?.ToUpper() ?? "UNK";
            var setType = cardToken["set_type"]?.ToString() ?? "";

            // Tenta image_uris["normal"] (cartas frente-única).
            // Fallback para card_faces[0] (dupla face: MDFC, transform).
            var imageUrl = cardToken["image_uris"]?["normal"]?.ToString()
                        ?? cardToken["card_faces"]?[0]?["image_uris"]?["normal"]?.ToString();

            DateTime releasedAt = DateTime.MinValue;
            var releasedAtStr = cardToken["released_at"]?.ToString();
            if (!string.IsNullOrEmpty(releasedAtStr))
                DateTime.TryParse(releasedAtStr, out releasedAt);

            // Garante UTC para compatibilidade com PostgreSQL timestamptz
            releasedAt = DateTime.SpecifyKind(releasedAt, DateTimeKind.Utc);

            // ── UPSERT em OracleCard ──────────────────────────────────────────
            var oracleCard = await _context.OracleCards.FindAsync(oracleId);
            if (oracleCard == null)
            {
                oracleCard = new OracleCard
                {
                    Id = oracleId,
                    Name = cardName,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                _context.OracleCards.Add(oracleCard);
                await _context.SaveChangesAsync();
            }

            // ── INSERT em CardPrinting (idempotente pelo ScryfallId) ──────────
            if (await _context.CardPrintings.AnyAsync(cp => cp.Id == scryfallId))
                continue;

            var newPrinting = new CardPrinting
            {
                Id = scryfallId,
                OracleCardId = oracleId,
                SetCode = setCode,
                CollectorNumber = cardToken["collector_number"]?.ToString() ?? "",
                ImageUrl = imageUrl,
                ReleasedAt = releasedAt,
                SetType = setType,
                IsLatestPrinting = false, // recalculado logo abaixo
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _context.CardPrintings.Add(newPrinting);
            await _context.SaveChangesAsync();

            // ── RECALCULA IsLatestPrinting para todas as impressões do OracleCard ──
            // Necessário porque inserir uma impressão mais nova invalida a flag da anterior.
            var allPrintings = await _context.CardPrintings
                .Where(cp => cp.OracleCardId == oracleId)
                .ToListAsync();

            var latestDate = allPrintings.Max(cp => cp.ReleasedAt);

            foreach (var p in allPrintings)
            {
                bool shouldBeLatest = p.ReleasedAt == latestDate;
                if (p.IsLatestPrinting != shouldBeLatest)
                {
                    p.IsLatestPrinting = shouldBeLatest;
                    p.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
        }
    }

    /// <inheritdoc/>
    public async Task SaveEmbeddingAsync(Guid printingId, float[] embedding)
    {
        var printing = await _context.CardPrintings.FindAsync(printingId);
        if (printing == null)
        {
            Console.WriteLine($"[ScannerRepository] PrintingId {printingId} não encontrado.");
            return;
        }

        printing.Embedding = new Vector(embedding);
        printing.EmbeddingUpdatedAt = DateTime.UtcNow;
        printing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A assinatura NÃO usa valor default (= null) para que a implementação seja
    /// reconhecida pelo compilador como implementação do membro da interface.
    /// Valores default em interfaces C# não são propagados para implementações —
    /// o compilador trata os dois como membros diferentes ao verificar conformidade.
    ///
    /// Callers que quiserem passar null explicitamente para obter tudo pendente usam:
    ///   await repo.GetPrintingsWithoutEmbeddingAsync(null);
    /// </remarks>
    public async Task<IList<(Guid PrintingId, string? ImageUrl)>> GetPrintingsWithoutEmbeddingAsync(
        string? setCode)
    {
        // Constrói a query base: apenas impressões sem embedding e com URL de imagem disponível
        var query = _context.CardPrintings
            .Where(cp => cp.Embedding == null && cp.ImageUrl != null);

        // Quando setCode é informado, restringe ao set atual do seed.
        // Isso evita que o seed do set ONE processe o backlog de outros sets (M15, IKO, etc.)
        // que poderiam ter centenas de impressões pendentes inseridas em ciclos anteriores.
        if (!string.IsNullOrWhiteSpace(setCode))
            query = query.Where(cp => cp.SetCode == setCode.ToUpper());

        // IsLatestPrinting primeiro: garante que as versões mais relevantes (as que aparecem
        // em buscas padrão) tenham embedding antes das impressões antigas.
        var rows = await query
            .OrderByDescending(cp => cp.IsLatestPrinting)
            .ThenByDescending(cp => cp.ReleasedAt)
            .Select(cp => new { cp.Id, cp.ImageUrl })
            .AsNoTracking()
            .ToListAsync();

        // Converte para lista de tuplas nomeadas.
        // Nomes explícitos (PrintingId / ImageUrl) são obrigatórios para que o compilador
        // infira os tipos corretamente no padrão de desconstrução:
        //   foreach (var (printingId, imageUrl) in pending)
        return rows
            .Select(x => (PrintingId: x.Id, ImageUrl: x.ImageUrl))
            .ToList<(Guid PrintingId, string? ImageUrl)>();
    }
}