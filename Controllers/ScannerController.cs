using ApiStudy.Filters;
using ApiStudy.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiStudy.Controllers;

// ── DTOs de request/response ──────────────────────────────────────────────────

/// <summary>
/// Resposta padrão do endpoint de identificação.
/// Segue o contrato acordado no plano arquitetural da Sprint 1.
/// </summary>
public class ScannerIdentifyResponse
{
    /// <summary>confirmed | rescan_required | ambiguous | not_found</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>high | medium | low</summary>
    public string Confidence { get; init; } = string.Empty;

    /// <summary>Score numérico entre 0.0 e 1.0.</summary>
    public float ConfidenceScore { get; init; }

    /// <summary>ocr+vector | ocr | vector | divergent | none</summary>
    public string DetectionMethod { get; init; } = string.Empty;

    /// <summary>Tempo total de processamento em milissegundos.</summary>
    public long ProcessingTimeMs { get; init; }

    /// <summary>
    /// Número da tentativa desta chamada (começa em 1).
    /// Quando Status = rescan_required, o cliente deve reenviar uma nova imagem
    /// com attempt = valor retornado aqui + 1 (ou sem o campo para deixar o server calcular).
    /// </summary>
    public int RescanAttempt { get; init; }

    /// <summary>Carta identificada. Null quando Status = not_found.</summary>
    public ScannerCardDto? Card { get; init; }

    /// <summary>Candidatos alternativos (preenchido apenas quando Status = ambiguous).</summary>
    public IList<ScannerCardDto> AlternativeCandidates { get; init; } = [];
}

public class ScannerCardDto
{
    public Guid OracleId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? SetCode { get; init; }
    public string? CollectorNumber { get; init; }
    public string? ImageUrl { get; init; }
    public string? ReleasedAt { get; init; }
    public float ConfidenceScore { get; init; }
}

// ── Controller ────────────────────────────────────────────────────────────────

/// <summary>
/// Endpoints do scanner de cartas MTG.
/// Substitui o CardRecognitionController (desativado via exclusão no .csproj).
///
/// Rotas:
///   POST api/scanner/identify   — identifica a carta em uma imagem
///   GET  api/scanner/seed/{set} — popula o catálogo com um set do Scryfall
/// </summary>
//[Authorize]
[ApiController]
[Route("api/[controller]")]
//[EnsureUser]
public class ScannerController : ControllerBase
{
    private readonly ScannerService _scannerService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ScannerController> _logger;

    public ScannerController(
        ScannerService scannerService,
        IHttpClientFactory httpClientFactory,
        ILogger<ScannerController> logger)
    {
        _scannerService = scannerService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Identifica uma carta Magic a partir de uma imagem capturada pela câmera.
    ///
    /// O cliente deve:
    ///   1. Enviar a imagem como multipart/form-data no campo "file".
    ///   2. Se a resposta retornar status "rescan_required", capturar nova foto
    ///      e reenviar com o campo "attempt" incrementado (ou omiti-lo).
    ///   3. Após 3 tentativas, o servidor retorna "ambiguous" com o melhor candidato.
    ///
    /// Formatos aceitos: JPEG, PNG, WebP.
    /// Tamanho máximo recomendado: 5MB (imagens maiores aumentam o tempo de processamento).
    /// </summary>
    [HttpPost("identify")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ScannerIdentifyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Identify(
        IFormFile file,
        [FromQuery] int attempt = 0)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Imagem obrigatória. Envie o arquivo no campo 'file'." });

        // Valida o tipo MIME antes de processar
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(new { error = $"Tipo de arquivo não suportado: {file.ContentType}. Use JPEG, PNG ou WebP." });

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            byte[] rawBytes = ms.ToArray();

            var result = await _scannerService.IdentifyAsync(rawBytes, attempt);

            sw.Stop();

            return Ok(new ScannerIdentifyResponse
            {
                Status = result.Status.ToString().ToLowerSnakeCase(),
                Confidence = result.Confidence.ToString().ToLower(),
                ConfidenceScore = result.ConfidenceScore,
                DetectionMethod = result.DetectionMethod,
                ProcessingTimeMs = sw.ElapsedMilliseconds,
                RescanAttempt = result.RescanAttempt,
                Card = result.Card != null ? ToDto(result.Card) : null,
                AlternativeCandidates = result.AlternativeCandidates
                    .Select(ToDto)
                    .ToList(),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ScannerController] Falha inesperada ao identificar carta.");
            return StatusCode(500, new { error = "Falha interna ao processar a imagem.", detail = ex.Message });
        }
    }

    /// <summary>
    /// Popula o catálogo de cartas com um set completo do Scryfall.
    ///
    /// Operação idempotente: sets já existentes têm apenas os embeddings pendentes processados.
    /// Respeita o rate limit do Scryfall (100ms entre páginas, 150ms entre downloads de imagem).
    ///
    /// Exemplos de setCode: "m15", "mh3", "fdn", "dsk"
    /// </summary>
    [HttpGet("seed/{setCode}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SeedSet([FromRoute] string setCode)
    {
        if (string.IsNullOrWhiteSpace(setCode))
            return BadRequest(new { error = "Código do set é obrigatório. Exemplo: 'mh3', 'fdn'." });

        // Normaliza para minúsculo (Scryfall aceita minúsculo, ex: "m15" não "M15")
        setCode = setCode.ToLower().Trim();

        try
        {
            _logger.LogInformation("[ScannerController] Iniciando seed do set: {SetCode}", setCode);

            var (cardsProcessed, embeddingsGenerated) =
                await _scannerService.SeedSetAsync(setCode, _httpClientFactory);

            return Ok(new
            {
                status = "success",
                set = setCode.ToUpper(),
                cardsProcessed,
                embeddingsGenerated,
                message = embeddingsGenerated > 0
                    ? $"{embeddingsGenerated} embeddings gerados. O set está pronto para uso."
                    : "Metadados atualizados. Todos os embeddings já existiam."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ScannerController] Falha no seed do set {SetCode}.", setCode);
            return StatusCode(500, new
            {
                error = $"Falha durante o seed do set {setCode.ToUpper()}.",
                detail = ex.Message
            });
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ScannerCardDto ToDto(ApiStudy.Models.Scanner.ScannerCandidate c) => new()
    {
        OracleId = c.OracleId,
        Name = c.Name,
        SetCode = c.SetCode,
        CollectorNumber = c.CollectorNumber,
        ImageUrl = c.ImageUrl,
        ReleasedAt = c.ReleasedAt?.ToString("yyyy-MM-dd"),
        ConfidenceScore = c.ConfidenceScore,
    };
}

// ── Extension helper ──────────────────────────────────────────────────────────

/// <summary>
/// Converte PascalCase/CamelCase para snake_case para serialização JSON consistente.
/// Ex: "RescanRequired" → "rescan_required"
/// </summary>
internal static class StringExtensions
{
    internal static string ToLowerSnakeCase(this string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsUpper(c) && i > 0)
                sb.Append('_');
            sb.Append(char.ToLower(c));
        }
        return sb.ToString();
    }
}