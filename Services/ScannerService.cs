using ApiStudy.Models.Scanner;
using ApiStudy.Repository;
using System.Diagnostics;

namespace ApiStudy.Services;

/// <summary>
/// Orquestrador da pipeline de reconhecimento de cartas MTG.
///
/// Fluxo por tentativa (~250–450ms total):
///
///   [0] CardDetectionService.DetectAndCrop()        ~40–80ms   (síncrono, CPU-bound)
///        ↓ imagem recortada e endireitada 488×680
///   [A] VectorService.GenerateEmbedding()           ~150–300ms ──┐ Task.WhenAll
///   [B] OcrService.ReadCardTitle()                  ~30–70ms   ──┘ (paralelo)
///        ↓ embedding float[512] + OcrResult(título, score)
///   [C] IScannerRepository.FindClosestCardsAsync()  ~5–15ms    (pgvector HNSW)
///        ↓ top-10 CardPrintings com distância cossenoidal
///   [D] DecisionEngine.Decide()                     ~1ms
///        ↓ ScannerResult com set e número exatos da impressão fotografada
///
/// INJEÇÃO DO OCR HIT:
///   Quando o OCR lê o nome com ≥80% de confiança E encontra a carta no banco,
///   um candidato Distance=0.0 é inserido no topo da lista antes do DecisionEngine.
///   O DecisionEngine confirma imediatamente ao ver dist=0.0 — é o resultado mais
///   confiável possível (nome exato encontrado no catálogo).
///
///   IMPORTANTE: o hit do OCR retorna a IsLatestPrinting da carta, não a impressão
///   específica fotografada. Para identificar "Plains ONE #267 vs Plains M15", o
///   vetor na busca vetorial faz essa discriminação — o OCR garante apenas o OracleCard.
/// </summary>
public class ScannerService
{
    private readonly CardDetectionService _detector;
    private readonly VectorService _vectorService;
    private readonly OcrService _ocrService;
    private readonly DecisionEngine _decisionEngine;
    private readonly IServiceScopeFactory _scopeFactory;

    public ScannerService(
        CardDetectionService detector,
        VectorService vectorService,
        OcrService ocrService,
        DecisionEngine decisionEngine,
        IServiceScopeFactory scopeFactory)
    {
        _detector = detector;
        _vectorService = vectorService;
        _ocrService = ocrService;
        _decisionEngine = decisionEngine;
        _scopeFactory = scopeFactory;
    }

