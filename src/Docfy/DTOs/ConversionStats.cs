namespace Docfy.DTOs
{
    public class ConversionStats
    {
        public int TotalPages { get; set; }
        public int TotalImages { get; set; }
        public int ProcessedImages { get; set; }
        public object Validation { get; set; } = new();
    }
}
