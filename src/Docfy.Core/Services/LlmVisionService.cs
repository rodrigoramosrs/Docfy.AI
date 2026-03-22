using Docfy.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;

namespace Docfy.Core.Services;

public class LlmVisionService : ILlmVisionService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LlmVisionService> _logger;

    public LlmVisionService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<LlmVisionService> logger)
    {
        _httpClient = httpClientFactory?.CreateClient("LlmClient") ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var apiKey = _configuration["LlmVision:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("LlmVision:ApiKey não configurada");

        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        
        if (int.TryParse(_configuration["LlmVision:RequestTimeoutSeconds"], out int requestTimeoutSeconds) && requestTimeoutSeconds > 0)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds);
        }
        else
        {
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }
    }

    public async Task<ImageAnalysisResult> AnalyzeImageAsync(ExtractedImage image)
    {
        int attempt = 0;
        int maxAttempts = 10;
        double currentScale = 1.0;
        byte[] currentImageData = image.Data;
        int finalWidth = (int)image.Width;
        int finalHeight = (int)image.Height;
        string resizeInfo = "";
        if(!Directory.Exists("output"))
        {
            Directory.CreateDirectory("output");
        }
        while (attempt < maxAttempts)
        {
            try
            {
                var base64Image = Convert.ToBase64String(currentImageData);
                var prompt = BuildAnalysisPrompt();

                var requestBody = new
                {
                    model = _configuration["LlmVision:Model"] ?? "gpt-4-vision-preview",
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "text", text = prompt },
                                new
                                {
                                    type = "image_url",
                                    image_url = new { url = $"data:{image.MimeType};base64,{base64Image}" }
                                }
                            }
                        }
                    },
                    max_tokens = int.Parse(_configuration["LlmVision:MaxTokens"] ?? "4096"),
                    temperature = 0.1
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var endpoint = _configuration["LlmVision:Endpoint"] ??
                    "https://api.openai.com/v1/chat/completions";

                var response = await _httpClient.PostAsync(endpoint, content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = ParseResponse(responseJson, image, base64Image);

                    if (currentScale < 1.0)
                    {
                        int percentage = (int)(currentScale * 100);
                        resizeInfo = $" [Processado com {percentage}% do tamanho original: {finalWidth}x{finalHeight}px]";
                        result.Description = result.Description + resizeInfo;
                    }

                    result.Width = finalWidth;
                    result.Height = finalHeight;
                    result.ProcessedScale = currentScale;

                    return result;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.RequestEntityTooLarge ||
                    responseJson.Contains("image") ||
                    responseJson.Contains("size") ||
                    responseJson.Contains("too large") ||
                    responseJson.Contains("maximum") ||
                    responseJson.Contains("dimension"))
                {
                    _logger.LogWarning(
                        "Imagem muito grande para API (tentativa {Attempt}, escala {Scale}%). " +
                        "Reduzindo 10%... Status: {StatusCode}",
                        attempt + 1, (int)(currentScale * 100), response.StatusCode);

                    currentScale -= 0.1;

                    if (currentScale < 0.1)
                    {
                        _logger.LogError("Imagem muito pequena após reduções, abortando");
                        break;
                    }

                    var resized = ResizeImageRobust(image.Data, currentScale, image.MimeType);

                    if (resized.Success)
                    {
                        currentImageData = resized.Data;
                        finalWidth = resized.Width;
                        finalHeight = resized.Height;
                    }
                    else
                    {
                        _logger.LogWarning("Falha no redimensionamento, tentando truncar bytes...");
                        currentImageData = TruncateImageData(image.Data, currentScale);
                        finalWidth = (int)(image.Width * currentScale);
                        finalHeight = (int)(image.Height * currentScale);
                    }

                    attempt++;
                    continue;
                }

                _logger.LogError("Erro na API LLM: {StatusCode} - {Response}",
                    response.StatusCode, responseJson);
                throw new HttpRequestException($"API LLM retornou erro: {response.StatusCode}");
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("413") ||
                                                  ex.Message.Contains("RequestEntityTooLarge") ||
                                                  ex.Message.Contains("timeout") ||
                                                  ex.Message.Contains("too large"))
            {
                _logger.LogWarning(
                    "Erro de tamanho/timeout na tentativa {Attempt} (escala {Scale}%). " +
                    "Reduzindo 10%...",
                    attempt + 1, (int)(currentScale * 100));

                currentScale -= 0.1;

                if (currentScale < 0.1)
                {
                    _logger.LogError("Limite mínimo de escala atingido");
                    break;
                }

                var resized = ResizeImageRobust(image.Data, currentScale, image.MimeType);

                if (resized.Success)
                {
                    currentImageData = resized.Data;
                    finalWidth = resized.Width;
                    finalHeight = resized.Height;
                }
                else
                {
                    _logger.LogWarning("Falha no redimensionamento robusto, usando truncagem...");
                    currentImageData = TruncateImageData(image.Data, currentScale);
                    finalWidth = (int)(image.Width * currentScale);
                    finalHeight = (int)(image.Height * currentScale);
                }

                attempt++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado na análise da imagem");
                break;
            }
        }

        _logger.LogError("Todas as tentativas de análise falharam para imagem da página {Page}",
            image.PageNumber);

        return new ImageAnalysisResult
        {
            PageNumber = image.PageNumber,
            ImageIndex = image.ImageIndex,
            ImageBase64 = $"data:{image.MimeType};base64,{Convert.ToBase64String(image.Data)}",
            MimeType = image.MimeType,
            Width = (int)image.Width,
            Height = (int)image.Height,
            Size = image.Data.Length,
            ImageHash = CalculateHash(image.Data),
            Description = $"[Erro ao processar imagem após {attempt} tentativas de redimensionamento. " +
                         $"Última escala tentada: {(int)(currentScale * 100)}% ({finalWidth}x{finalHeight}px)]",
            ContentType = "Error",
            IsDecorative = false,
            Confidence = 0,
            ProcessedScale = currentScale
        };
    }

    /// <summary>
    /// Tenta redimensionar a imagem de múltiplas formas, nunca falha completamente
    /// </summary>
    private (bool Success, byte[] Data, int Width, int Height) ResizeImageRobust(
        byte[] originalData,
        double scale,
        string mimeType)
    {
        if (originalData == null || originalData.Length == 0)
        {
            return (false, Array.Empty<byte>(), 0, 0);
        }

        // Estratégia 1: Tentar carregar com ImageSharp usando detecção automática
        try
        {
            using var inputStream = new MemoryStream(originalData);
            using var image = Image.Load(inputStream);

            int newWidth = Math.Max((int)(image.Width * scale), 100);
            int newHeight = Math.Max((int)(image.Height * scale), 100);

            image.Mutate(x => x.Resize(newWidth, newHeight));

            using var outputStream = new MemoryStream();

            // Salvar no formato original se possível
            if (mimeType.Contains("jpeg") || mimeType.Contains("jpg"))
            {
                image.Save(outputStream, new JpegEncoder { Quality = 85 });
            }
            else if (mimeType.Contains("png"))
            {
                image.Save(outputStream, new PngEncoder());
            }
            else if (mimeType.Contains("bmp"))
            {
                image.Save(outputStream, new BmpEncoder());
            }
            else if (mimeType.Contains("gif"))
            {
                image.Save(outputStream, new GifEncoder());
            }
            else if (mimeType.Contains("webp"))
            {
                image.Save(outputStream, new WebpEncoder());
            }
            else if (mimeType.Contains("tiff") || mimeType.Contains("tif"))
            {
                image.Save(outputStream, new TiffEncoder());
            }
            else if (mimeType.Contains("tga"))
            {
                image.Save(outputStream, new TgaEncoder());
            }
            else
            {
                // Fallback para PNG
                image.Save(outputStream, new PngEncoder());
            }

            return (true, outputStream.ToArray(), newWidth, newHeight);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ImageSharp falhou ao carregar imagem, tentando conversão...");
        }

        // Estratégia 2: Tentar converter para PNG usando SkiaSharp (se disponível) ou outra biblioteca
        // Por enquanto, tentamos manipular os bytes diretamente

        // Estratégia 3: Criar uma imagem placeholder com metadados
        try
        {
            return CreatePlaceholderImage((int)(originalData.Length * scale), scale);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao criar placeholder");
        }

        // Estratégia 4: Último recurso - truncar dados e retornar como "imagem genérica"
        try
        {
            var truncated = TruncateImageData(originalData, scale);
            return (true, truncated, 0, 0);
        }
        catch
        {
            return (false, originalData, 0, 0);
        }
    }

    /// <summary>
    /// Cria uma imagem PNG mínima válida para representar dados corrompidos/não suportados
    /// </summary>
    private (bool Success, byte[] Data, int Width, int Height) CreatePlaceholderImage(
        int targetBytes,
        double scale)
    {
        try
        {
            // Criar uma imagem 1x1 pixels ou proporcional ao tamanho desejado
            int width = Math.Max((int)(100 * scale), 10);
            int height = Math.Max((int)(100 * scale), 10);

            using var image = new Image<Rgba32>(width, height);

            // Preencher com cor cinza indicando "dados não processáveis"
            image.Mutate(x => x.BackgroundColor(new Rgba32(128, 128, 128, 255)));

            using var outputStream = new MemoryStream();
            image.Save(outputStream, new PngEncoder());

            var data = outputStream.ToArray();

            // Se ainda for maior que o target, truncar
            if (data.Length > targetBytes && targetBytes > 100)
            {
                data = data.Take(targetBytes).ToArray();
            }

            return (true, data, width, height);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao criar imagem placeholder");
            return (false, Array.Empty<byte>(), 0, 0);
        }
    }

    /// <summary>
    /// Trunca os bytes da imagem para simular redução de tamanho
    /// Último recurso quando nenhuma biblioteca consegue processar o formato
    /// </summary>
    private byte[] TruncateImageData(byte[] originalData, double scale)
    {
        if (originalData == null || originalData.Length == 0)
        {
            return Array.Empty<byte>();
        }

        int newLength = Math.Max((int)(originalData.Length * scale), 100);

        // Criar um array menor copiando o início dos dados
        var truncated = new byte[newLength];
        Buffer.BlockCopy(originalData, 0, truncated, 0, Math.Min(newLength, originalData.Length));

        _logger.LogInformation("Dados truncados de {Original} para {New} bytes ({Scale}%)",
            originalData.Length, newLength, (int)(scale * 100));

        return truncated;
    }

    private byte[] CalculateHash(byte[] data)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        return sha256.ComputeHash(data);
    }

    private string BuildAnalysisPrompt()
    {
        return @"Analise esta imagem extraída de um documento PDF e classifique-a seguindo estas regras:

1. SE for ícone, logo, seta, bullet decorativo ou elemento puramente decorativo:
   - Responda apenas: DECORATIVE

2. SE conter texto legível (parágrafos, títulos, labels):
   - Extraia TODO o texto mantendo a formatação
   - Responda: TEXT: [texto extraído]

3. SE for gráfico, tabela, diagrama ou fluxograma:
   - Descreva o que representa em linguagem natural detalhada
   - Responda: CHART: [descrição detalhada]

4. SE for código, configuração, request HTTP, JSON, XML ou comando técnico:
   - Extraia o conteúdo exato preservando sintaxe
   - Responda: CODE:[linguagem]: [conteúdo do código]

5. SE for captura de tela de interface ou aplicativo:
   - Descreva a interface e elementos visíveis
   - Responda: UI: [descrição]

Responda APENAS no formato especificado acima, sem explicações adicionais.";
    }

    private ImageAnalysisResult ParseResponse(string jsonResponse, ExtractedImage image, string base64Image)
    {
        var content = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            var result = new ImageAnalysisResult
            {
                PageNumber = image.PageNumber,
                ImageIndex = image.ImageIndex,
                ImageBase64 = $"data:{image.MimeType};base64,{base64Image}",
                MimeType = image.MimeType,
                Width = (int)image.Width,
                Height = (int)image.Height,
                Size = image.Data.Length,
                ImageHash = CalculateHash(image.Data)
            };

            if (content.StartsWith("DECORATIVE", StringComparison.OrdinalIgnoreCase))
            {
                result.IsDecorative = true;
                result.ContentType = "Decorative";
                result.Description = "Imagem decorativa (ícone, logo, ou elemento visual sem conteúdo semântico)";
                result.Confidence = 0.95;
            }
            else if (content.StartsWith("TEXT:", StringComparison.OrdinalIgnoreCase))
            {
                result.Description = content.Substring(5).Trim();
                result.ContentType = "Text";
                result.Confidence = 0.9;
            }
            else if (content.StartsWith("CHART:", StringComparison.OrdinalIgnoreCase))
            {
                result.Description = content.Substring(6).Trim();
                result.ContentType = "Chart";
                result.Confidence = 0.85;
            }
            else if (content.StartsWith("CODE:", StringComparison.OrdinalIgnoreCase))
            {
                var codePart = content.Substring(5);
                var colonIndex = codePart.IndexOf(':');

                if (colonIndex > 0)
                {
                    result.CodeLanguage = codePart.Substring(0, colonIndex).Trim();
                    result.Description = codePart.Substring(colonIndex + 1).Trim();
                }
                else
                {
                    result.Description = codePart.Trim();
                }

                result.ContentType = "Code";
                result.Confidence = 0.9;
            }
            else if (content.StartsWith("UI:", StringComparison.OrdinalIgnoreCase))
            {
                result.Description = content.Substring(3).Trim();
                result.ContentType = "UI";
                result.Confidence = 0.8;
            }
            else
            {
                result.Description = content;
                result.ContentType = "Unknown";
                result.Confidence = 0.5;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao parsear resposta da LLM");

            return new ImageAnalysisResult
            {
                PageNumber = image.PageNumber,
                ImageIndex = image.ImageIndex,
                ImageBase64 = $"data:{image.MimeType};base64,{base64Image}",
                MimeType = image.MimeType,
                Width = (int)image.Width,
                Height = (int)image.Height,
                Size = image.Data.Length,
                ImageHash = CalculateHash(image.Data),
                Description = content,
                ContentType = "Raw",
                Confidence = 0
            };
        }
    }
}