    public async Task<ScannerResult> IdentifyAsync(byte[] rawImageBytes, int previousAttempt = 0)
    {
        var sw = Stopwatch.StartNew();
        int attempt = previousAttempt + 1;

        // ── [0] Detecção e recorte da carta ───────────────────────────────────
        // CardDetectionService.DetectAndCrop() tenta Emgu.CV (WarpPerspective) primeiro;
        // se falhar usa fallback ImageSharp (crop central por proporção MTG).
        byte[] croppedBytes = _detector.DetectAndCrop(rawImageBytes);

        // ── [A + B] Vetor e OCR em paralelo ──────────────────────────────────
        // VectorService.GenerateEmbedding() é CPU-bound (~150–300ms no ResNet18).
        // OcrService.ReadCardTitle() também é CPU-bound (~30–70ms no Tesseract).
        // Executar em paralelo no ThreadPool reduz o tempo total em ~60–70%.
        var vectorTask = Task.Run(() => _vectorService.GenerateEmbedding(croppedBytes));
        var ocrTask = Task.Run(() => _ocrService.ReadCardTitle(croppedBytes));

        await Task.WhenAll(vectorTask, ocrTask);

        float[]? embedding = vectorTask.Result;
        OcrResult ocrResult = ocrTask.Result;

        // ── [C] Busca vetorial em CardPrintings ───────────────────────────────
        IList<VectorSearchResult> candidates = new List<VectorSearchResult>();

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IScannerRepository>();

        if (embedding != null)
        {
            // topK=10: busca as 10 impressões mais próximas.
            // O DecisionEngine usa apenas top-1 para a decisão final,
            // mas os demais são expostos como AlternativeCandidates no status Ambiguous.
            candidates = await repo.FindClosestCardsAsync(embedding, topK: 10);

            if (candidates.Count > 0)
                Console.WriteLine(
                    $"[ScannerService] Top-1 vetorial: \"{candidates[0].OracleCard.Name}\" " +
                    $"({candidates[0].LatestPrinting?.SetCode} #{candidates[0].LatestPrinting?.CollectorNumber}) " +
                    $"dist={candidates[0].Distance:F4}");
        }
        else
        {
            Console.WriteLine("[ScannerService] VectorService retornou null — embedding falhou.");
        }

        // ── OCR de alta confiança: injeta candidato por nome como top-0 ───────
        // Quando o OCR lê o nome exato com ≥80% de confiança e encontra no banco:
        //   1. Insere um VectorSearchResult com Distance=0.0 no topo da lista
        //   2. DecisionEngine vê dist=0.0 → Confirmed High imediatamente
        //
        // Por que ≥80%?
        //   80% é um bom equilíbrio: confiança suficiente para desambiguar o nome,
        //   mas não tão alto que ignore OCRs levemente incertos.
        //   A busca no banco usa LIKE StartsWith como fallback, então um nome levemente
        //   truncado (ex: "Felidar Guardia") ainda encontra "Felidar Guardian".
        const float OcrHighConfidenceThreshold = 0.80f;

        if (!string.IsNullOrWhiteSpace(ocrResult.Title)
            && ocrResult.Score >= OcrHighConfidenceThreshold)
        {
            var ocrCandidate = await repo.FindByNameAsync(ocrResult.Title);
            if (ocrCandidate != null)
            {
                var mutableList = new List<VectorSearchResult>(candidates);
                mutableList.Insert(0, ocrCandidate);
                candidates = mutableList;

                Console.WriteLine(
                    $"[ScannerService] OCR hit: \"{ocrResult.Title}\" (score={ocrResult.Score:P0}) " +
                    $"→ \"{ocrCandidate.OracleCard.Name}\" injetado como top-0.");
            }
            else
            {
                Console.WriteLine(
                    $"[ScannerService] OCR leu \"{ocrResult.Title}\" (score={ocrResult.Score:P0}) " +
                    $"mas não encontrou no banco.");
            }
        }

        // ── [D] Decisão ───────────────────────────────────────────────────────
        var result = _decisionEngine.Decide(candidates, ocrResult.Title, ocrResult.Score, attempt);

        sw.Stop();
        Console.WriteLine(
            $"[ScannerService] Tentativa {attempt}: status={result.Status}, " +
            $"confiança={result.Confidence}, método={result.DetectionMethod}, " +
            $"score={result.ConfidenceScore:F4}, ocr=\"{ocrResult.Title ?? "(null)"}\" " +
            $"(ocr_score={ocrResult.Score:P0}), tempo={sw.ElapsedMilliseconds}ms");

        return result;
    }

