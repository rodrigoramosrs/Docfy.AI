namespace Docfy.Models;

/// <summary>
/// Requisição de conversão enviada pelo cliente
/// </summary>
public class ConversionRequest
{
    public string FileName { get; set; } = string.Empty;
    public byte[] PdfContent { get; set; } = Array.Empty<byte>();
}
