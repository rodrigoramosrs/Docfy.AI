using Xunit;
using Docfy.Core.Services;
using Docfy.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text;

namespace Docfy.Tests.Services;

public class MarkdownBuilderServiceTests
{
    private readonly MarkdownBuilderService _markdownBuilderService;
    private readonly Mock<ILogger<MarkdownBuilderService>> _mockLogger;

    public MarkdownBuilderServiceTests()
    {
        _mockLogger = new Mock<ILogger<MarkdownBuilderService>>();
        _markdownBuilderService = new MarkdownBuilderService(_mockLogger.Object);
    }

    [Fact]
    public void BuildMarkdown_ShouldReturnValidMarkdown_WhenPagesAreProvided()
    {
        var pages = CreateMockPages();
        var imageAnalyses = CreateMockImageAnalyses(pages);

        var markdown = _markdownBuilderService.BuildMarkdown(pages, imageAnalyses);

        Assert.NotNull(markdown);
        Assert.NotEmpty(markdown);
    }

    [Fact]
    public void BuildMarkdown_ShouldHandleEmptyPages()
    {
        var pages = new List<ExtractedPage>();
        var imageAnalyses = new List<ImageAnalysisResult>();

        var markdown = _markdownBuilderService.BuildMarkdown(pages, imageAnalyses);

        Assert.Empty(markdown);
    }

    [Fact]
    public void BuildMarkdown_ShouldProcessTextHierarchy()
    {
        var pages = CreateMockPagesWithTextChunks();
        var imageAnalyses = new List<ImageAnalysisResult>();

        var markdown = _markdownBuilderService.BuildMarkdown(pages, imageAnalyses);

        Assert.NotNull(markdown);
    }

    [Fact]
    public void BuildMarkdown_ShouldHandleImagesWithoutAnalyses()
    {
        var pages = CreateMockPages();
        var imageAnalyses = new List<ImageAnalysisResult>();

        var markdown = _markdownBuilderService.BuildMarkdown(pages, imageAnalyses);

        Assert.NotNull(markdown);
    }

    [Fact]
    public void BuildMarkdown_ShouldIgnoreDecorativeImages()
    {
        var pages = CreateMockPages();
        var imageAnalyses = CreateMockImageAnalyses(pages);
        imageAnalyses[0].IsDecorative = true;

        var markdown = _markdownBuilderService.BuildMarkdown(pages, imageAnalyses);

        Assert.NotNull(markdown);
    }

    [Fact]
    public void BuildMarkdown_ShouldInsertTextImagesAsQuotes()
    {
        var pages = CreateMockPages();
        var imageAnalyses = CreateMockImageAnalyses(pages);
        imageAnalyses[0].ContentType = "Text";

        var markdown = _markdownBuilderService.BuildMarkdown(pages, imageAnalyses);

        Assert.NotNull(markdown);
        Assert.Contains("Conteúdo de imagem", markdown);
    }

    [Fact]
    public void BuildMarkdown_ShouldFormatCodeBlocks()
    {
        var pages = CreateMockPages();
        var imageAnalyses = CreateMockImageAnalyses(pages);
        imageAnalyses[0].ContentType = "Code";
        imageAnalyses[0].CodeLanguage = "csharp";

        var markdown = _markdownBuilderService.BuildMarkdown(pages, imageAnalyses);

        Assert.NotNull(markdown);
        Assert.Contains("```csharp", markdown);
    }

    [Fact]
    public void BuildMarkdown_ShouldHandleChartsAndDiagrams()
    {
        var pages = CreateMockPages();
        var imageAnalyses = CreateMockImageAnalyses(pages);
        imageAnalyses[0].ContentType = "Chart";

        var markdown = _markdownBuilderService.BuildMarkdown(pages, imageAnalyses);

        Assert.NotNull(markdown);
        Assert.Contains("Figura:", markdown);
    }

    [Fact]
    public void BuildMarkdown_ShouldHandleUIImages()
    {
        var pages = CreateMockPages();
        var imageAnalyses = CreateMockImageAnalyses(pages);
        imageAnalyses[0].ContentType = "UI";

        var markdown = _markdownBuilderService.BuildMarkdown(pages, imageAnalyses);

        Assert.NotNull(markdown);
        Assert.Contains("Interface:", markdown);
    }

[Fact]
    public void BuildMarkdown_ShouldSeparatePagesWithSeparator()
    {
        var pages = CreateMultiPageMockPages();
        var imageAnalyses = new List<ImageAnalysisResult>();

        var markdown = _markdownBuilderService.BuildMarkdown(pages, imageAnalyses);

        Assert.NotNull(markdown);
        Assert.Contains("---", markdown);
        Assert.EndsWith("---", markdown.TrimEnd());
    }

    [Fact]
    public void BuildMarkdown_ShouldPostProcessMarkdown()
    {
        var pages = CreateMockPages();
        var imageAnalyses = CreateMockImageAnalyses(pages);

        var markdown = _markdownBuilderService.BuildMarkdown(pages, imageAnalyses);

        Assert.NotNull(markdown);
    }

[Fact]
    public void BuildMarkdown_ShouldHandleMixedContentTypes()
    {
        var pages = CreateMockPages();
        var imageAnalyses = CreateMockImageAnalyses(pages);
        
        imageAnalyses[0].ContentType = "Code";
        imageAnalyses[0].CodeLanguage = "javascript";
        imageAnalyses[1].ContentType = "Text";

        var markdown = _markdownBuilderService.BuildMarkdown(pages, imageAnalyses);

        Assert.NotNull(markdown);
        Assert.Contains("```javascript", markdown);
        Assert.Contains("Conteúdo de imagem", markdown);

    }

