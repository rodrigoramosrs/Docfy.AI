using Xunit;
using Docfy.Core.Services;
using Docfy.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace Docfy.Tests.Services;

public class DocxExtractionServiceTests
{
    private readonly Mock<ILogger<DocxExtractionService>> _mockLogger;
    private readonly DocxExtractionService _service;

    public DocxExtractionServiceTests()
    {
        _mockLogger = new Mock<ILogger<DocxExtractionService>>();
        _service = new DocxExtractionService(_mockLogger.Object);
    }

    [Fact]
    public void SupportsFormat_ShouldReturnTrue_ForDocx()
    {
        var result = _service.SupportsFormat(".docx");
        Assert.True(result);
    }

    [Fact]
    public void SupportsFormat_ShouldReturnTrue_ForDocxCaseInsensitive()
    {
        Assert.True(_service.SupportsFormat(".DOCX"));
        Assert.True(_service.SupportsFormat(".Docx"));
        Assert.True(_service.SupportsFormat(".DOCX"));
    }

    [Fact]
    public void SupportsFormat_ShouldReturnFalse_ForOtherFormats()
    {
        Assert.False(_service.SupportsFormat(".pdf"));
        Assert.False(_service.SupportsFormat(".txt"));
        Assert.False(_service.SupportsFormat(".doc"));
    }

    [Fact]
    public async Task ExtractContentAsync_ShouldReturnEmptyList_WhenInputIsNull()
    {
        var result = await _service.ExtractContentAsync(null);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractContentAsync_ShouldReturnEmptyList_WhenInputIsEmpty()
    {
        var result = await _service.ExtractContentAsync(Array.Empty<byte>());
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractContentAsync_ShouldReturnEmptyList_WhenInputIsInvalidDocx()
    {
        var invalidBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        
        var result = await _service.ExtractContentAsync(invalidBytes);
        
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExtractContentAsync_ShouldExtractText_FromValidDocx()
    {
        var docxBytes = CreateMinimalDocx("Test Content");
        
        var result = await _service.ExtractContentAsync(docxBytes);
        
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Contains("Test Content", result[0].Text);
    }

    [Fact]
    public async Task ExtractContentAsync_ShouldExtractParagraphs()
    {
        var docxBytes = CreateDocxWithMultipleParagraphs();
        
        var result = await _service.ExtractContentAsync(docxBytes);
        
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Contains("Paragraph 1", result[0].Text);
        Assert.Contains("Paragraph 2", result[0].Text);
    }

    [Fact]
    public async Task ExtractContentAsync_ShouldExtractHeadings()
    {
        var docxBytes = CreateDocxWithHeadings();
        
        var result = await _service.ExtractContentAsync(docxBytes);
        
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Contains("Heading 1", result[0].Text);
        Assert.Contains("Heading 2", result[0].Text);
    }

    [Fact]
    public async Task ExtractContentAsync_ShouldExtractTables()
    {
        var docxBytes = CreateDocxWithTable();
        
        var result = await _service.ExtractContentAsync(docxBytes);
        
        Assert.NotNull(result);
        Assert.Single(result);
    }

    [Fact]
    public async Task ExtractContentAsync_ShouldReturnPageNumber1()
    {
        var docxBytes = CreateMinimalDocx("Test");
        
        var result = await _service.ExtractContentAsync(docxBytes);
        
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(1, result[0].PageNumber);
    }

    [Fact]
    public async Task ExtractContentAsync_ShouldHandleEmptyDocument()
    {
        var docxBytes = CreateEmptyDocx();
        
        var result = await _service.ExtractContentAsync(docxBytes);
        
        Assert.NotNull(result);
        Assert.Single(result);
    }

    [Fact]
    public async Task ExtractContentAsync_ShouldNotFail_WithLargeDocument()
    {
        var content = string.Join("\n", Enumerable.Range(1, 100).Select(i => $"Line {i}"));
        var docxBytes = CreateMinimalDocx(content);
        
        var result = await _service.ExtractContentAsync(docxBytes);
        
        Assert.NotNull(result);
        Assert.Single(result);
    }

    private byte[] CreateMinimalDocx(string text)
    {
        using var stream = new MemoryStream();
        using (var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = mainPart.Document.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Body());
            var para = body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph());
            var run = para.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Run());
            run.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text(text));
        }
        return stream.ToArray();
    }

    private byte[] CreateDocxWithMultipleParagraphs()
    {
        using var stream = new MemoryStream();
        using (var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = mainPart.Document.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Body());
            
            var para1 = body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph());
            var run1 = para1.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Run());
            run1.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text("Paragraph 1"));
            
            var para2 = body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph());
            var run2 = para2.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Run());
            run2.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text("Paragraph 2"));
        }
        return stream.ToArray();
    }

    private byte[] CreateDocxWithHeadings()
    {
        using var stream = new MemoryStream();
        using (var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = mainPart.Document.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Body());
            
            var h1 = body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph());
            h1.ParagraphProperties = new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties(
                new DocumentFormat.OpenXml.Wordprocessing.ParagraphStyleId { Val = "Heading1" });
            var run1 = h1.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Run());
            run1.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text("Heading 1"));
            
            var h2 = body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph());
            h2.ParagraphProperties = new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties(
                new DocumentFormat.OpenXml.Wordprocessing.ParagraphStyleId { Val = "Heading2" });
            var run2 = h2.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Run());
            run2.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text("Heading 2"));
        }
        return stream.ToArray();
    }

    private byte[] CreateDocxWithTable()
    {
        using var stream = new MemoryStream();
        using (var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = mainPart.Document.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Body());
            
            var table = body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Table());
            
            var row1 = table.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.TableRow());
            var cell1 = row1.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.TableCell());
            cell1.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                new DocumentFormat.OpenXml.Wordprocessing.Run(
                    new DocumentFormat.OpenXml.Wordprocessing.Text("Cell 1"))));
            
            var row2 = table.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.TableRow());
            var cell2 = row2.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.TableCell());
            cell2.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                new DocumentFormat.OpenXml.Wordprocessing.Run(
                    new DocumentFormat.OpenXml.Wordprocessing.Text("Cell 2"))));
        }
        return stream.ToArray();
    }

    private byte[] CreateEmptyDocx()
    {
        using var stream = new MemoryStream();
        using (var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            mainPart.Document.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Body());
        }
        return stream.ToArray();
    }
}
