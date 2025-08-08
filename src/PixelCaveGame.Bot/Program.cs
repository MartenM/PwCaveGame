// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PixelCaveGame.Bot;
using PixelCaveGame.Bot.Configuration;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    { 
        // Load appsettings.json and environment-specific appsettings
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        // Override with environment variables
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.Configure<AccountConfiguration>(context.Configuration.GetSection("AccountSettings"));
        services.Configure<BotConfiguration>(context.Configuration.GetSection("BotSettings"));

        services.AddSingleton<CaveGameBot>();
        services.AddHostedService<BotHost>();

    })
    .Build();
    
await host.RunAsync();