using System.Drawing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PixelCaveGame.Bot.Configuration;
using PixelCaveGame.Bot.Generation;
using PixelCaveGame.Bot.Util;
using PixelPilot.Client;
using PixelPilot.Client.Players.Basic;
using PixelPilot.Client.World;
using PixelPilot.Client.World.Blocks;
using PixelPilot.Client.World.Blocks.Placed;
using PixelPilot.Client.World.Constants;
using PixelWalker.Networking.Protobuf.WorldPackets;
using Random = System.Random;

namespace PixelCaveGame.Bot;

public class CaveGameBot
{
    private readonly ILogger<CaveGameBot> _logger;
    private readonly AccountConfiguration _accountConfiguration;
    private readonly BotConfiguration _botConfiguration;

    private PixelPilotClient _client;
    private readonly PlayerManager _playerManager = new PlayerManager();
    private readonly PixelWorld _world = new();

    private WorldGeneration _worldGeneration;
    private VisionManager _visionManager;

    private List<Point> _treasureLocations = new List<Point>();
    
    // Virtual layers of blocks, may or may not be visible to players.
    
    
    private bool AllowOthers { get; set; }
    
    public CaveGameBot(ILogger<CaveGameBot> logger, IOptions<AccountConfiguration> accountDetails, IOptions<BotConfiguration> botConfiguration)
    {
        _logger = logger;
        _botConfiguration = botConfiguration.Value;
        _accountConfiguration = accountDetails.Value;
    }

