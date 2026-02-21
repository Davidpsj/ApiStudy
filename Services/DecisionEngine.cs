using ApiStudy.Models.Scanner;

namespace ApiStudy.Services;

/// <summary>
/// Status do resultado retornado pela pipeline de identificação.
/// </summary>
public enum ScanStatus
{
    Confirmed,
    RescanRequired,
    Ambiguous,
    NotFound,
}

public enum ConfidenceLevel { High, Medium, Low }

/// <summary>
/// Resultado final produzido pelo DecisionEngine.
/// </summary>
public class ScannerResult
{
    public ScanStatus Status { get; init; }
    public ConfidenceLevel Confidence { get; init; }
    public float ConfidenceScore { get; init; }
    public string DetectionMethod { get; init; } = string.Empty;
    public int RescanAttempt { get; init; }
    public ScannerCandidate? Card { get; init; }
    public IList<ScannerCandidate> AlternativeCandidates { get; init; } = new List<ScannerCandidate>();
}

/// <summary>
/// Cérebro da pipeline — aplica regras de votação entre vetor e OCR.
///
/// FILOSOFIA CENTRAL:
///   O vetor (ResNet18 + pgvector cosine) é a fonte primária de identificação.
///   O OCR (Tesseract) é auxiliar — fontes ornamentadas MTG limitam sua precisão.
///
///   "Charming Frince" != "Charming Prince" por um único pixel no 'P' vs 'F'.
///   Não faz sentido descartar um vetor com distância 0.18 porque o Tesseract
///   errou uma letra.
///
/// TABELA DE DECISÃO (ordem de prioridade):
///
/// ┌──────────────────────────────────────────────────────────┬──────────────────┬──────────────┐
/// │ Condição                                                 │ Status           │ Confiança    │
/// ├──────────────────────────────────────────────────────────┼──────────────────┼──────────────┤
/// │ dist == 0 (hit por nome via FindByNameAsync)             │ Confirmed        │ High         │
/// │ dist < 0.30                                              │ Confirmed        │ High         │
/// │ dist < 0.42                                              │ Confirmed        │ Medium       │
/// │ dist < 0.42 E OCR discorda com score >= 0.90             │ Rescan ou Ambig  │ —            │
/// │ dist < 0.52 E OCR falhou                                 │ Confirmed        │ Low          │
/// │ dist >= 0.42 E OCR acertou (FindByName hit)              │ Confirmed        │ High         │
/// │ dist >= 0.52 E OCR falhou E attempt >= MaxAttempts       │ NotFound         │ —            │
/// │ dist >= 0.42 E OCR falhou E attempt < Max                │ RescanRequired   │ —            │
/// │ Todos os outros casos                                    │ RescanRequired   │ —            │
/// └──────────────────────────────────────────────────────────┴──────────────────┴──────────────┘
///
/// CALIBRAÇÃO DOS THRESHOLDS (v2):
///
///   DistHigh   = 0.30 (antes 0.35)
///     Com crop da arte, o ResNet18 é mais discriminativo. Distâncias acima de 0.30
///     para a mesma carta são raras com artes únicas, mas comuns entre cartas similares
///     (ex: várias versões de Llanowar Elves). Mais conservador = menos falsos positivos.
///
///   DistMedium = 0.42 (antes 0.45)
///     Na faixa 0.30–0.42 o vetor ainda é confiável mas merece confirmação.
///     0.45 aceitava distâncias que correspondiam a cartas diferentes em testes.
///
///   DistCutoff = 0.52 (antes 0.55)
///     Acima de 0.52 sem OCR é quase certo que é outra carta.
///
///   OcrBlockThreshold = 0.90 (antes 0.92)
///     O Tesseract raramente passa 0.92 para fontes ornamentadas. 0.90 permite que
///     leituras parcialmente corretas sirvam como sinal de conflito.
///
/// Thresholds de distância cossenoidal (pgvector <=>):
///   0.00 = idêntico (hit exato de nome via OCR)
///   0.05–0.20 = mesma carta, mesma impressão
///   0.20–0.30 = mesma carta, impressão diferente
///   0.30–0.42 = alta similaridade (Confirmed Medium)
///   0.42–0.52 = baixa similaridade (suspeito)
///   > 0.52    = provavelmente carta diferente
/// </summary>
public class DecisionEngine
{
    // Thresholds de distância cossenoidal (pgvector <=>)
    private const float DistHigh = 0.30f; // Confirmed High  — vetor muito seguro
    private const float DistMedium = 0.42f; // Confirmed Medium — vetor aceitável
    private const float DistCutoff = 0.52f; // Acima disso: NotFound ou RescanRequired

