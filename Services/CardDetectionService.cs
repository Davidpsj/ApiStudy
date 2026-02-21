// Emgu.CV usa System.Drawing internamente.
// Para evitar ambiguidades com SixLabors.ImageSharp, NÃO importamos nenhum dos dois
// via using genérico: todos os tipos ambíguos são qualificados com namespace completo.
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ApiStudy.Services;

/// <summary>
/// Estágio 0 da pipeline de reconhecimento.
///
/// Responsabilidade: receber a imagem bruta da câmera (fundo, mão, mesa, etc.)
/// e devolver apenas a carta recortada, endireitada e padronizada em 488×680px
/// (proporção 2.5×3.5 de uma carta MTG real).
///
/// Estratégia híbrida:
///   1. Tenta Emgu.CV: Canny → FindContours → filtro quadrilátero → WarpPerspective
///      Resultado: carta perfeitamente endireitada mesmo fotografada em ângulo
///   2. Se falhar (sem contorno confiável, exceção, runtime nativo ausente):
///      Fallback ImageSharp: crop central por proporção MTG + ajuste de contraste
///
/// Registrado como Singleton — sem estado mutável, operações stateless sobre bytes.
///
/// Nota sobre MorphShapes/GetStructuringElement:
///   O enum MorphShapes existe no Emgu.CV 4.x mas seus valores variam entre builds
///   (Rectangle vs Rect vs outros). Para evitar erros de compilação, passamos null
///   como kernel no CvInvoke.Dilate — o OpenCV usa automaticamente um kernel 3×3
///   retangular, que é exatamente o que precisamos para fechar lacunas de bordas.
/// </summary>
public class CardDetectionService
{
    // Tamanho de saída padronizado — 488×680 mantém a proporção exata 2.5×3.5 de uma carta MTG
    private const int OutputWidth = 488;
    private const int OutputHeight = 680;

    // Área mínima aceitável para um contorno ser considerado uma carta (5% da imagem)
    private const double MinCardAreaRatio = 0.05;

    /// <summary>
    /// Detecta e recorta a carta na imagem da câmera.
    /// Sempre retorna bytes válidos — nunca propaga exceção ao chamador.
    /// </summary>
    public byte[] DetectAndCrop(byte[] rawImageBytes)
    {
        if (rawImageBytes == null || rawImageBytes.Length == 0)
            return rawImageBytes ?? Array.Empty<byte>();

        try
        {
            var result = DetectWithEmguCv(rawImageBytes);
            if (result != null)
                return result;
        }
        catch (Exception ex)
        {
            // Runtime nativo pode estar ausente em Linux/Docker sem libopencv instalado
            Console.WriteLine(
                $"[CardDetection] Emgu.CV falhou ({ex.GetType().Name}): {ex.Message}. " +
                "Usando fallback ImageSharp.");
        }

        return CropWithImageSharp(rawImageBytes);
    }

    // ── CAMINHO PRINCIPAL: Emgu.CV ────────────────────────────────────────────

