using Docfy.Models;

namespace Docfy.Services;

public interface IPdfExtractionService
{
    Task<List<ExtractedPage>> ExtractContentAsync(byte[] pdfBytes);
}
