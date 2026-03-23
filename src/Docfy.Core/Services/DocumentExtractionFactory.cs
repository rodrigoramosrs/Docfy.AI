using Docfy.Core.Models;
using Microsoft.Extensions.Logging;

namespace Docfy.Core.Services;

public class DocumentExtractionFactory : IDocumentExtractionFactory
{
    private readonly IEnumerable<IDocumentExtractionService> _extractors;
    private readonly ILogger<DocumentExtractionFactory> _logger;

    public DocumentExtractionFactory(
        IEnumerable<IDocumentExtractionService> extractors,
        ILogger<DocumentExtractionFactory> logger)
    {
        _extractors = extractors;
        _logger = logger;
    }

    public IDocumentExtractionService? GetExtractor(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        foreach (var extractor in _extractors)
        {
            if (extractor.SupportsFormat(extension))
            {
                _logger.LogInformation("Usando extrator {Extractor} para formato {Extension}",
                    extractor.GetType().Name, extension);
                return extractor;
            }
        }

        _logger.LogWarning("Nenhum extrator encontrado para formato {Extension}", extension);
        return null;
    }

    public bool SupportsFormat(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return _extractors.Any(e => e.SupportsFormat(extension));
    }

    public IEnumerable<string> GetSupportedExtensions()
    {
        return _extractors.SelectMany(e => new[] { ".pdf", ".docx" }).Distinct();
    }
}