    public async Task StartAsync()
    {
        _logger.LogInformation("Starting CaveGameBot");

        var builder = new PixelGameClientBuilder();
        if (_accountConfiguration.Token != null)
        {
            _logger.LogInformation("Using token to connect to PixelWalker.");
            builder.SetToken(_accountConfiguration.Token);
        }
        else
        {
            builder.SetEmail(_accountConfiguration.Email)
                .SetPassword(_accountConfiguration.Password);
        }

        builder.SetPrefix("[CaveGameBot] ")
            .SetAutomaticReconnect(false);
        _client = builder.Build();
        
        _worldGeneration = new WorldGeneration(_world, _client);
        _visionManager = new VisionManager(_client);
        
        _client.OnPacketReceived += _playerManager.HandlePacket;
        _client.OnPacketReceived += _world.HandlePacket;
        
        // Commands
        _client.OnPacketReceived += (sender, packet) =>
        {
            var chatPacket = packet as PlayerChatPacket;
            if (chatPacket == null) return;
            
            var player = _playerManager.GetPlayer(chatPacket.PlayerId);
            
            if (player == null) return;
            if (!string.Equals(player.Username, "MARTEN", StringComparison.InvariantCultureIgnoreCase)) return;


            if (chatPacket.Message.StartsWith(".generate"))
            {
                var splits = chatPacket.Message.Substring(1).Split(" ");
                
                int seed = 1000;
                if (splits.Length > 1)
                {
                    int.TryParse(splits[1], out seed);
                }
                
                _worldGeneration.Seed = seed;
                
                Task.Run(async () =>
                {
                    try
                    {
                        await _worldGeneration.GenerateAsync(sendDirect: true);
                        _worldGeneration.Resend();
                        
                        _visionManager.AttachWorldMap(_worldGeneration.WorldMap, _worldGeneration.BackgroundMap);

                        await Task.Delay(1000);
                        
                        _client.SendChat("/resetplayer @a", prefix: false);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to generate world.");
                    }
                });
            }

            if (chatPacket.Message.StartsWith(".hide"))
            {
                _client.SendPm(player.Username, "Hiding all tiles.");
                _visionManager.HideAll();
            }
            
            if (chatPacket.Message.StartsWith(".show"))
            {
                _client.SendPm(player.Username, "Showing all tiles.");
            }
            
            if (chatPacket.Message.StartsWith(".start"))
            {
                _client.SendPm(player.Username, "Starting game loop.");
                _ = Start();
            }
            
            if (chatPacket.Message.StartsWith(".stop"))
            {
                _client.SendPm(player.Username, "Stopping game loop.");
                _ = Stop();
            }

            
            if (chatPacket.Message.StartsWith(".discover"))
            {
                _client.SendPm(player.Username, "Discovering tile.");

                var position = player.GetWorldPosition(_world);
                _visionManager.FloodDiscovery(position);
                _visionManager.SendBlockUpdate();
            }

            if (chatPacket.Message.StartsWith(".resend"))
            {
                _worldGeneration.Resend();
            }
            
            if (chatPacket.Message.StartsWith(".show treasure"))
            {
                ShowTreasure();
            }
            
            if (chatPacket.Message.StartsWith(".allow"))
            {
                AllowOthers = !AllowOthers;
                _client.SendChat($"Others can dig: {AllowOthers}");
            }
        };
        

        _client.OnPacketReceived += (sender, packet) =>
        {
            if (packet is SystemMessagePacket message)
            {
                Console.WriteLine("System message: " + message.Message);
            }
            
            if (packet is PlayerMovedPacket movedPacket)
            {
                var player = _playerManager.GetPlayer(movedPacket.PlayerId);
                if (player == null) return;
            
                if (player.Godmode) return;
                // !string.Equals(player.Username, "MARTEN", StringComparison.InvariantCultureIgnoreCase)
                if (!AllowOthers && true) return;
               
                if (movedPacket.Horizontal != 0 || movedPacket.Vertical != 0)
                {
                    // Diggy dig.
                    int x = (int)movedPacket.Position.X / 16 + movedPacket.Horizontal;
                    int y = (int)movedPacket.Position.Y / 16 + movedPacket.Vertical;
            
                    if (x < 0 || x >= _world.Width || y < 0 || y >= _world.Height) return;

                    var block = _world.BlockAt(WorldLayer.Foreground, x, y).Block;
                    if (block.CanDig())
                    {
                        
                        _client.Send(new PlacedBlock(x, y, WorldLayer.Foreground, new BasicBlock(PixelBlock.Empty))
                            .AsPacketOut());

                        var worldX = x;
                        var worldY = _world.Height - y - 1;

                        // Now fill in a 3x3 area for the floodfill of this block.
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            for (int dy = -2; dy <= 2; dy++)
                            {
                                _visionManager.DiscoverTile(worldX + dx, worldY + dy);
                                if (worldX + dx < 0 || worldX + dx >= _world.Width || worldY + dy < 0 || worldY + dy >= _world.Height) continue;
                                if (!_worldGeneration.WorldMap.IsSolidBlock(worldX + dx, worldY + dy))
                                {
                                    _visionManager.FloodDiscovery(worldX + dx, worldY + dy);
                                }
                            }
                        }
                        
                        _visionManager.SendBlockUpdate();

                        if (block == PixelBlock.PirateChestBrown)
                        {
                            AllowOthers = false;
                            _client.SendChat($"The treasure was found by {player.Username}!");
                            _client.SendChat($"/giveeffect {player.Username} Fly 10000", prefix: false);
                            _generateNewGame = true;
                            _lastWinner = player;
                            
                            _client.SendChat($"/tp @a @r[id={player.Id}]",  prefix: false);
                            ShowTreasure();
                        }
                    }
                    else if (block == PixelBlock.Empty)
                    {
                        if (movedPacket.Vertical == -1 && movedPacket.Horizontal == 0)
                        {
                            _client.Send(new PlacedBlock(x, y, WorldLayer.Foreground, new BasicBlock(PixelBlock.GravityDot))
                                .AsPacketOut());
                            _client.Send(new PlacedBlock(x, y, WorldLayer.Background, new BasicBlock(PixelBlock.PirateWoodPlankDarkBrownBg))
                                .AsPacketOut());
                        }
                    }
                }
            }
        };

        await _client.Connect(_botConfiguration.WorldId);

        await Task.Delay(1000);
        _client.SendChat("Hello World!");

