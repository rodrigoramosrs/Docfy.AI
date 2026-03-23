using Docfy.Core.Models;
using Microsoft.Extensions.Logging;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Docfy.Core.Services;

public interface ISettingsService
{
    AppSettings GetSettings();
    void SaveSettings(AppSettings settings);
    Task<string> ProcessMarkdownWithLlmAsync(string markdown);
}

public class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly ILlmVisionService _llmService;
    private readonly string _settingsPath;
    private AppSettings _settings;

    public SettingsService(ILogger<SettingsService> logger, ILlmVisionService llmService)
    {
        _logger = logger;
        _llmService = llmService;
        _settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        _settings = LoadSettings();
    }

    private AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar configurações");
        }
        return new AppSettings();
    }

    public AppSettings GetSettings()
    {
        return _settings;
    }

    public void SaveSettings(AppSettings newSettings)
    {
        try
        {
            var existing = LoadSettings();

            if (newSettings.ProcessMarkdownWithLlm != existing.ProcessMarkdownWithLlm)
                existing.ProcessMarkdownWithLlm = newSettings.ProcessMarkdownWithLlm;
            
            if (newSettings.MaxParallelImageProcessing != existing.MaxParallelImageProcessing)
                existing.MaxParallelImageProcessing = newSettings.MaxParallelImageProcessing;
            
            if (newSettings.ExtractImages != existing.ExtractImages)
                existing.ExtractImages = newSettings.ExtractImages;
            
            if (newSettings.PreserveOriginalFormatting != existing.PreserveOriginalFormatting)
                existing.PreserveOriginalFormatting = newSettings.PreserveOriginalFormatting;
            
            if (!string.IsNullOrEmpty(newSettings.DefaultOutputFormat) && newSettings.DefaultOutputFormat != existing.DefaultOutputFormat)
                existing.DefaultOutputFormat = newSettings.DefaultOutputFormat;
            
            if (newSettings.SupportedFormats != null && newSettings.SupportedFormats.Count > 0 && 
                (existing.SupportedFormats == null || !existing.SupportedFormats.SequenceEqual(newSettings.SupportedFormats)))
                existing.SupportedFormats = newSettings.SupportedFormats;

            if (newSettings.LlmVision != null)
            {
                if (!string.IsNullOrEmpty(newSettings.LlmVision.Provider) && newSettings.LlmVision.Provider != existing.LlmVision.Provider)
                    existing.LlmVision.Provider = newSettings.LlmVision.Provider;
                
                if (!string.IsNullOrEmpty(newSettings.LlmVision.ApiKey) && newSettings.LlmVision.ApiKey != existing.LlmVision.ApiKey)
                    existing.LlmVision.ApiKey = newSettings.LlmVision.ApiKey;
                
                if (!string.IsNullOrEmpty(newSettings.LlmVision.Model) && newSettings.LlmVision.Model != existing.LlmVision.Model)
                    existing.LlmVision.Model = newSettings.LlmVision.Model;
                
                if (!string.IsNullOrEmpty(newSettings.LlmVision.Endpoint) && newSettings.LlmVision.Endpoint != existing.LlmVision.Endpoint)
                    existing.LlmVision.Endpoint = newSettings.LlmVision.Endpoint;
                
                if (newSettings.LlmVision.MaxTokens > 0 && newSettings.LlmVision.MaxTokens != existing.LlmVision.MaxTokens)
                    existing.LlmVision.MaxTokens = newSettings.LlmVision.MaxTokens;
                
                if (newSettings.LlmVision.MaxParallelImageProcessing > 0 && newSettings.LlmVision.MaxParallelImageProcessing != existing.LlmVision.MaxParallelImageProcessing)
                    existing.LlmVision.MaxParallelImageProcessing = newSettings.LlmVision.MaxParallelImageProcessing;
                
                if (newSettings.LlmVision.RequestTimeoutSeconds > 0 && newSettings.LlmVision.RequestTimeoutSeconds != existing.LlmVision.RequestTimeoutSeconds)
                    existing.LlmVision.RequestTimeoutSeconds = newSettings.LlmVision.RequestTimeoutSeconds;
                
                if (newSettings.LlmVision.ContextSize > 0 && newSettings.LlmVision.ContextSize != existing.LlmVision.ContextSize)
                    existing.LlmVision.ContextSize = newSettings.LlmVision.ContextSize;
            }

            _settings = existing;

            var fullJson = File.ReadAllText(_settingsPath);
            var jObject = JObject.Parse(fullJson);
            
            jObject["ProcessMarkdownWithLlm"] = existing.ProcessMarkdownWithLlm;
            jObject["MaxParallelImageProcessing"] = existing.MaxParallelImageProcessing;
            jObject["ExtractImages"] = existing.ExtractImages;
            jObject["PreserveOriginalFormatting"] = existing.PreserveOriginalFormatting;
            jObject["DefaultOutputFormat"] = existing.DefaultOutputFormat;
            jObject["SupportedFormats"] = new JArray(existing.SupportedFormats);
            
            var llmVisionObj = jObject["LlmVision"] as JObject ?? new JObject();
            llmVisionObj["Provider"] = existing.LlmVision.Provider;
            llmVisionObj["ApiKey"] = existing.LlmVision.ApiKey;
            llmVisionObj["Model"] = existing.LlmVision.Model;
            llmVisionObj["Endpoint"] = existing.LlmVision.Endpoint;
            llmVisionObj["MaxTokens"] = existing.LlmVision.MaxTokens;
            llmVisionObj["MaxParallelImageProcessing"] = existing.LlmVision.MaxParallelImageProcessing;
            llmVisionObj["RequestTimeoutSeconds"] = existing.LlmVision.RequestTimeoutSeconds.ToString();
            llmVisionObj["ContextSize"] = existing.LlmVision.ContextSize;
            jObject["LlmVision"] = llmVisionObj;

            File.WriteAllText(_settingsPath, jObject.ToString(Formatting.Indented));
            _logger.LogInformation("Configurações salvas com merge preservando outras seções");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar configurações");
            throw;
        }
    }

    public async Task<string> ProcessMarkdownWithLlmAsync(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return markdown;

        try
        {
            _logger.LogInformation("Processando markdown com LLM para melhorar formatação");

            var prompt = $@"Você é um formatador de documentos markdown. Sua ÚNICA tarefa é formatar corretamente o conteúdo markdown fornecido.

REGRAS IMPORTANTES:
1. NÃO adicione NENHUM conteúdo novo - apenas formate o que já existe
2. NÃO adicione explicações ou comentários
3. NÃO remova conteúdo existente
4. Apenas corrija e melhore a formatação markdown
5. Retorne APENAS o markdown formatado, nada mais

Correções a aplicar:
- Corrija a sintaxe markdown (cabeçalhos, listas, links, tabelas)
- Adicione linhas em branco onde necessário para separar seções
- Alinhe corretamente listas e sublistas
- Certifique-se de que tabelas estão com alinhamento correto
- Corrija blocos de código com linguagem especificada
- Normalize cabeçalhos (espaço após #)
- Corrija links e imagens

Conteúdo para formatar:

{markdown}

Lembre-se: Retorne APENAS o markdown formatado, sem nenhum texto adicional ou explicação.";

            var result = await _llmService.AnalyzeTextAsync(prompt);
            return result?.Trim() ?? markdown;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar markdown com LLM");
            return markdown;
        }
    }
}