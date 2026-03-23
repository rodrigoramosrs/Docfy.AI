using Docfy.Core.Models;
using Microsoft.Extensions.Logging;

namespace Docfy.Core.Services;

public class PdfExtractionAdapter : IDocumentExtractionService
{
    private readonly IPdfExtractionService _pdfService;
    private readonly ILogger<PdfExtractionAdapter> _logger;

    public PdfExtractionAdapter(IPdfExtractionService pdfService, ILogger<PdfExtractionAdapter> logger)
    {
        _pdfService = pdfService;
        _logger = logger;
    }

    public bool SupportsFormat(string extension)
    {
        return extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<ExtractedPage>> ExtractContentAsync(byte[] documentBytes)
    {
        return await _pdfService.ExtractContentAsync(documentBytes);
    }
}