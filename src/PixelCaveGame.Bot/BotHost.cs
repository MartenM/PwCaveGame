using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PixelCaveGame.Bot;

public class BotHost : IHostedService
{
    private ILogger<BotHost> _logger;
    private CaveGameBot _bot;
    
    public BotHost(ILogger<BotHost> logger, CaveGameBot bot)
    {
        _logger = logger;
        _bot = bot;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting application...");
        await _bot.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
       await _bot.StopAsync();
    }
}