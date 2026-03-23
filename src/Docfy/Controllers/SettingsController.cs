using Microsoft.AspNetCore.Mvc;
using Docfy.Core.Models;
using Docfy.Core.Services;

namespace Docfy.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(ISettingsService settingsService, ILogger<SettingsController> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var settings = _settingsService.GetSettings();
        return Ok(new SettingsDto
        {
            ProcessMarkdownWithLlm = settings.ProcessMarkdownWithLlm,
            MaxParallelImageProcessing = settings.MaxParallelImageProcessing,
            ExtractImages = settings.ExtractImages,
            PreserveOriginalFormatting = settings.PreserveOriginalFormatting,
            DefaultOutputFormat = settings.DefaultOutputFormat,
            SupportedFormats = settings.SupportedFormats,
            LlmVision = new LlmSettingsDto
            {
                Provider = settings.LlmVision.Provider,
                ApiKey = settings.LlmVision.ApiKey,
                Model = settings.LlmVision.Model,
                Endpoint = settings.LlmVision.Endpoint,
                MaxTokens = settings.LlmVision.MaxTokens,
                MaxParallelImageProcessing = settings.LlmVision.MaxParallelImageProcessing,
                RequestTimeoutSeconds = settings.LlmVision.RequestTimeoutSeconds,
                ContextSize = settings.LlmVision.ContextSize
            }
        });
    }

    [HttpPost]
    public IActionResult Save([FromBody] SettingsDto settingsDto)
    {
        try
        {
            var settings = _settingsService.GetSettings();
            
            if (settingsDto.ProcessMarkdownWithLlm != settings.ProcessMarkdownWithLlm)
                settings.ProcessMarkdownWithLlm = settingsDto.ProcessMarkdownWithLlm;
            
            if (settingsDto.MaxParallelImageProcessing != settings.MaxParallelImageProcessing)
                settings.MaxParallelImageProcessing = settingsDto.MaxParallelImageProcessing;
            
            if (settingsDto.ExtractImages != settings.ExtractImages)
                settings.ExtractImages = settingsDto.ExtractImages;
            
            if (settingsDto.PreserveOriginalFormatting != settings.PreserveOriginalFormatting)
                settings.PreserveOriginalFormatting = settingsDto.PreserveOriginalFormatting;
            
            if (!string.IsNullOrEmpty(settingsDto.DefaultOutputFormat) && settingsDto.DefaultOutputFormat != settings.DefaultOutputFormat)
                settings.DefaultOutputFormat = settingsDto.DefaultOutputFormat;
            
            if (settingsDto.SupportedFormats != null && settingsDto.SupportedFormats.Count > 0 && 
                (settings.SupportedFormats == null || !settings.SupportedFormats.SequenceEqual(settingsDto.SupportedFormats)))
                settings.SupportedFormats = settingsDto.SupportedFormats;

            if (settingsDto.LlmVision != null)
            {
                if (!string.IsNullOrEmpty(settingsDto.LlmVision.Provider))
                    settings.LlmVision.Provider = settingsDto.LlmVision.Provider;
                
                if (!string.IsNullOrEmpty(settingsDto.LlmVision.ApiKey))
                    settings.LlmVision.ApiKey = settingsDto.LlmVision.ApiKey;
                
                if (!string.IsNullOrEmpty(settingsDto.LlmVision.Model))
                    settings.LlmVision.Model = settingsDto.LlmVision.Model;
                
                if (!string.IsNullOrEmpty(settingsDto.LlmVision.Endpoint))
                    settings.LlmVision.Endpoint = settingsDto.LlmVision.Endpoint;
                
                if (settingsDto.LlmVision.MaxTokens > 0)
                    settings.LlmVision.MaxTokens = settingsDto.LlmVision.MaxTokens;
                
                if (settingsDto.LlmVision.MaxParallelImageProcessing > 0)
                    settings.LlmVision.MaxParallelImageProcessing = settingsDto.LlmVision.MaxParallelImageProcessing;
                
                if (settingsDto.LlmVision.RequestTimeoutSeconds > 0)
                    settings.LlmVision.RequestTimeoutSeconds = settingsDto.LlmVision.RequestTimeoutSeconds;
                
                if (settingsDto.LlmVision.ContextSize > 0)
                    settings.LlmVision.ContextSize = settingsDto.LlmVision.ContextSize;
            }

            _settingsService.SaveSettings(settings);
            _logger.LogInformation("Configurações salvas");

            return Ok(new { Success = true, Message = "Configurações salvas com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar configurações");
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }

    [HttpPost("process-markdown")]
    public async Task<IActionResult> ProcessMarkdown([FromBody] string markdown)
    {
        try
        {
            var settings = _settingsService.GetSettings();
            
            if (!settings.ProcessMarkdownWithLlm)
            {
                return Ok(new { Success = true, Markdown = markdown, Processed = false });
            }

            var processed = await _settingsService.ProcessMarkdownWithLlmAsync(markdown);
            return Ok(new { Success = true, Markdown = processed, Processed = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar markdown");
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }
}