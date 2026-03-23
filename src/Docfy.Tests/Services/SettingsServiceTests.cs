using Xunit;
using Docfy.Core.Services;
using Docfy.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Docfy.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly Mock<ILogger<SettingsService>> _mockLogger;
    private readonly Mock<ILlmVisionService> _mockLlmService;
    private readonly string _testSettingsPath;
    private readonly string _testDir;

    public SettingsServiceTests()
    {
        _mockLogger = new Mock<ILogger<SettingsService>>();
        _mockLlmService = new Mock<ILlmVisionService>();
        _testDir = Path.Combine(Path.GetTempPath(), "docfy_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _testSettingsPath = Path.Combine(_testDir, "appsettings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    private SettingsService CreateService()
    {
        var service = new SettingsService(_mockLogger.Object, _mockLlmService.Object);
        return service;
    }

    private void WriteTestSettings(object settings)
    {
        var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
        File.WriteAllText(_testSettingsPath, json);
    }

    [Fact]
    public void GetSettings_ShouldReturnDefaultSettings_WhenFileDoesNotExist()
    {
        var service = CreateService();
        var settings = service.GetSettings();

        Assert.NotNull(settings);
        Assert.False(settings.ProcessMarkdownWithLlm);
        Assert.True(settings.ExtractImages);
        Assert.True(settings.PreserveOriginalFormatting);
        Assert.Equal(2, settings.MaxParallelImageProcessing);
    }

    [Fact]
    public void GetSettings_ShouldReturnSettings_WhenFileExists()
    {
        var testSettings = new
        {
            ProcessMarkdownWithLlm = true,
            MaxParallelImageProcessing = 4,
            ExtractImages = false,
            PreserveOriginalFormatting = true,
            DefaultOutputFormat = "markdown",
            SupportedFormats = new[] { ".pdf", ".docx" },
            LlmVision = new
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

        WriteTestSettings(testSettings);
        var service = CreateService();
        var settings = service.GetSettings();

        Assert.NotNull(settings);
        Assert.True(settings.ProcessMarkdownWithLlm);
        Assert.Equal(4, settings.MaxParallelImageProcessing);
        Assert.False(settings.ExtractImages);
    }

    [Fact]
    public void SaveSettings_ShouldMergeSettings_WithoutOverwriting()
    {
        var existingSettings = new
        {
            Logging = new { LogLevel = new { Default = "Information" } },
            AllowedHosts = "*",
            LlmVision = new
            {
                Provider = "OpenAI",
                ApiKey = "existing-key",
                Model = "gpt-4",
                Endpoint = "https://api.openai.com",
                MaxTokens = 4096
            },
            ProcessMarkdownWithLlm = false,
            MaxParallelImageProcessing = 2,
            ExtractImages = true,
            PreserveOriginalFormatting = true,
            DefaultOutputFormat = "markdown",
            SupportedFormats = new[] { ".pdf", ".docx" }
        };

        WriteTestSettings(existingSettings);
        var service = CreateService();

        var newSettings = new AppSettings
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
                ApiKey = "existing-key",
                Model = "gpt-4",
                Endpoint = "https://api.openai.com",
                MaxTokens = 4096
            }
        };

        service.SaveSettings(newSettings);

        var savedJson = File.ReadAllText(_testSettingsPath);
        var savedObj = JObject.Parse(savedJson);

        Assert.True(savedObj.ContainsKey("Logging"));
        Assert.True(savedObj.ContainsKey("AllowedHosts"));
        Assert.True(savedObj.ContainsKey("LlmVision"));
        Assert.Equal(true, savedObj["ProcessMarkdownWithLlm"]?.Value<bool>());
        Assert.Equal(4, savedObj["MaxParallelImageProcessing"]?.Value<int>());
    }

    [Fact]
    public void SaveSettings_ShouldUpdateLlmVisionSettings()
    {
        var existingSettings = new
        {
            LlmVision = new
            {
                Provider = "OpenAI",
                ApiKey = "old-key",
                Model = "gpt-3.5",
                Endpoint = "https://old.api.com",
                MaxTokens = 2048
            },
            ProcessMarkdownWithLlm = false
        };

        WriteTestSettings(existingSettings);
        var service = CreateService();

        var newSettings = new AppSettings
        {
            ProcessMarkdownWithLlm = true,
            LlmVision = new LlmSettings
            {
                Provider = "LocalAI",
                ApiKey = "new-key",
                Model = "llama-2",
                Endpoint = "http://localhost:8080",
                MaxTokens = 4096,
                ContextSize = 16384,
                RequestTimeoutSeconds = 600,
                MaxParallelImageProcessing = 3
            }
        };

        service.SaveSettings(newSettings);

        var savedJson = File.ReadAllText(_testSettingsPath);
        var savedObj = JObject.Parse(savedJson);

        Assert.Equal("LocalAI", savedObj["LlmVision"]?["Provider"]?.Value<string>());
        Assert.Equal("new-key", savedObj["LlmVision"]?["ApiKey"]?.Value<string>());
        Assert.Equal("llama-2", savedObj["LlmVision"]?["Model"]?.Value<string>());
        Assert.Equal("http://localhost:8080", savedObj["LlmVision"]?["Endpoint"]?.Value<string>());
        Assert.Equal(4096, savedObj["LlmVision"]?["MaxTokens"]?.Value<int>());
        Assert.Equal(16384, savedObj["LlmVision"]?["ContextSize"]?.Value<int>());
    }

    [Fact]
    public async Task ProcessMarkdownWithLlmAsync_ShouldReturnOriginal_WhenInputIsEmpty()
    {
        var service = CreateService();
        
        var result = await service.ProcessMarkdownWithLlmAsync("");
        
        Assert.Equal("", result);
    }

    [Fact]
    public async Task ProcessMarkdownWithLlmAsync_ShouldReturnOriginal_WhenInputIsNull()
    {
        var service = CreateService();
        
        var result = await service.ProcessMarkdownWithLlmAsync(null);
        
        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessMarkdownWithLlmAsync_ShouldCallLlmService_WhenInputIsValid()
    {
        _mockLlmService
            .Setup(x => x.AnalyzeTextAsync(It.IsAny<string>()))
            .ReturnsAsync("# Formatted Markdown\n\nContent here");

        var service = CreateService();
        var input = "# Test\n\nSome content";
        
        var result = await service.ProcessMarkdownWithLlmAsync(input);
        
        Assert.NotNull(result);
        _mockLlmService.Verify(x => x.AnalyzeTextAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMarkdownWithLlmAsync_ShouldReturnOriginal_WhenLlmFails()
    {
        _mockLlmService
            .Setup(x => x.AnalyzeTextAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("LLM Error"));

        var service = CreateService();
        var input = "# Test\n\nSome content";
        
        var result = await service.ProcessMarkdownWithLlmAsync(input);
        
        Assert.Equal(input, result);
    }

    [Fact]
    public void SaveSettings_ShouldPreserveOtherJsonSections()
    {
        var existingSettings = new
        {
            Logging = new { LogLevel = new { Default = "Warning", Microsoft = "Error" } },
            AllowedHosts = "*",
            CustomSection = new { CustomKey = "CustomValue" },
            LlmVision = new { Provider = "OpenAI", ApiKey = "key" },
            ProcessMarkdownWithLlm = false,
            MaxParallelImageProcessing = 2,
            ExtractImages = true,
            PreserveOriginalFormatting = true,
            DefaultOutputFormat = "markdown",
            SupportedFormats = new[] { ".pdf" }
        };

        WriteTestSettings(existingSettings);
        var service = CreateService();

        var newSettings = new AppSettings
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
                ApiKey = "key",
                Model = "gpt-4"
            }
        };

        service.SaveSettings(newSettings);

        var savedJson = File.ReadAllText(_testSettingsPath);
        var savedObj = JObject.Parse(savedJson);

        Assert.True(savedObj.ContainsKey("Logging"));
        Assert.True(savedObj.ContainsKey("AllowedHosts"));
        Assert.True(savedObj.ContainsKey("CustomSection"));
        Assert.Equal("CustomValue", savedObj["CustomSection"]?["CustomKey"]?.Value<string>());
    }
}
