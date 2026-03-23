using Docfy.Core.Models;

namespace Docfy.Core.Services;

public interface IDocumentExtractionService
{
    Task<List<ExtractedPage>> ExtractContentAsync(byte[] documentBytes);
    bool SupportsFormat(string extension);
}