using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Kernel.Pdf.Xobject;
using iText.IO.Image;
using Docfy.Models;

namespace Docfy.Services;

public class PdfExtractionService : IPdfExtractionService
{
    private readonly ILogger<PdfExtractionService> _logger;

    public PdfExtractionService(ILogger<PdfExtractionService> logger)
    {
        _logger = logger;
    }

    public async Task<List<ExtractedPage>> ExtractContentAsync(byte[] pdfBytes)
    {
        return await Task.Run(() =>
        {
            var pages = new List<ExtractedPage>();

            using var stream = new MemoryStream(pdfBytes);
            using var pdfDoc = new PdfDocument(new PdfReader(stream));

            int numberOfPages = pdfDoc.GetNumberOfPages();
            _logger.LogInformation("Processando PDF com {Pages} páginas", numberOfPages);

            for (int i = 1; i <= numberOfPages; i++)
            {
                var page = pdfDoc.GetPage(i);
                var extractedPage = new ExtractedPage
                {
                    PageNumber = i
                };

                var textChunks = ExtractTextWithPosition(page);
                extractedPage.TextChunks = textChunks;
                extractedPage.Text = string.Join(" ", textChunks.Select(t => t.Text));

                extractedPage.Images = ExtractImagesSafe(page, i);

                foreach (var img in extractedPage.Images)
                {
                    img.Hash = CalculateHash(img.Data);
                }

                pages.Add(extractedPage);
            }

            return pages;
        });
    }

    private List<Models.TextChunk> ExtractTextWithPosition(PdfPage page)
    {
        var chunks = new List<Models.TextChunk>();
        var listener = new LocationTextExtractionStrategy();

        var text = PdfTextExtractor.GetTextFromPage(page, listener);

        if (!string.IsNullOrWhiteSpace(text))
        {
            chunks.Add(new Models.TextChunk
            {
                Text = text,
                FontSize = 12,
                IsBold = false,
                IsItalic = false
            });
        }

        return chunks;
    }

    private List<ExtractedImage> ExtractImagesSafe(PdfPage page, int pageNumber)
    {
        var images = new List<ExtractedImage>();
        var resources = page.GetResources();

        if (resources == null) return images;

        var xObjects = resources.GetResourceNames(PdfName.XObject);
        if (xObjects == null) return images;

        int imageIndex = 0;

        foreach (var name in xObjects)
        {
            var xObject = resources.GetResourceObject(PdfName.XObject, name);

            if (xObject is PdfStream stream && stream.Get(PdfName.Subtype)?.Equals(PdfName.Image) == true)
            {
                try
                {
                    var imageData = ExtractImageDataSafe(stream);

                    if (imageData == null || imageData.Length == 0)
                    {
                        _logger.LogWarning("Imagem {Name} na página {Page} retornou dados vazios",
                            name, pageNumber);
                        continue;
                    }

                    var width = stream.GetAsNumber(PdfName.Width)?.IntValue() ?? 0;
                    var height = stream.GetAsNumber(PdfName.Height)?.IntValue() ?? 0;

                    if (width < 20 || height < 20)
                    {
                        _logger.LogDebug("Ignorando imagem pequena: {Width}x{Height}", width, height);
                        continue;
                    }

                    var mimeType = DetectMimeType(stream, imageData);

                    images.Add(new ExtractedImage
                    {
                        PageNumber = pageNumber,
                        ImageIndex = imageIndex++,
                        Data = imageData,
                        Width = width,
                        Height = height,
                        MimeType = mimeType
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao extrair imagem {Name} da página {Page}",
                        name, pageNumber);
                }
            }
        }

        return images;
    }

    /// <summary>
    /// Extrai dados da imagem de forma segura, tentando converter se necessário
    /// </summary>
    private byte[] ExtractImageDataSafe(PdfStream stream)
    {
        try
        {
            // Tentar obter bytes diretamente
            var bytes = stream.GetBytes();

            if (bytes != null && bytes.Length > 0)
            {
                // Verificar se é um formato válido tentando detectar magic bytes
                if (IsValidImageFormat(bytes))
                {
                    return bytes;
                }

                _logger.LogDebug("Formato de imagem não reconhecido, tentando converter...");
            }

            // Se não conseguiu bytes válidos, tentar converter para PNG usando iText
            try
            {
                var pdfImage = new PdfImageXObject(stream);
                // pdfImage.GetImageBytes() pode retornar dados processados
                var processedBytes = pdfImage.GetImageBytes();

                if (processedBytes != null && processedBytes.Length > 0)
                {
                    return processedBytes;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Falha ao converter usando PdfImageXObject");
            }

            // Último recurso: retornar bytes brutos mesmo se não reconhecidos
            // O serviço LLM tentará processar ou falhará graciosamente
            return bytes ?? Array.Empty<byte>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao extrair bytes da imagem");
            return Array.Empty<byte>();
        }
    }

    /// <summary>
    /// Verifica se os bytes correspondem a um formato de imagem conhecido
    /// </summary>
    private bool IsValidImageFormat(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 8) return false;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return true;

        // JPEG: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return true;

        // GIF: GIF87a ou GIF89a
        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
            return true;

        // BMP: BM
        if (bytes[0] == 0x42 && bytes[1] == 0x4D)
            return true;

        // WebP: RIFF....WEBP
        if (bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes.Length > 8 && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            return true;

        // TIFF: II (little endian) ou MM (big endian)
        if ((bytes[0] == 0x49 && bytes[1] == 0x49) || (bytes[0] == 0x4D && bytes[1] == 0x4D))
            return true;

        return false;
    }

    private string DetectMimeType(PdfStream stream, byte[] imageData)
    {
        var filter = stream.Get(PdfName.F);

        // Verificar magic bytes primeiro (mais confiável)
        if (imageData.Length >= 8)
        {
            if (imageData[0] == 0x89 && imageData[1] == 0x50) return "image/png";
            if (imageData[0] == 0xFF && imageData[1] == 0xD8) return "image/jpeg";
            if (imageData[0] == 0x47 && imageData[1] == 0x49) return "image/gif";
            if (imageData[0] == 0x42 && imageData[1] == 0x4D) return "image/bmp";
            if (imageData[0] == 0x52 && imageData[1] == 0x49) return "image/webp";
        }

        // Fallback para filtros PDF
        if (filter?.Equals(PdfName.DCTDecode) == true) return "image/jpeg";
        if (filter?.Equals(PdfName.FlateDecode) == true) return "image/png";
        if (filter?.Equals(PdfName.LZWDecode) == true) return "image/tiff";
        if (filter?.Equals(PdfName.JBIG2Decode) == true) return "image/jbig2";

        return "image/png"; // Default
    }

    private byte[] CalculateHash(byte[] data)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        return sha256.ComputeHash(data);
    }
}