    // OCR só bloqueia o vetor quando está MUITO confiante (>= 90%) E nomes divergem.
    private const float OcrBlockThreshold = 0.90f;

    private const int MaxRescanAttempts = 3;

    public ScannerResult Decide(
        IList<VectorSearchResult> vectorCandidates,
        string? ocrTitle,
        float ocrScore,
        int attempt = 1)
    {
        bool vectorEmpty = vectorCandidates == null || vectorCandidates.Count == 0;
        bool ocrFailed = string.IsNullOrWhiteSpace(ocrTitle);

        // ── Ambos falharam ────────────────────────────────────────────────────
        if (vectorEmpty && ocrFailed)
            return BuildNotFound(attempt);

        // ── Só OCR, sem vetor (banco vazio ou sem embeddings) ─────────────────
        if (vectorEmpty)
            return attempt < MaxRescanAttempts
                ? BuildRescan(attempt)
                : BuildAmbiguous(null, attempt, "ocr");

        var top1 = vectorCandidates![0];
        float dist = top1.Distance;

        // ── Hit exato por nome via FindByNameAsync (dist = 0.0) ───────────────
        // ScannerService injeta top-0 com Distance=0 quando OCR leu o nome exato
        // e o encontrou no banco. É a confirmação mais confiável possível.
        if (dist == 0f)
            return BuildConfirmed(top1, ConfidenceLevel.High, "ocr+vector", attempt,
                vectorCandidates.Skip(1).ToList());

        // ── Vetor muito forte (dist < 0.30): confirma sem depender do OCR ─────
        // Com crop da arte, ResNet18 é muito discriminativo nesta faixa.
        // Fontes ornamentadas MTG erram letras individuais (P vs F, C vs G).
        // Não faz sentido rejeitar um vetor com dist=0.18 por "Charming Frince".
        if (dist < DistHigh)
            return BuildConfirmed(top1, ConfidenceLevel.High,
                ocrFailed ? "vector" : "ocr+vector", attempt,
                vectorCandidates.Skip(1).ToList());

        // ── Vetor bom (dist 0.30–0.42) ────────────────────────────────────────
        if (dist < DistMedium)
        {
            // Único caso em que OCR pode bloquear o vetor:
            // confiança >= 90% E nomes sem nenhuma palavra em comum.
            // Indica foto de carta diferente com arte visualmente similar.
            if (!ocrFailed && ocrScore >= OcrBlockThreshold
                && !NamesOverlap(top1.OracleCard.Name, ocrTitle!))
            {
                return attempt < MaxRescanAttempts
                    ? BuildRescan(attempt)
                    : BuildAmbiguous(top1, attempt, "vector");
            }

            return BuildConfirmed(top1, ConfidenceLevel.Medium,
                ocrFailed ? "vector" : "ocr+vector", attempt,
                vectorCandidates.Skip(1).ToList());
        }

        // ── Vetor fraco (dist >= 0.42) ─────────────────────────────────────────
        // Acima do cutoff absoluto: muito provavelmente outra carta
        if (dist >= DistCutoff)
            return attempt < MaxRescanAttempts
                ? BuildRescan(attempt)
                : BuildNotFound(attempt);

        // dist 0.42–0.52: suspeito — pede rescan ou retorna ambíguo na última tentativa
        return attempt < MaxRescanAttempts
            ? BuildRescan(attempt)
            : BuildAmbiguous(top1, attempt, "vector");
    }

