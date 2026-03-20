namespace Docfy.DTOs
{
    public class ImageInfo
    {
        public int PageNumber { get; set; }
        public int ImageIndex { get; set; }
        public string Base64 { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public int Size { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsDecorative { get; set; }
        public bool IsDuplicate { get; set; }
        public string? CodeLanguage { get; set; }
        public double Confidence { get; set; }
        public double ProcessedScale { get; set; }
    }
}
