namespace Docfy.Core.Models;

/// <summary>
/// Representa uma página extraída do PDF com texto e imagens
/// </summary>
public class ExtractedPage
{
    public int PageNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public List<ExtractedImage> Images { get; set; } = new();
    public List<TextChunk> TextChunks { get; set; } = new(); // Para posicionamento
}

/// <summary>
/// Representa uma imagem extraída do PDF
/// </summary>
public class ExtractedImage
{
    public int PageNumber { get; set; }
    public int ImageIndex { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public byte[] Hash { get; set; } = Array.Empty<byte>(); // Para deduplicação
    public float X { get; set; } = 0; // Posição X na página
    public float Y { get; set; } = 0; // Posição Y na página
    public float Width { get; set; }
    public float Height { get; set; }
    public string MimeType { get; set; } = "image/png";
}

/// <summary>
/// Chunk de texto com informações de posicionamento e formatação
/// </summary>
public class TextChunk
{
    public string Text { get; set; } = string.Empty;
    public float X { get; set; } = 0;
    public float Y { get; set; } = 0;
    public float FontSize { get; set; } = 12;
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public string? FontFamily { get; set; }
}
