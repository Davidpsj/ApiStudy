using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Tesseract;

namespace ApiStudy.Services;

/// <summary>
/// Resultado do OCR.
/// </summary>
public record OcrResult(string? Title, float Score);

/// <summary>
/// Estágio B da pipeline — lê o título da carta na faixa superior da imagem 488×680px.
///
/// PAPEL DO OCR NA ARQUITETURA:
///   O OCR é AUXILIAR, não primário. O vetor (ResNet18) identifica a carta pela arte.
///   O OCR contribui em dois cenários:
///     1. Confirma o vetor quando ambos concordam (aumenta confiança)
///     2. FindByNameAsync: quando lê o nome com ≥ 70% de confiança e encontra no banco,
///        injeta o candidato correto mesmo que o vetor tenha errado
///
///   Fontes ornamentadas MTG (Goudy Medieval, Matrix) causam erros comuns:
///     P ↔ F, C ↔ G, rn ↔ m, d ↔ cl, etc.
///   Esses erros são esperados e tratados pelo DecisionEngine.
///
/// CALIBRAÇÃO DA REGIÃO DO TÍTULO (empiricamente medida em 488×680):
///
///   ┌────────────────────────────────┐  y=0
///   │ borda externa ~4–6px           │
///   │  ┌──[NOME DA CARTA]──────────┐ │  ← início: y ≈ 24px (3.5%)
///   │  └──────────────────────────-┘ │  ← fim:    y ≈ 65px (9.5%)
///   │  [linha tipo + símbolo set]    │
///   │  [início da arte]              │
///
///   A região foi expandida de 4.1%–8.8% para 3.5%–9.5% da altura
///   para capturar cards com bordas mais espessas (ex: frame pré-M15, borderless)
///   e evitar cortar o ascendente de letras altas como 'l', 'k', 'b', 'f'.
///
///   A largura foi expandida de 60% para 65% para capturar títulos longos
///   como "Consecrated Sphinx" ou "Thoughtseize" sem truncar.
///
/// MODO LSTM:
///   LstmOnly é mais preciso que o modo Legado para fontes não-padrão.
///   O modo Legado (OcrEngineMode.Default) era melhor em fontes monoespacadas;
///   para fontes serifadas como Goudy Medieval, o LSTM tem vantagem significativa.
///
/// Registrado como Singleton no Program.cs.
/// </summary>
public class OcrService
{
    private readonly TesseractEngine _engine;

    // ── Geometria da faixa do título em 488×680 ───────────────────────────────
    //
    // Calibração empírica (v2) — expandida para capturar bordas espessas e letras altas:
    //
    //   TitleTopRatio    = 3.5%  → y ≈ 24px  (antes: 4.1% = 28px)
    //   TitleHeightRatio = 6.0%  → h ≈ 41px  (antes: 4.7% = 32px — muito apertado)
    //   TitleLeftRatio   = 3.5%  → x ≈ 17px  (mantido)
    //   TitleWidthRatio  = 65%   → w ≈ 317px (antes: 60% = 293px)
    //
    // Por que expandir a altura?
    //   32px upscalado 4× = 128px para o Tesseract. Fontes MTG têm ascendentes
    //   que ficam ~8px acima da linha de base. Com 32px de margem, alguns cards
    //   tinham o topo das letras cortado (especialmente 'l', 'k', 'd').
    //   Com 41px (164px upscalado), há espaço suficiente para todos os casos.
    //
    // Por que expandir a largura?
    //   Nomes longos como "Consecrated Sphinx" (18 chars) em alguns frames
    //   chegam até ~63% da largura. 65% dá uma margem de segurança.
    private const float TitleTopRatio = 0.035f;  // y ≈ 24px
    private const float TitleHeightRatio = 0.060f;  // h ≈ 41px
    private const float TitleLeftRatio = 0.035f;  // x ≈ 17px
    private const float TitleWidthRatio = 0.650f;  // w ≈ 317px

    // Confiança mínima do Tesseract para aceitar o resultado.
    // Reduzido de 0.40 para 0.35 — fontes ornamentadas MTG raramente atingem 0.40
    // mesmo quando a leitura está correta. O DecisionEngine filtra por OcrBlockThreshold (0.92)
    // quando usa OCR para bloquear o vetor, então o risco de falso positivo é baixo.
    private const float MinAcceptableConfidence = 0.35f;

    public OcrService(string tessDataPath)
    {
        if (!Directory.Exists(tessDataPath))
            throw new DirectoryNotFoundException(
                $"Pasta tessdata não encontrada: {tessDataPath}.");

        // IMPORTANTE: apenas eng+por.
        // chi_tra, jpn, deu, fra aumentam latência 5-10× sem benefício —
        // nomes de cartas MTG são sempre em caracteres latinos.
        string languages = File.Exists(Path.Combine(tessDataPath, "por.traineddata"))
            ? "eng+por"
            : "eng";

        // LstmOnly: rede neural LSTM pura, mais precisa para fontes não-padrão
        // como Goudy Medieval e Matrix usadas nos títulos de cartas MTG.
        _engine = new TesseractEngine(tessDataPath, languages, EngineMode.LstmOnly);
        _engine.DefaultPageSegMode = PageSegMode.SingleLine;

        // Whitelist: restringe o Tesseract a letras latinas e diacríticos usados em
        // nomes de cartas MTG. Elimina leitura de símbolos CJK e caracteres especiais
        // que o Tesseract "vê" em bordas douradas, arabescos e ornamentos do frame.
        _engine.SetVariable("tessedit_char_whitelist",
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
            "ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖØÙÚÛÜÝàáâãäåæçèéêëìíîïðñòóôõöøùúûüý" +
            " '-");
    }

