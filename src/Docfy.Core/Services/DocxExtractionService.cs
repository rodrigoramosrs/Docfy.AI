using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Docfy.Core.Models;
using Microsoft.Extensions.Logging;

namespace Docfy.Core.Services;

public class DocxExtractionService : IDocumentExtractionService
{
    private readonly ILogger<DocxExtractionService> _logger;

    public DocxExtractionService(ILogger<DocxExtractionService> logger)
    {
        _logger = logger;
    }

    public bool SupportsFormat(string extension)
    {
        return extension.Equals(".docx", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<ExtractedPage>> ExtractContentAsync(byte[] documentBytes)
    {
        return await Task.Run(() =>
        {
            var pages = new List<ExtractedPage>();

            if (documentBytes == null || documentBytes.Length == 0)
            {
                _logger.LogWarning("DOCX bytes são null ou vazios");
                return pages;
            }

            try
            {
                using var stream = new MemoryStream(documentBytes);
                using var wordDocument = WordprocessingDocument.Open(stream, false);

                var mainPart = wordDocument.MainDocumentPart;
                if (mainPart == null)
                {
                    _logger.LogWarning("Documento DOCX sem MainDocumentPart");
                    return pages;
                }

                var document = mainPart.Document;
                if (document == null)
                {
                    _logger.LogWarning("Documento DOCX inválido");
                    return pages;
                }

                var page = new ExtractedPage
                {
                    PageNumber = 1
                };

                var textContent = new List<string>();
                var images = new List<ExtractedImage>();

                ExtractContentFromBody(document.Body, textContent, images, wordDocument, mainPart);

                page.Text = string.Join("\n\n", textContent);
                page.Images = images;

                pages.Add(page);

                _logger.LogInformation("DOCX processado com {ImageCount} imagens", images.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar DOCX");
            }

            return pages;
        });
    }

    private void ExtractContentFromBody(
        Body body,
        List<string> textContent,
        List<ExtractedImage> images,
        WordprocessingDocument wordDocument,
        MainDocumentPart mainPart)
    {
        foreach (var element in body.Elements())
        {
            ProcessElement(element, textContent, images, wordDocument, mainPart, 1);
        }
    }

    private void ProcessElement(
        OpenXmlElement element,
        List<string> textContent,
        List<ExtractedImage> images,
        WordprocessingDocument wordDocument,
        MainDocumentPart mainPart,
        int pageNumber)
    {
        switch (element)
        {
            case Paragraph paragraph:
                var paraText = GetParagraphText(paragraph);
                if (!string.IsNullOrWhiteSpace(paraText))
                {
                    var style = GetParagraphStyle(paragraph);
                    var prefix = style switch
                    {
                        "Heading1" => "## ",
                        "Heading2" => "### ",
                        "Heading3" => "#### ",
                        _ => ""
                    };
                    textContent.Add(prefix + paraText);
                }
                break;

            case Table table:
                var tableText = ExtractTableAsMarkdown(table);
                if (!string.IsNullOrWhiteSpace(tableText))
                {
                    textContent.Add(tableText);
                }
                break;

            case SectionProperties _:
                break;
        }

        foreach (var child in element.Elements())
        {
            ProcessElement(child, textContent, images, wordDocument, mainPart, pageNumber);
        }
    }

    private string GetParagraphText(Paragraph paragraph)
    {
        var texts = paragraph.Descendants<Text>();
        return string.Concat(texts.Select(t => t.Text));
    }

    private string GetParagraphStyle(Paragraph paragraph)
    {
        var paragraphProps = paragraph.ParagraphProperties;
        if (paragraphProps?.ParagraphStyleId?.Val != null)
        {
            return paragraphProps.ParagraphStyleId.Val.Value;
        }
        return "";
    }

    private string ExtractTableAsMarkdown(Table table)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (!rows.Any()) return "";

        var markdown = new System.Text.StringBuilder();
        var isFirstRow = true;

        foreach (var row in rows)
        {
            var cells = row.Elements<TableCell>().ToList();
            var cellTexts = cells.Select(c => GetCellText(c)).ToList();

            if (isFirstRow)
            {
                markdown.AppendLine("| " + string.Join(" | ", cellTexts) + " |");
                markdown.AppendLine("| " + string.Join(" | ", cellTexts.Select(_ => "---")) + " |");
                isFirstRow = false;
            }
            else
            {
                markdown.AppendLine("| " + string.Join(" | ", cellTexts) + " |");
            }
        }

        return markdown.ToString();
    }

    private string GetCellText(TableCell cell)
    {
        var texts = cell.Descendants<Text>().Select(t => t.Text);
        return string.Concat(texts).Replace("|", "\\|").Replace("\n", " ");
    }
}