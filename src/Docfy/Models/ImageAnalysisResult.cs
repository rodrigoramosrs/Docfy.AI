namespace Docfy.Models;

public class ImageAnalysisResult
{
    public int PageNumber { get; set; }
    public int ImageIndex { get; set; }
    public byte[] ImageHash { get; set; } = Array.Empty<byte>();
    public string ImageBase64 { get; set; } = string.Empty;
    public string MimeType { get; set; } = "image/png";
    public int Width { get; set; }
    public int Height { get; set; }
    public int Size { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ContentType { get; set; } = "Unknown";
    public bool IsDecorative { get; set; }
    public bool IsDuplicate { get; set; }
    public string? CodeLanguage { get; set; }
    public double Confidence { get; set; }
    public double ProcessedScale { get; set; } = 1.0;
}
