using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ApiStudy.Services;

/// <summary>
/// Gera embeddings de 512 dimensões a partir da ARTE de cartas MTG usando ResNet18 via ONNX.
///
/// MUDANÇA CRÍTICA EM RELAÇÃO À VERSÃO ANTERIOR:
///   Antes: processava a carta inteira (488×680px)
///   Agora: processa apenas a região da arte (~8%–85% da altura)
///
/// POR QUE SOMENTE A ARTE?
///
///   Uma carta MTG tem regiões com conteúdo visual distinto:
///
///   ┌──────────────────────┐  y=0%
///   │ TÍTULO + CUSTO MANA  │  y=0–8%    ← letras, símbolos — iguais em todas as impressões
///   ├──────────────────────┤  y=8%
///   │                      │
///   │   ARTE DA CARTA      │  y=8–85%   ← ÚNICO por impressão ← processamos aqui
///   │                      │
///   ├──────────────────────┤  y=85%
///   │ TIPO + SÍMBOLO SET   │  y=85–90%  ← texto — confunde o modelo
///   ├──────────────────────┤  y=90%
///   │ TEXTO DE REGRAS      │  y=90–96%  ← letras miúdas — ruído puro para o ResNet18
///   ├──────────────────────┤  y=96%
///   │ PODER/RES + RODAPÉ   │  y=96–100% ← números — irrelevantes para identificação visual
///   └──────────────────────┘  y=100%
///
/// IMPACTO PRÁTICO:
///   Plains full art ONE: a arte é uma paisagem Phyrexiana única.
///   Com a carta inteira, o ResNet18 via via "frame dourado + texto preto" como feature
///   dominante — comum a TODAS as Plains — e confundia com Drake-Skull Cameo (fundo escuro similar).
///   Com apenas a arte, o modelo compara paisagens vs criaturas/artefatos — sem confusão.
///
/// Registrado como Singleton — InferenceSession (~45MB) é pesado para instanciar por requisição.
/// OnnxRuntime é thread-safe para chamadas concorrentes ao Run().
/// </summary>
public class VectorService
{
    private readonly InferenceSession _session;

    // Normalização ImageNet — deve ser idêntica à usada no treinamento do ResNet18
    private static readonly float[] Mean = { 0.485f, 0.456f, 0.406f };
    private static readonly float[] Std = { 0.229f, 0.224f, 0.225f };

    // ── Região da arte em uma carta 488×680px ─────────────────────────────────
    //
    // Medições empíricas sobre imagens Scryfall "normal" (488×680):
    //
    //   Título ocupa:   y ≈ 0px  – 55px    →  0% –  8%
    //   Arte ocupa:     y ≈ 55px – 577px   →  8% – 85%
    //   Tipo/texto:     y ≈ 577px – 680px  → 85% – 100%
    //
    // Margem horizontal: 3% de cada lado para excluir a borda do frame.
    //
    // NOTA: estes ratios funcionam para cartas do frame moderno (M15+).
    //       Cartas do frame antigo (pre-M15) têm proporções ligeiramente diferentes,
    //       mas a diferença é pequena o suficiente para não afetar a qualidade do embedding.
    private const float ArtTopRatio = 0.081f;  // y ≈ 55px em 680
    private const float ArtHeightRatio = 0.764f;  // h ≈ 520px (até y≈575px)
    private const float ArtLeftRatio = 0.030f;  // x ≈ 15px em 488
    private const float ArtWidthRatio = 0.940f;  // w ≈ 459px

    public VectorService(string modelPath)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"Modelo ONNX não encontrado: {modelPath}");

        _session = new InferenceSession(modelPath);
    }

    /// <summary>
    /// Gera o vetor de embedding a partir da imagem recortada da carta (488×680px).
    /// Processa apenas a região da arte para maximizar discriminação entre impressões.
    /// Retorna null se a imagem for inválida ou o modelo falhar.
    /// </summary>
    public float[]? GenerateEmbedding(byte[] imageBytes)
    {
        if (imageBytes == null || imageBytes.Length == 0)
            return null;

        try
        {
            using var image = Image.Load<Rgb24>(imageBytes);

            // ── Crop da região da arte ──────────────────────────────────────
            // Remove título, tipo, texto de regras e rodapé.
            // Mantém apenas a ilustração — única por impressão.
            int x = Math.Clamp((int)(image.Width * ArtLeftRatio), 0, image.Width - 1);
            int y = Math.Clamp((int)(image.Height * ArtTopRatio), 0, image.Height - 1);
            int w = Math.Clamp((int)(image.Width * ArtWidthRatio), 1, image.Width - x);
            int h = Math.Clamp((int)(image.Height * ArtHeightRatio), 1, image.Height - y);

            image.Mutate(ctx =>
            {
                ctx.Crop(new Rectangle(x, y, w, h));
                // Resize para 224×224 — entrada padrão do ResNet18
                // Triangle (bilinear) é mais rápido que Lanczos3 e suficiente para embeddings
                ctx.Resize(224, 224, KnownResamplers.Triangle);
            });

            // ── Normalização ImageNet ───────────────────────────────────────
            // Converte HWC (Height×Width×Channel) para CHW (Channel×Height×Width)
            // e aplica mean/std por canal, como o ResNet18 espera.
            var input = new DenseTensor<float>(new[] { 1, 3, 224, 224 });

            image.ProcessPixelRows(accessor =>
            {
                for (int row = 0; row < 224; row++)
                {
                    var rowSpan = accessor.GetRowSpan(row);
                    for (int col = 0; col < 224; col++)
                    {
                        input[0, 0, row, col] = (rowSpan[col].R / 255f - Mean[0]) / Std[0];
                        input[0, 1, row, col] = (rowSpan[col].G / 255f - Mean[1]) / Std[1];
                        input[0, 2, row, col] = (rowSpan[col].B / 255f - Mean[2]) / Std[2];
                    }
                }
            });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("data", input)
            };

            using var results = _session.Run(inputs);
            return results.First().AsEnumerable<float>().ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VectorService] Falha ao gerar embedding: {ex.Message}");
            return null;
        }
    }
}