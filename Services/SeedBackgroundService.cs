using ApiStudy.Repository;

namespace ApiStudy.Services;

/// <summary>
/// Serviço de background que mantém o catálogo de cartas atualizado automaticamente.
///
/// Roda como IHostedService — iniciado junto com a aplicação e executado
/// indefinidamente em loop enquanto o servidor estiver de pé.
///
/// Ciclo de execução:
///   1. Aguarda o delay inicial (10s) para a aplicação terminar de subir
///   2. Consulta a API do Scryfall para listar todos os sets disponíveis
///   3. Filtra sets que ainda não existem no catálogo local
///   4. Para cada set novo, chama ScannerService.SeedSetAsync()
///   5. Aguarda o intervalo configurado (padrão: 24h) e repete
///
/// Sets ignorados: memorabilia, token, minigame, funny
/// (não são cartas jogáveis e não devem aparecer no scanner)
///
/// Registrado como Singleton via AddHostedService<SeedBackgroundService>()
/// no Program.cs. Usa IServiceScopeFactory para resolver serviços Scoped
/// (IScannerRepository, ScannerService) dentro do loop de background,
/// pois serviços Hosted vivem mais que um único escopo de requisição.
/// </summary>
public class SeedBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SeedBackgroundService> _logger;

    // Sets excluídos — não são cartas jogáveis do jogo principal
    private static readonly HashSet<string> IgnoredSetTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "memorabilia", "token", "minigame", "funny"
    };

    // Intervalo entre verificações de sets novos
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    // Delay inicial para a aplicação terminar de inicializar antes do primeiro seed
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(10);

    // Scryfall exige User-Agent no formato "AppName/versão (contato)" e Accept: application/json.
    // Substitua o e-mail de contato por um real — o Scryfall usa isso para rastrear abusos.
    // Referência: https://scryfall.com/docs/api
    private const string ScryfallUserAgent = "ApiStudy/1.0 (contact@yourdomain.com)";
    private const string ScryfallAccept = "application/json";
    private const string ScryfallSetsUrl = "https://api.scryfall.com/sets";

    public SeedBackgroundService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<SeedBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Loop principal do background service.
    /// CancellationToken é sinalizado quando a aplicação está sendo encerrada (SIGTERM/Ctrl+C).
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "[SeedBackground] Serviço iniciado. Aguardando {Delay}s antes do primeiro ciclo...",
            InitialDelay.TotalSeconds);

        // Delay inicial — dá tempo para o banco conectar, migrations rodarem, etc.
        await Task.Delay(InitialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSeedCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Encerramento gracioso via Ctrl+C / SIGTERM — não loga como erro
                break;
            }
            catch (Exception ex)
            {
                // Loga mas não derruba o serviço — tenta novamente no próximo ciclo
                _logger.LogError(ex,
                    "[SeedBackground] Erro durante o ciclo de seed. Próxima tentativa em {Hours}h.",
                    CheckInterval.TotalHours);
            }

            _logger.LogInformation("[SeedBackground] Próximo ciclo em {Hours}h.", CheckInterval.TotalHours);

            // Aguarda até o próximo ciclo, respeitando o cancelamento
            await Task.Delay(CheckInterval, stoppingToken);
        }

        _logger.LogInformation("[SeedBackground] Serviço encerrado.");
    }

    /// <summary>
    /// Um ciclo completo de verificação:
    ///   1. Busca todos os sets do Scryfall
    ///   2. Identifica quais ainda não estão no catálogo
    ///   3. Semeia cada set novo via ScannerService
    /// </summary>
    private async Task RunSeedCycleAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[SeedBackground] Iniciando ciclo de verificação de sets...");

        var client = CriarClienteScryfall();

        // Por que SendAsync em vez de GetStringAsync?
        //
        // GetStringAsync lança HttpRequestException em qualquer resposta não-2xx SEM dar
        // acesso ao body do erro — impossível saber o motivo real do 400/429/503.
        //
        // Com SendAsync + ReadAsStringAsync temos controle total:
        //   - Lemos o body ANTES de tomar qualquer ação
        //   - Logamos o JSON de erro do Scryfall (campo "details") para diagnóstico
        //   - Decidimos se pula o ciclo ou lança para o catch do ExecuteAsync
        using var setsRequest = new HttpRequestMessage(HttpMethod.Get, ScryfallSetsUrl);
        using var setsResponse = await client.SendAsync(setsRequest, stoppingToken);

        if (!setsResponse.IsSuccessStatusCode)
        {
            var errorBody = await setsResponse.Content.ReadAsStringAsync(stoppingToken);
            _logger.LogError(
                "[SeedBackground] Scryfall retornou {Status} ao listar sets. Resposta: {Body}",
                (int)setsResponse.StatusCode, errorBody);

            // Não lança — apenas pula o ciclo e aguarda o próximo intervalo
            return;
        }

        var rawJson = await setsResponse.Content.ReadAsStringAsync(stoppingToken);
        var setsJson = Newtonsoft.Json.Linq.JObject.Parse(rawJson);
        var setsData = setsJson["data"] as Newtonsoft.Json.Linq.JArray;

        if (setsData == null || setsData.Count == 0)
        {
            _logger.LogWarning("[SeedBackground] Nenhum set retornado pelo Scryfall.");
            return;
        }

        // Filtra apenas sets de cartas jogáveis (exclui tokens, memorabilia, etc.)
        var setsToCheck = setsData
            .Where(s =>
            {
                string setType = s["set_type"]?.ToString() ?? string.Empty;
                return !string.IsNullOrEmpty(s["code"]?.ToString())
                    && !IgnoredSetTypes.Contains(setType);
            })
            .Select(s => s["code"]!.ToString().ToLower())
            .ToList();

        _logger.LogInformation(
            "[SeedBackground] {Total} sets elegíveis encontrados ({Ignored} ignorados por tipo).",
            setsToCheck.Count, setsData.Count - setsToCheck.Count);

        // Verifica quais sets ainda não estão no catálogo local
        var newSets = new List<string>();

        using (var checkScope = _scopeFactory.CreateScope())
        {
            var repo = checkScope.ServiceProvider.GetRequiredService<IScannerRepository>();

            foreach (var setCode in setsToCheck)
            {
                stoppingToken.ThrowIfCancellationRequested();

                if (!await repo.SetExistsAsync(setCode))
                    newSets.Add(setCode);
            }
        }

        if (newSets.Count == 0)
        {
            _logger.LogInformation("[SeedBackground] Catálogo atualizado — nenhum set novo encontrado.");
            return;
        }

        _logger.LogInformation("[SeedBackground] {Count} set(s) novo(s) para processar: {Sets}",
            newSets.Count, string.Join(", ", newSets.Select(s => s.ToUpper())));

        // Semeia cada set novo em sequência
        // (paralelo seria mais rápido, mas violaria o rate limit do Scryfall)
        foreach (var setCode in newSets)
        {
            stoppingToken.ThrowIfCancellationRequested();

            _logger.LogInformation("[SeedBackground] Iniciando seed do set: {SetCode}", setCode.ToUpper());

            try
            {
                // Cria um escopo próprio para cada set — garante que o DbContext
                // seja descartado corretamente entre seeds (evita memory leak em loop longo)
                using var seedScope = _scopeFactory.CreateScope();
                var scannerService = seedScope.ServiceProvider.GetRequiredService<ScannerService>();

                var (cardsProcessed, embeddingsGenerated) =
                    await scannerService.SeedSetAsync(setCode, _httpClientFactory);

                _logger.LogInformation(
                    "[SeedBackground] Set {SetCode} concluído: {Cards} cartas, {Embeddings} embeddings.",
                    setCode.ToUpper(), cardsProcessed, embeddingsGenerated);
            }
            catch (Exception ex)
            {
                // Falha em um set não para os demais
                _logger.LogError(ex,
                    "[SeedBackground] Falha ao semear o set {SetCode}. Continuando...",
                    setCode.ToUpper());
            }

            // Pausa entre sets para respeitar o rate limit do Scryfall (política de uso justo)
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }

        _logger.LogInformation("[SeedBackground] Ciclo de seed concluído.");
    }

    /// <summary>
    /// Cria um HttpClient configurado com os headers obrigatórios do Scryfall.
    ///
    /// O Scryfall rejeita requisições sem:
    ///   User-Agent : AppName/versão (email-de-contato)  → identifica sua aplicação
    ///   Accept     : application/json                    → garante resposta em JSON
    ///
    /// O .NET adiciona automaticamente seu próprio User-Agent que o Scryfall rejeita com 400,
    /// por isso fazemos Remove() antes de Add() para garantir exatamente um header.
    /// </summary>
    private HttpClient CriarClienteScryfall()
    {
        var client = _httpClientFactory.CreateClient();

        // Remove o User-Agent padrão do .NET antes de sobrescrever com o nosso
        client.DefaultRequestHeaders.Remove("User-Agent");
        client.DefaultRequestHeaders.Add("User-Agent", ScryfallUserAgent);

        // Scryfall retorna 400 se Accept não for application/json
        client.DefaultRequestHeaders.Remove("Accept");
        client.DefaultRequestHeaders.Add("Accept", ScryfallAccept);

        return client;
    }
}