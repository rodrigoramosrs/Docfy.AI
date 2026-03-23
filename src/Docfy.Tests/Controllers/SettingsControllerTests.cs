using Xunit;
using Docfy.Controllers;
using Docfy.Core.Models;
using Docfy.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Docfy.Tests.Controllers;

public class SettingsControllerTests
{
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<ILogger<SettingsController>> _mockLogger;
    private readonly SettingsController _controller;

    public SettingsControllerTests()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _mockLogger = new Mock<ILogger<SettingsController>>();
        _controller = new SettingsController(_mockSettingsService.Object, _mockLogger.Object);
    }

    [Fact]
    public void Get_ShouldReturnSettings_WhenServiceReturnsSettings()
    {
        var settings = new AppSettings
        {
            ProcessMarkdownWithLlm = true,
            MaxParallelImageProcessing = 4,
            ExtractImages = true,
            PreserveOriginalFormatting = true,
            DefaultOutputFormat = "markdown",
            SupportedFormats = new List<string> { ".pdf", ".docx" },
            LlmVision = new LlmSettings
            {
                Provider = "OpenAI",
                ApiKey = "test-key",
                Model = "gpt-4",
                Endpoint = "https://api.openai.com",
                MaxTokens = 4096,
                MaxParallelImageProcessing = 5,
                RequestTimeoutSeconds = 300,
                ContextSize = 8192
            }
        };

        _mockSettingsService.Setup(x => x.GetSettings()).Returns(settings);

        var result = _controller.Get();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<SettingsDto>(okResult.Value);
        Assert.True(dto.ProcessMarkdownWithLlm);
        Assert.Equal(4, dto.MaxParallelImageProcessing);
        Assert.NotNull(dto.LlmVision);
        Assert.Equal("OpenAI", dto.LlmVision.Provider);
        Assert.Equal("gpt-4", dto.LlmVision.Model);
    }

    [Fact]
    public void Get_ShouldReturnLlmVisionSettings_WhenAvailable()
    {
        var settings = new AppSettings
        {
            LlmVision = new LlmSettings
            {
                Provider = "LocalAI",
                ApiKey = "local-key",
                Model = "llama-2",
                Endpoint = "http://localhost:8080",
                MaxTokens = 2048,
                MaxParallelImageProcessing = 3,
                RequestTimeoutSeconds = 600,
                ContextSize = 4096
            }
        };

        _mockSettingsService.Setup(x => x.GetSettings()).Returns(settings);

        var result = _controller.Get();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<SettingsDto>(okResult.Value);
        Assert.NotNull(dto.LlmVision);
        Assert.Equal("LocalAI", dto.LlmVision.Provider);
        Assert.Equal("llama-2", dto.LlmVision.Model);
        Assert.Equal("http://localhost:8080", dto.LlmVision.Endpoint);
        Assert.Equal("local-key", dto.LlmVision.ApiKey);
    }

    [Fact]
    public void Save_ShouldCallSaveSettings_WhenValidSettingsProvided()
    {
        var existingSettings = new AppSettings
        {
            ProcessMarkdownWithLlm = false,
            MaxParallelImageProcessing = 2,
            ExtractImages = true,
            PreserveOriginalFormatting = true,
            DefaultOutputFormat = "markdown",
            SupportedFormats = new List<string> { ".pdf" },
            LlmVision = new LlmSettings()
        };

        _mockSettingsService.Setup(x => x.GetSettings()).Returns(existingSettings);

        var dto = new SettingsDto
        {
            ProcessMarkdownWithLlm = true,
            MaxParallelImageProcessing = 4,
            ExtractImages = true,
            PreserveOriginalFormatting = true,
            DefaultOutputFormat = "markdown",
            SupportedFormats = new List<string> { ".pdf", ".docx" },
            LlmVision = new LlmSettingsDto
            {
                Provider = "OpenAI",
                ApiKey = "new-key",
                Model = "gpt-4",
                Endpoint = "https://api.openai.com"
            }
        };

        var result = _controller.Save(dto);

        var okResult = Assert.IsType<OkObjectResult>(result);
        _mockSettingsService.Verify(x => x.SaveSettings(It.IsAny<AppSettings>()), Times.Once);
    }

    [Fact]
    public void Save_ShouldReturn500_WhenServiceThrowsException()
    {
        _mockSettingsService.Setup(x => x.GetSettings()).Throws(new Exception("Test error"));

        var dto = new SettingsDto
        {
            ProcessMarkdownWithLlm = true
        };

        var result = _controller.Save(dto);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public void Save_ShouldMergeLlmVisionSettings_WhenProvided()
    {
        var existingSettings = new AppSettings
        {
            ProcessMarkdownWithLlm = false,
            MaxParallelImageProcessing = 2,
            ExtractImages = true,
            PreserveOriginalFormatting = true,
            DefaultOutputFormat = "markdown",
            SupportedFormats = new List<string> { ".pdf" },
            LlmVision = new LlmSettings
            {
                Provider = "OpenAI",
                ApiKey = "old-key",
                Model = "gpt-3.5"
            }
        };

        _mockSettingsService.Setup(x => x.GetSettings()).Returns(existingSettings);

        var dto = new SettingsDto
        {
            ProcessMarkdownWithLlm = true,
            MaxParallelImageProcessing = 4,
            ExtractImages = true,
            PreserveOriginalFormatting = true,
            DefaultOutputFormat = "markdown",
            SupportedFormats = new List<string> { ".pdf", ".docx" },
            LlmVision = new LlmSettingsDto
            {
                Provider = "LocalAI",
                ApiKey = "new-key",
                Model = "llama-2",
                Endpoint = "http://localhost:8080",
                MaxTokens = 4096,
                ContextSize = 8192
            }
        };

        var result = _controller.Save(dto);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("LocalAI", existingSettings.LlmVision.Provider);
        Assert.Equal("new-key", existingSettings.LlmVision.ApiKey);
        Assert.Equal("llama-2", existingSettings.LlmVision.Model);
    }

    [Fact]
    public async Task ProcessMarkdown_ShouldReturnProcessedMarkdown_WhenLlmEnabled()
    {
        var settings = new AppSettings { ProcessMarkdownWithLlm = true };
        _mockSettingsService.Setup(x => x.GetSettings()).Returns(settings);
        _mockSettingsService
            .Setup(x => x.ProcessMarkdownWithLlmAsync(It.IsAny<string>()))
            .ReturnsAsync("# Formatted\n\nContent");

        var result = await _controller.ProcessMarkdown("# Test\n\nContent");

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task ProcessMarkdown_ShouldReturnOriginal_WhenLlmDisabled()
    {
        var settings = new AppSettings { ProcessMarkdownWithLlm = false };
        _mockSettingsService.Setup(x => x.GetSettings()).Returns(settings);

        var result = await _controller.ProcessMarkdown("# Test\n\nContent");

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task ProcessMarkdown_ShouldReturn500_WhenServiceThrowsException()
    {
        var settings = new AppSettings { ProcessMarkdownWithLlm = true };
        _mockSettingsService.Setup(x => x.GetSettings()).Returns(settings);
        _mockSettingsService
            .Setup(x => x.ProcessMarkdownWithLlmAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("LLM Error"));

        var result = await _controller.ProcessMarkdown("# Test");

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public void Save_ShouldNotUpdateLlmVision_WhenNull()
    {
        var existingSettings = new AppSettings
        {
            ProcessMarkdownWithLlm = false,
            MaxParallelImageProcessing = 2,
            ExtractImages = true,
            PreserveOriginalFormatting = true,
            DefaultOutputFormat = "markdown",
            SupportedFormats = new List<string> { ".pdf" },
            LlmVision = new LlmSettings
            {
                Provider = "OpenAI",
                ApiKey = "existing-key"
            }
        };

        _mockSettingsService.Setup(x => x.GetSettings()).Returns(existingSettings);

        var dto = new SettingsDto
        {
            ProcessMarkdownWithLlm = true,
            MaxParallelImageProcessing = 4,
            ExtractImages = true,
            PreserveOriginalFormatting = true,
            DefaultOutputFormat = "markdown",
            SupportedFormats = new List<string> { ".pdf" },
            LlmVision = null
        };

        var result = _controller.Save(dto);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("OpenAI", existingSettings.LlmVision.Provider);
        Assert.Equal("existing-key", existingSettings.LlmVision.ApiKey);
    }

    [Fact]
    public void Get_ShouldReturnDefaultLlmVision_WhenNotConfigured()
    {
        var settings = new AppSettings
        {
            ProcessMarkdownWithLlm = false,
            LlmVision = new LlmSettings()
        };

        _mockSettingsService.Setup(x => x.GetSettings()).Returns(settings);

        var result = _controller.Get();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<SettingsDto>(okResult.Value);
        Assert.NotNull(dto.LlmVision);
        Assert.Equal("OpenAI", dto.LlmVision.Provider);
    }
}