        await Setup();
    }

    private bool _isRunning = false;

    private bool _generateNewGame = false;
    private CancellationTokenSource _gameLoopCancelToken = new();
    private Task? _gameLoop = null;
    private Player? _lastWinner;


    private void ShowTreasure()
    {
        foreach(var treasurePoint in _treasureLocations)
        {
            _visionManager.CubicDiscovery(treasurePoint.X, treasurePoint.Y, 5);
        }
        _visionManager.SendBlockUpdate();
    }
    
    public async Task Start()
    {
        await Stop();

        _gameLoopCancelToken = new();
        _gameLoop = Task.Run(async () =>
        {
            try
            {
                await _createGameLoop();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occured in the game loop.");
            }
        });
        _generateNewGame = true;
    }

    public async Task Stop()
    {
        _generateNewGame = false;
        if (_gameLoop == null) return;
        await _gameLoopCancelToken.CancelAsync();

        try
        {
            await _gameLoop;
        }
        catch (OperationCanceledException ex)
        {
            // Expected
        }

        _gameLoop = null;
    }

    public async Task _createGameLoop()
    {
        if (_isRunning) return;

        while (_gameLoopCancelToken.IsCancellationRequested == false && _client.IsConnected)
        {
            if (!_generateNewGame)
            {
                await Task.Delay(1000);
                continue;
            }
            
            _generateNewGame = false;
            _treasureLocations.Clear();

            var seedRandom = new Random();
            _worldGeneration.Seed = seedRandom.Next() % 10000;
            
            await Task.Delay(5000);
            
            await _worldGeneration.GenerateAsync(sendDirect: false);
            _visionManager.AttachWorldMap(_worldGeneration.WorldMap, _worldGeneration.BackgroundMap);

            var treasureAmount = Math.Max(2, 6 - _playerManager.Players.Count());
            for (int i = 0; i < treasureAmount; i++)
            {
                _placeTreasure(_worldGeneration.Seed);
            }

            
            _visionManager.HideAll();
            
            _visionManager.FloodDiscovery(_worldGeneration.SpawnX, _worldGeneration.SpawnY - 1);
            _visionManager.SendBlockUpdate();
            
            // _worldGeneration.Resend();

            await Task.Delay(1000);
                        
            _client.SendChat("/resetplayer @a", prefix: false);
            _client.SendChat($"Find the treasure! {treasureAmount} have been hidden. Good luck!");

            if (_lastWinner != null)
            {
                _client.SendChat($"/givecrown #{_lastWinner.Id}", prefix: false);
            }
            
            await Task.Delay(2000);
            
            // Place the treasure block
            AllowOthers = true;
        }
    }

    private void _placeTreasure(int seed)
    {
        // Generate a random point
        var rnd = new Random(seed);
        int x,y;
        do
        {
            x = rnd.Next(_world.Width - 20) + 10;
            y = _world.Height - rnd.Next(_world.Height - 60) - 60;
            
        } while (_worldGeneration.WorldMap.IsSolidBlock(x, y));
        
        while (_worldGeneration.WorldMap.IsSolidBlock(x, y)) y--;
        
        _treasureLocations.Add(new Point(x,y));
        _worldGeneration.WorldMap.PlaceBlock(x, y, new BasicBlock(PixelBlock.PirateChestBrown));

        var treasureFloorOptions = new List<PixelBlock>()
        {
            PixelBlock.BeveledGreen,
            PixelBlock.BeveledCyan,
            PixelBlock.BeveledMagenta
        };

        var treasureFloor = treasureFloorOptions[rnd.Next(treasureFloorOptions.Count)];
        
        _worldGeneration.WorldMap.PlaceBlock(x - 1, y - 1, new BasicBlock(treasureFloor));
        _worldGeneration.WorldMap.PlaceBlock(x, y - 1, new BasicBlock(treasureFloor));
        _worldGeneration.WorldMap.PlaceBlock(x + 1, y - 1, new BasicBlock(treasureFloor));
    }

    public async Task Setup()
    {
        _client.SendChat("/clearworld", prefix: false);
        await _world.InitTask;
    }

    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping CaveGameBot");
    }
}