    // ── Builders ──────────────────────────────────────────────────────────────

    private static ScannerResult BuildConfirmed(
        VectorSearchResult top1,
        ConfidenceLevel level,
        string method,
        int attempt,
        IList<VectorSearchResult> alternatives) => new()
        {
            Status = ScanStatus.Confirmed,
            Confidence = level,
            ConfidenceScore = MathF.Round(Math.Max(0f, 1f - top1.Distance), 4),
            DetectionMethod = method,
            RescanAttempt = attempt,
            Card = ToCandidate(top1, method),
            // Candidatos alternativos permitem que o cliente ofereça opção de correção
            // caso o scanner tenha identificado a impressão errada do mesmo card
            AlternativeCandidates = alternatives.Select(a => ToCandidate(a, "vector")).ToList(),
        };

    private static ScannerResult BuildRescan(int attempt) => new()
    {
        Status = ScanStatus.RescanRequired,
        Confidence = ConfidenceLevel.Low,
        ConfidenceScore = 0f,
        DetectionMethod = "divergent",
        RescanAttempt = attempt,
        Card = null,
    };

    private static ScannerResult BuildAmbiguous(
        VectorSearchResult? top1, int attempt, string method) => new()
        {
            Status = ScanStatus.Ambiguous,
            Confidence = ConfidenceLevel.Low,
            ConfidenceScore = top1 != null ? MathF.Round(1f - top1.Distance, 4) : 0f,
            DetectionMethod = method,
            RescanAttempt = attempt,
            Card = top1 != null ? ToCandidate(top1, method) : null,
        };

    private static ScannerResult BuildNotFound(int attempt) => new()
    {
        Status = ScanStatus.NotFound,
        Confidence = ConfidenceLevel.Low,
        ConfidenceScore = 0f,
        DetectionMethod = "none",
        RescanAttempt = attempt,
        Card = null,
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifica se os dois nomes têm sobreposição suficiente para considerar concordância.
    /// Mais tolerante que igualdade exata — aceita que o OCR erre algumas letras.
    ///
    /// Lógica: se >= 55% das palavras do nome do banco aparecem no texto do OCR
    /// (após normalização), considera concordância.
    ///
    /// CALIBRAÇÃO: reduzido de 60% para 55%.
    ///   Para cartas com nomes curtos de 2 palavras (ex: "Felidar Guardian"),
    ///   60% exigia que ambas as palavras fossem identificadas.
    ///   Com 55%, uma correspondência parcial ("Felidar Guardia") já é aceita.
    ///   O risco de falso positivo é baixo porque NamesOverlap só é chamado quando
    ///   ocrScore >= 0.90 — o Tesseract está muito confiante nessa situação.
    /// </summary>
    private static bool NamesOverlap(string dbName, string ocrName)
    {
        static string Norm(string s) =>
            new string(s.ToLowerInvariant()
                .Normalize(System.Text.NormalizationForm.FormKD)
                .Where(c => c < 128 && (char.IsLetterOrDigit(c) || c == ' '))
                .ToArray()).Trim();

        var dbWords = Norm(dbName).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var ocrNorm = Norm(ocrName);

        if (dbWords.Length == 0) return false;

        int matches = dbWords.Count(w => w.Length >= 3 && ocrNorm.Contains(w));

        // 55% de palavras em comum = concordância suficiente
        return (float)matches / dbWords.Length >= 0.55f;
    }

    private static ScannerCandidate ToCandidate(VectorSearchResult r, string source) => new()
    {
        OracleId = r.OracleCard.Id,
        Name = r.OracleCard.Name,
        SetCode = r.LatestPrinting?.SetCode,
        CollectorNumber = r.LatestPrinting?.CollectorNumber,
        ImageUrl = r.LatestPrinting?.ImageUrl,
        ReleasedAt = r.LatestPrinting?.ReleasedAt,
        ConfidenceScore = MathF.Round(1f - r.Distance, 4),
        Source = source,
    };
}