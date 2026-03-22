namespace Docfy.Core.Models;

public class PageProgressEvent
{
    public int PageNumber { get; set; }
    public int TotalPages { get; set; }
    public string Status { get; set; } = "Processing"; // Started, Processing, Completed
    public int TotalImagesInPage { get; set; }
    public int ProcessedImagesInPage { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ImageProgressEvent
{
    public int PageNumber { get; set; }
    public int ImageIndex { get; set; }
    public int TotalImagesInPage { get; set; }
    public string Status { get; set; } = "Processing"; // Started, Processing, Completed, Error, Duplicate
    public string? ContentType { get; set; }
    public string Message { get; set; } = string.Empty;
    public double? Confidence { get; set; }
    public bool IsDuplicate { get; set; }
    public bool IsDecorative { get; set; }
}