    /// <summary>
    /// Detecta a carta via visão computacional:
    ///   1. Escala de cinza + blur Gaussiano (reduz ruído de sensor)
    ///   2. Canny (detecta bordas com histerese dupla)
    ///   3. Dilate com kernel null (OpenCV usa 3×3 retangular por padrão —
    ///      fecha pequenas lacunas nas bordas sem depender do enum MorphShapes)
    ///   4. FindContours → filtra pelo maior quadrilátero convexo
    ///   5. WarpPerspective (endireita a perspectiva)
    /// Retorna null se nenhum contorno adequado for encontrado.
    /// </summary>
    private static byte[]? DetectWithEmguCv(byte[] rawImageBytes)
    {
        using var mat = new Mat();

        // ImreadModes.ColorBgr: nome correto no Emgu.CV 4.x para carregamento em 3 canais BGR
        // (versões antigas usavam ImreadModes.Color, que foi renomeado)
        CvInvoke.Imdecode(rawImageBytes, ImreadModes.ColorBgr, mat);
        if (mat.IsEmpty) return null;

        double imageArea = mat.Width * mat.Height;

        // Escala de cinza — Canny opera em imagem single-channel
        using var gray = new Mat();
        using var blurred = new Mat();
        CvInvoke.CvtColor(mat, gray, ColorConversion.Bgr2Gray);

        // System.Drawing.Size qualificado explicitamente para evitar conflito com ImageSharp
        CvInvoke.GaussianBlur(gray, blurred, new System.Drawing.Size(5, 5), 0);

        // Canny: threshold1=50 (bordas fracas que conectam bordas fortes),
        //        threshold2=150 (bordas fortes — borda da carta vs fundo)
        using var edges = new Mat();
        CvInvoke.Canny(blurred, edges, 50, 150);

        // Dilate com kernel null: o OpenCV usa automaticamente um kernel 3×3 retangular.
        // Isso fecha pequenas lacunas nas bordas detectadas pelo Canny, tornando os
        // contornos mais contínuos e fáceis de aproximar com ApproxPolyDP.
        // IMPORTANTE: a assinatura do Emgu.CV é posicional — NÃO use argumentos nomeados
        // (kernel:, iterations: etc.) pois o wrapper P/Invoke não os declara.
        CvInvoke.Dilate(edges, edges, null, new System.Drawing.Point(-1, -1), 1, BorderType.Default, new MCvScalar());

        // Encontra todos os contornos externos na imagem de bordas
        using var contours = new VectorOfVectorOfPoint();
        using var hierarchy = new Mat();
        CvInvoke.FindContours(
            edges, contours, hierarchy,
            RetrType.External,
            ChainApproxMethod.ChainApproxSimple);

        System.Drawing.Point[]? cardCorners = FindCardContour(contours, imageArea);
        if (cardCorners == null) return null;

        return ApplyWarpPerspective(mat, cardCorners);
    }

    /// <summary>
    /// Itera os contornos e retorna os 4 cantos do maior quadrilátero convexo
    /// que ocupe pelo menos MinCardAreaRatio da área total da imagem.
    /// </summary>
    private static System.Drawing.Point[]? FindCardContour(
        VectorOfVectorOfPoint contours, double imageArea)
    {
        System.Drawing.Point[]? bestContour = null;
        double bestArea = 0;

        for (int i = 0; i < contours.Size; i++)
        {
            using var contour = contours[i];

            // ApproxPolyDP: simplifica o contorno para um polígono.
            // epsilon = 2% do perímetro — trade-off padrão para formas retangulares.
            double perimeter = CvInvoke.ArcLength(contour, true);
            using var approx = new VectorOfPoint();
            CvInvoke.ApproxPolyDP(contour, approx, 0.02 * perimeter, true);

            // Uma carta é um quadrilátero (4 vértices) convexo
            if (approx.Size != 4 || !CvInvoke.IsContourConvex(approx))
                continue;

            double area = CvInvoke.ContourArea(approx);
            if (area < imageArea * MinCardAreaRatio)
                continue;

            if (area > bestArea)
            {
                bestArea = area;
                bestContour = approx.ToArray();
            }
        }

        return bestContour;
    }

