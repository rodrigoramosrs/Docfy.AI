using Docfy.Core.Models;

namespace Docfy.Core.Services;

public interface IDocumentExtractionFactory
{
    IDocumentExtractionService? GetExtractor(string fileName);
    bool SupportsFormat(string fileName);
    IEnumerable<string> GetSupportedExtensions();
}