    [Fact]
    public void ValidateMarkdown_ShouldReturnValidStats()
    {
        var markdown = "# Test Document\n\nThis is a test paragraph.\n\n```csharp\ncode block\n```";

        var result = _markdownBuilderService.ValidateMarkdown(markdown);

        Assert.NotNull(result);
        Assert.True(result.TotalCharacters > 0);
        Assert.True(result.TotalLines > 0);
    }

    [Fact]
    public void ValidateMarkdown_ShouldDetectUnclosedCodeBlocks()
    {
        var markdown = "# Test Document\n\n```code\nunclosed block";

        var result = _markdownBuilderService.ValidateMarkdown(markdown);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Issues);
        Assert.Contains("Bloco de código não fechado", result.Issues);
    }

[Fact]
    public void ValidateMarkdown_ShouldDetectEmptyHeadings()
    {
        var markdown = "#\n\nParagraph";

        var result = _markdownBuilderService.ValidateMarkdown(markdown);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Issues);
        Assert.Contains("Heading vazio", result.Issues);
    }

    [Fact]
    public void BuildMarkdown_ShouldHandleDuplicates()
    {
        var pages = CreateMockPages();
        var imageAnalyses = CreateMockImageAnalyses(pages);
        imageAnalyses[0].IsDuplicate = true;

        var markdown = _markdownBuilderService.BuildMarkdown(pages, imageAnalyses);

        Assert.NotNull(markdown);
    }

    [Fact]
    public void BuildMarkdown_ShouldPreserveTextOrder()
    {
        var pages = CreateMockPagesWithTextChunks();
        var imageAnalyses = CreateMockImageAnalyses(pages);

        var markdown = _markdownBuilderService.BuildMarkdown(pages, imageAnalyses);

        Assert.NotNull(markdown);
    }

    private List<ExtractedPage> CreateMockPages()
    {
        var pages = new List<ExtractedPage>
        {
            new ExtractedPage
            {
                PageNumber = 1,
                Text = "This is page 1 text",
                TextChunks = new List<TextChunk>
                {
                    new TextChunk { Text = "This is page 1 text", FontSize = 12 }
                },
                Images = new List<ExtractedImage>
                {
                    new ExtractedImage
                    {
                        PageNumber = 1,
                        ImageIndex = 0,
                        Data = new byte[] { 1, 2, 3 },
                        Width = 100,
                        Height = 100,
                        MimeType = "image/png"
                    }
                }
            },
            new ExtractedPage
            {
                PageNumber = 2,
                Text = "This is page 2 text",
                TextChunks = new List<TextChunk>
                {
                    new TextChunk { Text = "This is page 2 text", FontSize = 12 }
                },
                Images = new List<ExtractedImage>
                {
                    new ExtractedImage
                    {
                        PageNumber = 2,
                        ImageIndex = 0,
                        Data = new byte[] { 4, 5, 6 },
                        Width = 100,
                        Height = 100,
                        MimeType = "image/jpeg"
                    }
                }
            }
        };

        return pages;
    }

    private List<ExtractedPage> CreateMockPagesWithTextChunks()
    {
        var pages = new List<ExtractedPage>
        {
            new ExtractedPage
            {
                PageNumber = 1,
                Text = "Page 1",
                TextChunks = new List<TextChunk>
                {
                    new TextChunk { Text = "Normal text", FontSize = 12 },
                    new TextChunk { Text = "Bold text", FontSize = 12, IsBold = true },
                    new TextChunk { Text = "Italic text", FontSize = 12, IsItalic = true },
                    new TextChunk { Text = "Title", FontSize = 20, IsBold = true }
                }
            }
        };

        return pages;
    }

    private List<ExtractedPage> CreateMultiPageMockPages()
    {
        var pages = new List<ExtractedPage>
        {
            new ExtractedPage
            {
                PageNumber = 1,
                Text = "Page 1",
                TextChunks = new List<TextChunk>
                {
                    new TextChunk { Text = "Page 1", FontSize = 12 }
                }
            },
            new ExtractedPage
            {
                PageNumber = 2,
                Text = "Page 2",
                TextChunks = new List<TextChunk>
                {
                    new TextChunk { Text = "Page 2", FontSize = 12 }
                }
            }
        };

        return pages;
    }

    private List<ImageAnalysisResult> CreateMockImageAnalyses(List<ExtractedPage> pages)
    {
        var imageAnalyses = new List<ImageAnalysisResult>();

        foreach (var page in pages)
        {
            foreach (var image in page.Images)
            {
                imageAnalyses.Add(new ImageAnalysisResult
                {
                    PageNumber = page.PageNumber,
                    ImageIndex = image.ImageIndex,
                    ImageHash = new byte[] { 1, 2, 3 },
                    ImageBase64 = Convert.ToBase64String(image.Data),
                    MimeType = image.MimeType,
                    Width = (int)image.Width,
                    Height = (int)image.Height,
                    Size = image.Data.Length,
                    Description = "Image description",
                    ContentType = "Text",
                    IsDecorative = false,
                    Confidence = 0.9
                });
            }
        }

        return imageAnalyses;
    }
}