    /// <summary>
    /// Aplica transformação de perspectiva (WarpPerspective) para produzir
    /// uma vista frontal plana da carta.
    ///
    /// System.Drawing.PointF qualificado explicitamente para evitar ambiguidade
    /// com SixLabors.ImageSharp.PointF.
    /// </summary>
    private static byte[] ApplyWarpPerspective(Mat source, System.Drawing.Point[] corners)
    {
        var ordered = OrderPoints(corners);

        // Pontos de destino: retângulo de saída padronizado 488×680
        var dst = new System.Drawing.PointF[]
        {
            new System.Drawing.PointF(0,           0),
            new System.Drawing.PointF(OutputWidth, 0),
            new System.Drawing.PointF(OutputWidth, OutputHeight),
            new System.Drawing.PointF(0,           OutputHeight),
        };

        // Converte System.Drawing.Point[] → System.Drawing.PointF[]
        var src = ordered
            .Select(p => new System.Drawing.PointF(p.X, p.Y))
            .ToArray();

        // VectorOfPointF: wrapper do Emgu.CV para passar arrays de PointF ao OpenCV.
        // GetPerspectiveTransform calcula a matriz 3×3 de transformação de perspectiva.
        using var perspMatrix = CvInvoke.GetPerspectiveTransform(
            new VectorOfPointF(src),
            new VectorOfPointF(dst));

        using var warped = new Mat();
        CvInvoke.WarpPerspective(
            source, warped, perspMatrix,
            new System.Drawing.Size(OutputWidth, OutputHeight));

        // Encode em JPEG com qualidade 92 (equilíbrio tamanho/fidelidade)
        var buffer = new VectorOfByte();
        CvInvoke.Imencode(
            ".jpg", warped, buffer,
            new KeyValuePair<ImwriteFlags, int>(ImwriteFlags.JpegQuality, 92));

        return buffer.ToArray();
    }

    /// <summary>
    /// Ordena 4 pontos no sentido horário: [topo-esq, topo-dir, baixo-dir, baixo-esq].
    /// Algoritmo baseado em soma/diferença de coordenadas:
    ///   - topo-esquerdo  = menor  (x+y)
    ///   - baixo-direito  = maior  (x+y)
    ///   - topo-direito   = menor  (y-x)
    ///   - baixo-esquerdo = maior  (y-x)
    /// </summary>
    private static System.Drawing.Point[] OrderPoints(System.Drawing.Point[] pts)
    {
        int[] sums = pts.Select(p => p.X + p.Y).ToArray();
        int[] diffs = pts.Select(p => p.Y - p.X).ToArray();

        return new[]
        {
            pts[Array.IndexOf(sums,  sums.Min())],   // topo-esquerdo
            pts[Array.IndexOf(diffs, diffs.Min())],  // topo-direito
            pts[Array.IndexOf(sums,  sums.Max())],   // baixo-direito
            pts[Array.IndexOf(diffs, diffs.Max())],  // baixo-esquerdo
        };
    }

    // ── FALLBACK: SixLabors.ImageSharp ───────────────────────────────────────

    /// <summary>
    /// Fallback 100% gerenciado — sem dependência de runtime nativo.
    /// Crop central com proporção 2.5×3.5 + melhorias para OCR e vetor.
    ///
    /// Todos os tipos do ImageSharp são qualificados com namespace completo
    /// para evitar conflito com System.Drawing, que está em escopo via Emgu.CV.
    /// </summary>
    private static byte[] CropWithImageSharp(byte[] rawImageBytes)
    {
        using var image = SixLabors.ImageSharp.Image
            .Load<SixLabors.ImageSharp.PixelFormats.Rgb24>(rawImageBytes);

        // Calcula crop máximo que respeita a proporção MTG (488/680 ≈ 0.7176)
        int cropWidth = image.Width;
        int cropHeight = (int)(image.Width * (OutputHeight / (float)OutputWidth));

        if (cropHeight > image.Height)
        {
            cropHeight = image.Height;
            cropWidth = (int)(image.Height * (OutputWidth / (float)OutputHeight));
        }

        int left = (image.Width - cropWidth) / 2;
        int top = (image.Height - cropHeight) / 2;

        image.Mutate(ctx =>
        {
            ctx.Crop(new SixLabors.ImageSharp.Rectangle(left, top, cropWidth, cropHeight));

            // Ajustes que beneficiam tanto OCR (contraste) quanto o vetor (nitidez)
            ctx.Contrast(1.15f);
            ctx.Brightness(1.05f);
            ctx.GaussianSharpen(0.8f);

            ctx.Resize(OutputWidth, OutputHeight,
                SixLabors.ImageSharp.Processing.KnownResamplers.Lanczos3);
        });

        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms,
            new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 92 });
        return ms.ToArray();
    }
}