using Docfy.Core.Models;

namespace Docfy.Core.Services;

public interface IPdfExtractionService
{
    Task<List<ExtractedPage>> ExtractContentAsync(byte[] pdfBytes);
}
