namespace Docfy.Core.Models;

public class MarkdownValidationResult
{
    public int TotalCharacters { get; set; }
    public int TotalLines { get; set; }
    public int HeadingsCount { get; set; }
    public int CodeBlocksCount { get; set; }
    public List<string> Issues { get; set; } = new();
}