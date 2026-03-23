using Xunit;
using Docfy.Core.Services;
using Docfy.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace Docfy.Tests.Services;

public class DocumentExtractionFactoryTests
{
    private readonly Mock<ILogger<DocumentExtractionFactory>> _mockLogger;
    private readonly Mock<IDocumentExtractionService> _mockPdfExtractor;
    private readonly Mock<IDocumentExtractionService> _mockDocxExtractor;

    public DocumentExtractionFactoryTests()
    {
        _mockLogger = new Mock<ILogger<DocumentExtractionFactory>>();
        _mockPdfExtractor = new Mock<IDocumentExtractionService>();
        _mockDocxExtractor = new Mock<IDocumentExtractionService>();

        _mockPdfExtractor.Setup(x => x.SupportsFormat(".pdf")).Returns(true);
        _mockPdfExtractor.Setup(x => x.SupportsFormat(".docx")).Returns(false);

        _mockDocxExtractor.Setup(x => x.SupportsFormat(".docx")).Returns(true);
        _mockDocxExtractor.Setup(x => x.SupportsFormat(".pdf")).Returns(false);
    }

    private DocumentExtractionFactory CreateFactory()
    {
        var extractors = new List<IDocumentExtractionService>
        {
            _mockPdfExtractor.Object,
            _mockDocxExtractor.Object
        };
        return new DocumentExtractionFactory(extractors, _mockLogger.Object);
    }

    [Fact]
    public void GetExtractor_ShouldReturnPdfExtractor_ForPdfFiles()
    {
        var factory = CreateFactory();
        
        var extractor = factory.GetExtractor("document.pdf");
        
        Assert.NotNull(extractor);
        Assert.Equal(_mockPdfExtractor.Object, extractor);
    }

    [Fact]
    public void GetExtractor_ShouldReturnDocxExtractor_ForDocxFiles()
    {
        var factory = CreateFactory();
        
        var extractor = factory.GetExtractor("document.docx");
        
        Assert.NotNull(extractor);
        Assert.Equal(_mockDocxExtractor.Object, extractor);
    }

    [Fact]
    public void GetExtractor_ShouldReturnNull_ForUnsupportedFormat()
    {
        var factory = CreateFactory();
        
        var extractor = factory.GetExtractor("document.txt");
        
        Assert.Null(extractor);
    }

    [Fact]
    public void GetExtractor_ShouldHandleCaseInsensitiveExtensions()
    {
        var factory = CreateFactory();
        
        var extractor1 = factory.GetExtractor("document.PDF");
        var extractor2 = factory.GetExtractor("document.DOCX");
        
        Assert.NotNull(extractor1);
        Assert.NotNull(extractor2);
    }

    [Fact]
    public void SupportsFormat_ShouldReturnTrue_ForSupportedFormats()
    {
        var factory = CreateFactory();
        
        Assert.True(factory.SupportsFormat("document.pdf"));
        Assert.True(factory.SupportsFormat("document.docx"));
    }

    [Fact]
    public void SupportsFormat_ShouldReturnFalse_ForUnsupportedFormats()
    {
        var factory = CreateFactory();
        
        Assert.False(factory.SupportsFormat("document.txt"));
        Assert.False(factory.SupportsFormat("document.html"));
    }

    [Fact]
    public void GetSupportedExtensions_ShouldReturnAllExtensions()
    {
        var factory = CreateFactory();
        
        var extensions = factory.GetSupportedExtensions();
        
        Assert.NotNull(extensions);
        Assert.Contains(".pdf", extensions);
        Assert.Contains(".docx", extensions);
    }

    [Fact]
    public void GetExtractor_ShouldHandleFileNameWithMultipleDots()
    {
        var factory = CreateFactory();
        
        var extractor = factory.GetExtractor("my.document.pdf");
        
        Assert.NotNull(extractor);
    }

    [Fact]
    public void GetExtractor_ShouldHandleFileNameWithoutExtension()
    {
        var factory = CreateFactory();
        
        var extractor = factory.GetExtractor("document");
        
        Assert.Null(extractor);
    }

    [Fact]
    public void GetExtractor_ShouldHandleEmptyFileName()
    {
        var factory = CreateFactory();
        
        var extractor = factory.GetExtractor("");
        
        Assert.Null(extractor);
    }

    [Fact]
    public void GetExtractor_ShouldWorkWithSingleExtractor()
    {
        var extractors = new List<IDocumentExtractionService> { _mockPdfExtractor.Object };
        var factory = new DocumentExtractionFactory(extractors, _mockLogger.Object);
        
        var pdfExtractor = factory.GetExtractor("document.pdf");
        var docxExtractor = factory.GetExtractor("document.docx");
        
        Assert.NotNull(pdfExtractor);
        Assert.Null(docxExtractor);
    }

    [Fact]
    public void GetExtractor_ShouldWorkWithEmptyExtractors()
    {
        var extractors = new List<IDocumentExtractionService>();
        var factory = new DocumentExtractionFactory(extractors, _mockLogger.Object);
        
        var extractor = factory.GetExtractor("document.pdf");
        
        Assert.Null(extractor);
    }

    [Fact]
    public async Task ExtractContentAsync_ShouldBeCalledOnCorrectExtractor()
    {
        var expectedPages = new List<ExtractedPage>
        {
            new ExtractedPage { PageNumber = 1, Text = "Test content" }
        };

        _mockPdfExtractor
            .Setup(x => x.ExtractContentAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(expectedPages);

        var factory = CreateFactory();
        var extractor = factory.GetExtractor("document.pdf");
        
        var result = await extractor.ExtractContentAsync(new byte[] { 0x00 });
        
        Assert.NotNull(result);
        Assert.Single(result);
        _mockPdfExtractor.Verify(x => x.ExtractContentAsync(It.IsAny<byte[]>()), Times.Once);
    }
}
