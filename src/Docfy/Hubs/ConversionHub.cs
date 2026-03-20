using Microsoft.AspNetCore.SignalR;

namespace Docfy.Hubs;

public class ConversionHub : Hub
{
    private readonly ILogger<ConversionHub> _logger;

    public ConversionHub(ILogger<ConversionHub> logger)
    {
        _logger = logger;
    }

    public async Task CancelConversion()
    {
        await Clients.Caller.SendAsync("ConversionCancelled", new
        {
            Message = "Conversão cancelada pelo usuário"
        });
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Cliente conectado: {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("Connected", new
        {
            ConnectionId = Context.ConnectionId,
            Message = "Conectado ao servidor de conversão"
        });
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Cliente desconectado: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
