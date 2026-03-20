using Docfy.Controllers;

namespace Docfy.DTOs
{
    public class ConversionResponse
    {
        public bool Success { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Markdown { get; set; } = string.Empty;
        public ConversionStats Stats { get; set; } = new();
        public List<ImageInfo> Images { get; set; } = new();
    }
}