    /// <summary>
    /// Lê o nome da carta na faixa do título da imagem 488×680px.
    /// </summary>
    public OcrResult ReadCardTitle(byte[] croppedCardBytes)
    {
        if (croppedCardBytes == null || croppedCardBytes.Length == 0)
            return new OcrResult(null, 0f);

        try
        {
            byte[] titleRegion = ExtractTitleRegion(croppedCardBytes);

            using var pix = Pix.LoadFromMemory(titleRegion);
            using var page = _engine.Process(pix);

            float score = page.GetMeanConfidence();

            if (score < MinAcceptableConfidence)
            {
                Console.WriteLine($"[OcrService] Confiança baixa ({score:P0}) — título descartado.");
                return new OcrResult(null, score);
            }

            string raw = page.GetText()?.Trim() ?? string.Empty;
            string cleaned = CleanOcrTitle(raw);

            if (string.IsNullOrWhiteSpace(cleaned))
            {
                Console.WriteLine("[OcrService] Texto vazio após limpeza.");
                return new OcrResult(null, score);
            }

            Console.WriteLine($"[OcrService] Título: \"{cleaned}\" | Score: {score:P0}");
            return new OcrResult(cleaned, score);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OcrService] Falha: {ex.Message}");
            return new OcrResult(null, 0f);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Extrai e pré-processa a faixa do título para o Tesseract.
    ///
    /// Pipeline:
    ///   Crop (região calibrada) → Upscale 4× Lanczos3 → Grayscale
    ///   → Contrast 2.2 → Brightness 1.10 → GaussianSharpen 0.5
    ///
    /// Por que PNG e não JPEG para o Tesseract?
    ///   JPEG introduz artefatos de bloco (8×8px) que o LSTM interpreta como
    ///   caracteres fantasmas, especialmente em fontes serifadas pequenas.
    ///
    /// Por que Contrast 2.2 (antes era 2.5)?
    ///   2.5 saturava demais o fundo dourado em alguns frames, criando halos
    ///   ao redor das letras que o Tesseract interpretava como bordas de caractere.
    ///   2.2 mantém bom contraste sem saturar.
    ///
    /// Por que Brightness 1.10 (antes era 1.15)?
    ///   1.15 clareava demais o fundo de cartas brancas (plains, auras),
    ///   fazendo texto claro desaparecer. 1.10 é mais conservador.
    /// </summary>
    private byte[] ExtractTitleRegion(byte[] cardBytes)
    {
        using var image = Image.Load<Rgb24>(cardBytes);

        int x = Math.Clamp((int)(image.Width * TitleLeftRatio), 0, image.Width - 1);
        int y = Math.Clamp((int)(image.Height * TitleTopRatio), 0, image.Height - 1);
        int w = Math.Clamp((int)(image.Width * TitleWidthRatio), 1, image.Width - x);
        int h = Math.Clamp((int)(image.Height * TitleHeightRatio), 1, image.Height - y);

        // Upscale 4× para o Tesseract — fonte MTG em tamanho nativo tem ~12px de altura,
        // que é pequeno demais para o LSTM. Com 4×, o caractere fica ~48px — adequado.
        int upW = w * 4;
        int upH = h * 4;

        image.Mutate(ctx =>
        {
            ctx.Crop(new Rectangle(x, y, w, h));
            ctx.Resize(upW, upH, KnownResamplers.Lanczos3);
            ctx.Grayscale();
            ctx.Contrast(2.2f);    // empurra fundo para branco, texto para preto
            ctx.Brightness(1.10f); // levemente mais claro sem estourar
            ctx.GaussianSharpen(0.5f); // reforça bordas dos caracteres
        });

        using var ms = new MemoryStream();
        image.SaveAsPng(ms); // PNG preserva nitidez sem artefatos JPEG
        return ms.ToArray();
    }

    /// <summary>
    /// Remove ruídos comuns do OCR em cartas MTG.
    ///
    /// Ordem de limpeza:
    ///   1. Remove símbolos de mana {W}, {2}, {T} — podem aparecer se o crop
    ///      inclui acidentalmente parte do custo de mana
    ///   2. Remove caracteres fora do conjunto latino + espaço + hífen
    ///   3. Normaliza espaços múltiplos
    ///   4. Descarta strings muito curtas (< 2 chars) — provavelmente ruído
    /// </summary>
    private static string CleanOcrTitle(string raw)
    {
        // Remove símbolos de mana que escaparam da whitelist via encodings UTF8 incomuns
        var cleaned = System.Text.RegularExpressions.Regex.Replace(raw, @"\{[^}]*\}", "");

        // Remove qualquer caractere fora do conjunto latino + espaço + hífen + apóstrofe
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"[^A-Za-zÀ-ÿ '\-]", "");

        // Normaliza espaços múltiplos (Tesseract às vezes insere espaços duplos entre letras)
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();

        // Mínimo de 2 caracteres — strings de 1 char são quase sempre ruído
        return cleaned.Length >= 2 ? cleaned : string.Empty;
    }

    // ── Debug (descomente para ativar) ────────────────────────────────────────

    // /// <summary>
    // /// Salva a região processada do título em /tmp para inspeção visual.
    // /// Útil para calibrar os ratios TitleTopRatio/TitleHeightRatio.
    // /// Ative apenas em desenvolvimento — não use em produção.
    // /// </summary>
    // private static void SalvarDebug(byte[] pngBytes)
    // {
    //     var path = $"/tmp/ocr_debug_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.png";
    //     File.WriteAllBytes(path, pngBytes);
    //     Console.WriteLine($"[OcrService] Debug salvo em: {path}");
    // }
}