    /// <summary>
    /// Seed de um set completo do Scryfall: baixa metadados + gera embeddings.
    ///
    /// FLUXO:
    ///   1. Pagina a API Scryfall (/cards/search?q=e:{setCode}) → insere/atualiza CardPrintings
    ///   2. Busca apenas as impressões DO SET SEM EMBEDDING → gera e salva embeddings
    ///
    /// MUDANÇA CRÍTICA EM RELAÇÃO À VERSÃO ANTERIOR:
    ///   GetPrintingsWithoutEmbeddingAsync agora recebe setCode para filtrar apenas
    ///   as impressões do set atual. A versão anterior retornava TODAS as impressões
    ///   pendentes do banco, fazendo com que Plains ONE #267 esperasse na fila atrás
    ///   de centenas de impressões de outros sets inseridos anteriormente.
    ///
    /// RATE LIMITS:
    ///   100ms entre páginas da API, 150ms entre downloads de imagem.
    ///   Respeita a política de uso do Scryfall (máx ~10 req/s).
    /// </summary>
    public async Task<(int cardsProcessed, int embeddingsGenerated)> SeedSetAsync(
        string setCode, IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient();

        // O Scryfall rejeita com HTTP 400 qualquer requisição sem estes dois headers.
        // Remove antes do Add para evitar InvalidOperationException se o HttpClient
        // já tiver o header de uma chamada anterior (HttpClients são reutilizados pelo pool).
        client.DefaultRequestHeaders.Remove("User-Agent");
        client.DefaultRequestHeaders.Add("User-Agent", "ApiStudy-MTGScanner/1.0 (contact@yourdomain.com)");
        client.DefaultRequestHeaders.Remove("Accept");
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        string? nextUrl = "https://api.scryfall.com/cards/search" +
                          $"?q=e:{setCode}&unique=prints&include_extras=false";

        int totalCards = 0;
        int totalEmbeddings = 0;

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IScannerRepository>();

        if (await repo.SetExistsAsync(setCode))
            Console.WriteLine($"[SeedSet] Set {setCode.ToUpper()} já existe no banco. " +
                              "Verificando embeddings pendentes...");

        // ── Paginação Scryfall ────────────────────────────────────────────────
        //
        // Por que SendAsync em vez de GetStringAsync?
        //   GetStringAsync lança HttpRequestException sem acesso ao body de erro.
        //   Com SendAsync distinguimos:
        //     400/404 = set não existe no Scryfall (código interno/promo) → aviso + skip
        //     429     = rate limit → SeedBackgroundService fará retry no próximo ciclo
        //     5xx     → relança para o catch do SeedBackgroundService logar e continuar
        while (!string.IsNullOrEmpty(nextUrl))
        {
            using var pageRequest = new HttpRequestMessage(HttpMethod.Get, nextUrl);
            using var pageResponse = await client.SendAsync(pageRequest);

            if (!pageResponse.IsSuccessStatusCode)
            {
                var errorBody = await pageResponse.Content.ReadAsStringAsync();

                // Set inexistente no Scryfall — não é erro fatal, apenas avisa e encerra
                if (pageResponse.StatusCode == System.Net.HttpStatusCode.BadRequest
                 || pageResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine(
                        $"[SeedSet] Set {setCode.ToUpper()} não encontrado no Scryfall " +
                        $"({(int)pageResponse.StatusCode}). Pulando. " +
                        $"Resposta: {errorBody[..Math.Min(errorBody.Length, 300)]}");
                    break;
                }

                // Outros erros (429 rate-limit, 5xx) → relança para tratamento upstream
                throw new HttpRequestException(
                    $"Scryfall retornou {(int)pageResponse.StatusCode} para o set {setCode}. " +
                    $"Body: {errorBody[..Math.Min(errorBody.Length, 300)]}");
            }

            var rawJson = await pageResponse.Content.ReadAsStringAsync();
            var json = Newtonsoft.Json.Linq.JObject.Parse(rawJson);
            var data = json["data"] as Newtonsoft.Json.Linq.JArray;

            if (data != null)
            {
                await repo.AddOrUpdateBatchAsync(data);
                totalCards += data.Count;
                Console.WriteLine($"[SeedSet] {setCode.ToUpper()}: {totalCards} cartas inseridas/atualizadas.");
            }

            nextUrl = json["next_page"]?.ToString();
            if (!string.IsNullOrEmpty(nextUrl))
                await Task.Delay(100); // Rate limit Scryfall
        }

        // ── Geração de embeddings — APENAS DO SET ATUAL ───────────────────────
        // MUDANÇA: passa setCode para filtrar apenas impressões deste set.
        // A versão anterior passava null e processava tudo pendente no banco.
        var pending = await repo.GetPrintingsWithoutEmbeddingAsync(setCode);
        Console.WriteLine($"[SeedSet] {setCode.ToUpper()}: {pending.Count} impressões aguardando embedding.");

        foreach (var (printingId, imageUrl) in pending)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                Console.WriteLine($"[SeedSet] Printing {printingId} sem imageUrl — pulando.");
                continue;
            }

            try
            {
                // Download da imagem Scryfall (488×680px, ~60–120KB)
                byte[] imageBytes = await client.GetByteArrayAsync(imageUrl);

                // Aplica o mesmo pipeline que será usado na foto da câmera:
                // DetectAndCrop → VectorService.GenerateEmbedding
                // Isso garante que o embedding do seed e da foto usam o mesmo crop,
                // minimizando a distância cossenoidal entre seed e foto real.
                byte[] croppedBytes = _detector.DetectAndCrop(imageBytes);
                float[]? embedding = _vectorService.GenerateEmbedding(croppedBytes);

                if (embedding == null)
                {
                    Console.WriteLine($"[SeedSet] Falha ao gerar embedding para printing {printingId}.");
                    continue;
                }

                await repo.SaveEmbeddingAsync(printingId, embedding);
                totalEmbeddings++;

                if (totalEmbeddings % 50 == 0)
                    Console.WriteLine($"[SeedSet] {setCode.ToUpper()}: {totalEmbeddings} embeddings gerados...");

                await Task.Delay(150); // Rate limit Scryfall
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SeedSet] Erro ao processar printing {printingId}: {ex.Message}");
            }
        }

        Console.WriteLine(
            $"[SeedSet] Concluído: set={setCode.ToUpper()}, " +
            $"cartas={totalCards}, embeddings={totalEmbeddings}.");

        return (totalCards, totalEmbeddings);
    }
}