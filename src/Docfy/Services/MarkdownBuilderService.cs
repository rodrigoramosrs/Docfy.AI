using System.Text;
using System.Text.RegularExpressions;
using Docfy.Models;

namespace Docfy.Services;

public class MarkdownBuilderService
{
    private readonly ILogger<MarkdownBuilderService> _logger;

    public MarkdownBuilderService(ILogger<MarkdownBuilderService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Constrói o documento Markdown final combinando texto e análises de imagens
    /// </summary>
    public string BuildMarkdown(List<ExtractedPage> pages, List<ImageAnalysisResult> imageAnalyses)
    {
        var markdown = new StringBuilder();
        var imageLookup = imageAnalyses.ToDictionary(
            a => (a.PageNumber, a.ImageIndex), 
            a => a);

        foreach (var page in pages.OrderBy(p => p.PageNumber))
        {
            _logger.LogDebug("Processando página {Page}", page.PageNumber);

            // Processar texto da página detectando hierarquia
            var processedText = ProcessTextHierarchy(page.TextChunks);
            
            // Inserir imagens no fluxo textual
            var pageImages = page.Images
                .OrderBy(img => img.Y) // Ordenar por posição vertical (topo para baixo)
                .ToList();

            var lines = processedText.Split('\n').ToList();
            var finalContent = new StringBuilder();

            int currentLine = 0;
            int totalLines = lines.Count;

            foreach (var image in pageImages)
            {
                if (!imageLookup.TryGetValue((image.PageNumber, image.ImageIndex), out var analysis))
                    continue;

                // Ignorar imagens decorativas ou duplicadas
                if (analysis.IsDecorative || (analysis.IsDuplicate && string.IsNullOrEmpty(analysis.Description)))
                {
                    _logger.LogDebug("Ignorando imagem decorativa/duplicada na página {Page}", page.PageNumber);
                    continue;
                }

                // Estimar posição aproximada da imagem no texto baseado em Y
                // (simplificação: assumir distribuição uniforme)
                int estimatedLine = (int)((image.Y / 1000f) * totalLines); // Ajustar conforme altura real
                estimatedLine = Math.Clamp(estimatedLine, 0, totalLines);

                // Adicionar linhas até a posição estimada
                while (currentLine < estimatedLine && currentLine < totalLines)
                {
                    finalContent.AppendLine(lines[currentLine]);
                    currentLine++;
                }

                // Inserir representação da imagem
                var imageRepresentation = FormatImageRepresentation(analysis);
                finalContent.AppendLine();
                finalContent.AppendLine(imageRepresentation);
                finalContent.AppendLine();
            }

            // Adicionar linhas restantes
            while (currentLine < totalLines)
            {
                finalContent.AppendLine(lines[currentLine]);
                currentLine++;
            }

            markdown.Append(finalContent);
            
            // Separador entre páginas (opcional)
            if (page.PageNumber < pages.Count)
            {
                markdown.AppendLine();
                markdown.AppendLine("---");
                markdown.AppendLine();
            }
        }

        // Pós-processamento
        var finalMarkdown = PostProcess(markdown.ToString());
        
        return finalMarkdown;
    }

    /// <summary>
    /// Processa chunks de texto para detectar hierarquia de títulos
    /// </summary>
    private string ProcessTextHierarchy(List<TextChunk> chunks)
    {
        if (!chunks.Any()) return string.Empty;

        var result = new StringBuilder();
        string? previousLine = null;

        foreach (var chunk in chunks)
        {
            var text = chunk.Text.Trim();
            if (string.IsNullOrEmpty(text)) continue;

            // Heurísticas para detectar títulos
            bool isTitle = DetectTitle(chunk, previousLine);

            if (isTitle)
            {
                // Determinar nível do título baseado no tamanho da fonte
                int level = chunk.FontSize switch
                {
                    > 18 => 1,
                    > 14 => 2,
                    > 12 => 3,
                    _ => 4
                };

                // Adicionar prefixo de título Markdown
                var prefix = new string('#', level);
                result.AppendLine($"{prefix} {text}");
            }
            else
            {
                // Detectar listas
                if (IsListItem(text))
                {
                    result.AppendLine(text);
                }
                else
                {
                    result.AppendLine(text);
                }
            }

            previousLine = text;
        }

        return result.ToString();
    }

    private bool DetectTitle(TextChunk chunk, string? previousLine)
    {
        // Heurísticas:
        // 1. Fonte maior que o normal
        // 2. Texto em negrito
        // 3. Padrões de título (Capítulo, Seção, etc.)
        // 4. Curto e isolado
        
        var titlePatterns = new[] { 
            @"^(Capítulo|Seção|Cap\.|Sec\.|Chapter|Section|Resumo|Abstract|Introdução|Conclusão|Referências)\s*\d*", 
            @"^\d+\.", 
            @"^[A-Z][A-Z\s]+$" 
        };
        
        if (chunk.FontSize > 14) return true;
        if (chunk.IsBold && chunk.Text.Length < 100) return true;
        
        foreach (var pattern in titlePatterns)
        {
            if (Regex.IsMatch(chunk.Text, pattern, RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }

    private bool IsListItem(string text)
    {
        // Detectar itens de lista numerados ou com bullets
        return Regex.IsMatch(text, @"^(\d+[\.]\s+|[-\*•]\s+|\([\da-z]\)\s+)", RegexOptions.IgnoreCase);
    }

    private string FormatImageRepresentation(ImageAnalysisResult analysis)
    {
        var sb = new StringBuilder();

        switch (analysis.ContentType)
        {
            case "Text":
                // Texto extraído da imagem - inserir como citação ou bloco
                sb.AppendLine("> **Conteúdo de imagem:**");
                sb.AppendLine();
                sb.AppendLine(analysis.Description);
                break;

            case "Chart":
            case "Diagram":
                // Descrição de gráfico/diagrama
                sb.AppendLine("> **Figura:** " + analysis.Description);
                break;

            case "Code":
                // Código em bloco com syntax highlighting
                var lang = analysis.CodeLanguage?.ToLower() ?? "text";
                sb.AppendLine($"```{lang}");
                sb.AppendLine(analysis.Description);
                sb.AppendLine("```");
                break;

            case "UI":
                // Descrição de interface
                sb.AppendLine("> **Interface:** " + analysis.Description);
                break;

            default:
                // Fallback genérico
                if (!string.IsNullOrWhiteSpace(analysis.Description))
                {
                    sb.AppendLine("> " + analysis.Description);
                }
                break;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Pós-processamento para limpar e normalizar o Markdown
    /// </summary>
    private string PostProcess(string markdown)
    {
        // Remover linhas em branco excessivas
        markdown = Regex.Replace(markdown, @"\n{3,}", "\n\n");
        
        // Normalizar headings (garantir espaço após #)
        markdown = Regex.Replace(markdown, @"^(#{1,6})([^\s])", "$1 $2", RegexOptions.Multiline);
        
        // Corrigir listas mal formatadas
        markdown = Regex.Replace(markdown, @"^\s*[-\*]\s*", "- ", RegexOptions.Multiline);
        
        // Garantir que blocos de código estejam bem formados
        markdown = FixCodeBlocks(markdown);

        return markdown.Trim();
    }

    private string FixCodeBlocks(string markdown)
    {
        // Garantir que blocos de código abertos sejam fechados
        var lines = markdown.Split('\n');
        var result = new StringBuilder();
        bool inCodeBlock = false;
        string? currentLanguage = null;

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    // Fechando bloco
                    inCodeBlock = false;
                    currentLanguage = null;
                }
                else
                {
                    // Abrindo bloco
                    inCodeBlock = true;
                    var match = Regex.Match(line.Trim(), @"^```(\w+)?");
                    currentLanguage = match.Groups[1].Value;
                }
                result.AppendLine(line);
            }
            else
            {
                result.AppendLine(line);
            }
        }

        // Fechar bloco se necessário
        if (inCodeBlock)
        {
            result.AppendLine("```");
        }

        return result.ToString();
    }

    /// <summary>
    /// Validação básica do Markdown gerado
    /// </summary>
    public object ValidateMarkdown(string markdown)
    {
        var issues = new List<string>();
        
        // Verificar blocos de código não fechados
        var codeBlockMatches = Regex.Matches(markdown, "```");
        if (codeBlockMatches.Count % 2 != 0)
        {
            issues.Add("Bloco de código não fechado detectado");
        }

        // Verificar headings vazios
        if (Regex.IsMatch(markdown, @"^#{1,6}\s*$", RegexOptions.Multiline))
        {
            issues.Add("Heading vazio detectado");
        }

        // Estatísticas
        var stats = new
        {
            TotalCharacters = markdown.Length,
            TotalLines = markdown.Split('\n').Length,
            HeadingsCount = Regex.Matches(markdown, @"^#{1,6}\s", RegexOptions.Multiline).Count,
            CodeBlocksCount = Regex.Matches(markdown, "```").Count / 2,
            Issues = issues
        };

        return stats;
    }
}
