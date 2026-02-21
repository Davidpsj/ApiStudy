using ApiStudy.Models.Scanner;
using Newtonsoft.Json.Linq;

namespace ApiStudy.Repository;

/// <summary>
/// Contrato do repositório do scanner de cartas MTG.
/// Todas as operações de persistência e consulta vetorial passam por aqui.
/// </summary>
public interface IScannerRepository
{
    // ── IDENTIFICAÇÃO ──────────────────────────────────────────────────────────

    /// <summary>
    /// Busca as <paramref name="topK"/> impressões cujo embedding é mais próximo
    /// (menor distância cossenoidal) do <paramref name="embedding"/> fornecido.
    ///
    /// Retorna a impressão específica (SetCode + CollectorNumber) cujo embedding
    /// venceu a busca — não a "mais recente" da carta.
    ///
    /// Padrão topK=10 para cobrir cartas com muitas impressões (Plains, Lightning Bolt)
    /// sem sacrificar latência (índice HNSW do pgvector mantém O(log N) até ~50).
    /// </summary>
    Task<IList<VectorSearchResult>> FindClosestCardsAsync(float[] embedding, int topK = 10);

    /// <summary>
    /// Busca uma carta pelo nome exato (ou prefixo se o nome exato não existir).
    ///
    /// Retorna Distance=0.0 quando encontrado — sinaliza ao DecisionEngine que é
    /// um hit confiável via OCR, independente da distância cossenoidal do vetor.
    ///
    /// A impressão retornada é a IsLatestPrinting=true — o set exato será
    /// discriminado pelo vetor nas etapas seguintes da pipeline.
    /// </summary>
    Task<VectorSearchResult?> FindByNameAsync(string name);

    // ── SEED ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifica se já existe ao menos uma impressão do set no banco.
    /// Usado pelo SeedSetAsync para evitar re-download desnecessário dos metadados.
    /// </summary>
    Task<bool> SetExistsAsync(string setCode);

    /// <summary>
    /// Insere ou atualiza em lote as cartas do JSON retornado pelo Scryfall.
    /// Recalcula IsLatestPrinting para todas as impressões afetadas.
    /// </summary>
    Task AddOrUpdateBatchAsync(JArray cardsData);

    /// <summary>
    /// Salva o embedding gerado para uma impressão específica.
    /// </summary>
    Task SaveEmbeddingAsync(Guid printingId, float[] embedding);

    /// <summary>
    /// Retorna impressões sem embedding gerado, ordenadas por relevância
    /// (IsLatestPrinting primeiro, depois por ReleasedAt decrescente).
    ///
    /// Quando <paramref name="setCode"/> é informado, filtra apenas impressões
    /// daquele set — use durante o SeedSetAsync para não processar o backlog
    /// de outros sets na mesma chamada.
    ///
    /// Quando <paramref name="setCode"/> é null, retorna todas as impressões
    /// pendentes do banco — usado pelo job periódico de re-embedding.
    /// </summary>
    Task<IList<(Guid PrintingId, string? ImageUrl)>> GetPrintingsWithoutEmbeddingAsync(
        string? setCode);
}