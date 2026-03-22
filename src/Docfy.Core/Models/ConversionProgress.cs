namespace Docfy.Core.Models;

/// <summary>
/// Atualização de progresso enviada via SignalR
/// </summary>
public class ConversionProgress
{
    public int Percentage { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
