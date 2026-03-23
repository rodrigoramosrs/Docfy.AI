namespace Docfy.Core.Models;

public class AppSettings
{
    public bool ProcessMarkdownWithLlm { get; set; } = false;
    public int MaxParallelImageProcessing { get; set; } = 2;
    public bool ExtractImages { get; set; } = true;
    public bool PreserveOriginalFormatting { get; set; } = true;
    public string DefaultOutputFormat { get; set; } = "markdown";
    public List<string> SupportedFormats { get; set; } = new() { ".pdf", ".docx" };
    
    public LlmSettings LlmVision { get; set; } = new();
}

public class LlmSettings
{
    public string Provider { get; set; } = "OpenAI";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-4-vision-preview";
    public string Endpoint { get; set; } = "https://api.openai.com/v1/chat/completions";
    public int MaxTokens { get; set; } = 4096;
    public int MaxParallelImageProcessing { get; set; } = 5;
    public int RequestTimeoutSeconds { get; set; } = 300;
    public int ContextSize { get; set; } = 8192;
}

public class SettingsDto
{
    public bool ProcessMarkdownWithLlm { get; set; }
    public int MaxParallelImageProcessing { get; set; }
    public bool ExtractImages { get; set; }
    public bool PreserveOriginalFormatting { get; set; }
    public string DefaultOutputFormat { get; set; }
    public List<string> SupportedFormats { get; set; }
    
    public LlmSettingsDto LlmVision { get; set; } = new();
}

public class LlmSettingsDto
{
    public string Provider { get; set; }
    public string ApiKey { get; set; }
    public string Model { get; set; }
    public string Endpoint { get; set; }
    public int MaxTokens { get; set; }
    public int MaxParallelImageProcessing { get; set; }
    public int RequestTimeoutSeconds { get; set; }
    public int ContextSize { get; set; }
}