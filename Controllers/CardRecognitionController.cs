using ApiStudy.Repository;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using ApiStudy.Services;
using ApiStudy.Models.Cards;

namespace ApiStudy.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CardRecognitionController : ControllerBase
{
    private readonly IScannerRepository _repo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly VectorService _vectorService;

    public CardRecognitionController(IScannerRepository repo, IHttpClientFactory httpClientFactory, VectorService vectorService)
    {
        _repo = repo;
        _httpClientFactory = httpClientFactory;
        _vectorService = vectorService;
    }

    /// <summary>
    /// Realiza o Seed de uma edição completa vinda do Scryfall.
    /// </summary>
    [HttpGet("seed/{setCode}")]
    public async Task<IActionResult> SeedSet(string setCode)
    {
        if (string.IsNullOrWhiteSpace(setCode)) return BadRequest("Código da edição é obrigatório.");

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("User-Agent", "ApiStudy-MTGScanner");

        string? nextUrl = $"https://api.scryfall.com/cards/search?q=e:{setCode}&unique=prints&include_extras=true";
        int totalProcessed = 0;

        try
        {
            while (!string.IsNullOrEmpty(nextUrl))
            {
                var response = await client.GetStringAsync(nextUrl);
                var json = JObject.Parse(response);
                var data = json["data"] as JArray;

                if (data != null)
                {
                    // Envia o JArray completo para o repositório tratar o lote
                    //await _repo.AddBatchToCatalog(data);
                    totalProcessed += data.Count;
                }

                nextUrl = json["next_page"]?.ToString();

                // Delay para respeitar o rate limit da API externa
                if (!string.IsNullOrEmpty(nextUrl))
                    await Task.Delay(100);
            }

            return Ok(new { Status = "Sucesso", Edicao = setCode.ToUpper(), CartasProcessadas = totalProcessed });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro durante o processamento do set {setCode}: {ex.Message}");
        }
    }

    // Remova o [FromForm] se ele estiver causando o erro no Swagger
    [HttpPost("identify")]
    public async Task<IActionResult> Identify(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("Arquivo inválido.");

        // 1. LER E PROCESSAR A IMAGEM DA CÂMERA
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        byte[] cameraBytes = ms.ToArray();

        // GERA O VETOR E O ARQUIVO debug_ia.jpg
        float[]? queryVector = _vectorService.GenerateEmbedding(cameraBytes, "debug_ia.jpg");
        if (queryVector == null) return StatusCode(500, "Falha ao processar imagem da câmera.");

        // 2. BUSCAR NO BANCO DE DADOS
        dynamic? result = null; // await _repo.IdentifyCardAsync(queryVector);

        if (result?.Card == null) return NotFound(new { message = "Nenhuma carta no banco." });

        // 3. PROCESSO DE COMPARAÇÃO VISUAL (DUELO)
        if (!string.IsNullOrEmpty(result.Card.ImageUrl))
        {
            try
            {
                using var client = new HttpClient();
                // User-Agent para evitar bloqueio do Scryfall
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                Console.WriteLine($"[DEBUG] Baixando referência do banco: {result.Card.ImageUrl}");
                byte[] bancoBytes = await client.GetByteArrayAsync(result.Card.ImageUrl);

                // GERA O ARQUIVO debug_banco.jpg COM O MESMO ALGORITMO
                _vectorService.GenerateEmbedding(bancoBytes, "debug_banco.jpg");

                Console.WriteLine(">>> DUELO GERADO: Compare debug_ia.jpg com debug_banco.jpg");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AVISO] Não foi possível baixar a imagem do banco: {ex.Message}");
            }
        }

        // 4. RESULTADO E LOG
        Console.WriteLine($"[RESULTADO] CARTA: {result.Card.Name} | DISTÂNCIA: {result.Distance:F4}");

        // Threshold de segurança: distâncias menores são melhores
        if (result.Distance > 0.50f)
        {
            return NotFound(new { message = "Baixa confiança no reconhecimento.", distance = result.Distance });
        }

        return Ok(new
        {
            name = result.Card.Name,
            setCode = result.Card.SetCode,
            collectorNumber = result.Card.CollectorNumber,
            distance = result.Distance,
            confidence = Math.Round((1 - result.Distance) * 100, 2)
        });
    }
}