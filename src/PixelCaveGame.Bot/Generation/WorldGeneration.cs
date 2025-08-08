using PixelPilot.Client;
using PixelPilot.Client.World;
using PixelPilot.Client.World.Blocks;
using PixelPilot.Client.World.Blocks.Placed;
using PixelPilot.Client.World.Constants;

namespace PixelCaveGame.Bot.Generation;

/// <summary>
/// Generate terrain / a world.
/// Uses an inverted Y instead of PW weird going up Y goes down schema. Tech debt.? Maybe...
/// </summary>
public class WorldGeneration
{
    private PixelWorld _world;
    private PixelPilotClient _client;
    private PixelWalkerBitMap _worldMap;
    private PixelWalkerBitMap _backgroundMap;
    public int Seed { get; set; }
    private Random _rnd { get; set; }

    public int SeaHeight { get; private set; }
    public int[] TerrainHeight { get; private set; }
    private int WorldWidth => _world.Width;
    private int WorldHeight => _world.Height;
    
    public int SpawnX { get; private set; }
    public int SpawnY { get; private set; }
    
    public PixelWalkerBitMap WorldMap => _worldMap;
    public PixelWalkerBitMap BackgroundMap => _backgroundMap;

    public WorldGeneration(PixelWorld world, PixelPilotClient client)
    {
        _world = world;
        _client = client;
    }

    public async Task GenerateAsync(bool sendDirect = false)
    {
        _client.SendChat("/clearworld", prefix: false);
        
        SeaHeight = _world.Height - 40;
        _worldMap = new PixelWalkerBitMap(_world.Width, _world.Height, 1);
        _backgroundMap = new PixelWalkerBitMap(_world.Width, _world.Height, 0);
        
        _rnd = new Random(Seed);
        TerrainHeight = new int[WorldWidth];
        
        GenerateBaseTerrain();

        GenerateOres();

        GenerateCaves();
  
        GenerateTopLevelDecorations();
  
        for (int x = 0; x < WorldWidth; x++)
        {
            _worldMap.PlaceBlock(x, 0, new BasicBlock(PixelBlock.GenericYellow));
        }
        
        // Generate the spawn point
        SpawnX = (int) _rnd.NextInt64(WorldWidth / 2) + WorldWidth / 4;
        SpawnY = TerrainHeight[SpawnX] + 3;
        
        _worldMap.PlaceBlock(SpawnX, SpawnY, new BasicBlock(PixelBlock.ToolSpawnLobby));
        _worldMap.PlaceBlock(SpawnX - 1, TerrainHeight[SpawnX - 1], new BasicBlock(PixelBlock.BeveledYellow));
        _worldMap.PlaceBlock(SpawnX, TerrainHeight[SpawnX], new BasicBlock(PixelBlock.BeveledYellow));
        _worldMap.PlaceBlock(SpawnX + 1, TerrainHeight[SpawnX + 1], new BasicBlock(PixelBlock.BeveledYellow));
        
        _worldMap.PlaceBlock(0, 0, new BasicBlock(PixelBlock.GenericStripedHazardBlack));
        _worldMap.PlaceBlock(WorldWidth - 1, 0, new BasicBlock(PixelBlock.GenericStripedHazardBlack));
        _worldMap.PlaceBlock(0, WorldHeight - 1, new BasicBlock(PixelBlock.GenericStripedHazardBlack));
        _worldMap.PlaceBlock(WorldWidth - 1, WorldHeight - 1, new BasicBlock(PixelBlock.GenericStripedHazardBlack));

        if (sendDirect)
        {
            _client.SendRange(_worldMap.GeneratePackets());
            _client.SendRange(_backgroundMap.GeneratePackets());
        }
    }

    public void Resend()
    {
        _client.SendRange(_worldMap.GeneratePackets());
        _client.SendRange(_backgroundMap.GeneratePackets());
    }

    private void GenerateBaseTerrain()
    {
        FastNoiseLite terrainNoise = GetNoise();
        terrainNoise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        terrainNoise.SetFrequency(0.02f);

        FastNoiseLite terrainDepth = GetNoise();
        terrainDepth.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        terrainDepth.SetFrequency(0.1f);
        
        FastNoiseLite stonePatch = GetNoise();
        stonePatch.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        stonePatch.SetFrequency(0.08f);
        
        for (int x = 0; x < WorldWidth; x++)
        {
            var terrainY = (int) (terrainNoise.GetNoise(x, 0) * 20) + SeaHeight;
            TerrainHeight[x] = terrainY;
            
            _worldMap.PlaceBlock(x, terrainY, new BasicBlock(PixelBlock.GrassBrickMiddle));
            _backgroundMap.PlaceBlock(x, terrainY, new BasicBlock(PixelBlock.GardenGrassBg));
            
            var depth = (int) (Math.Max(-5, terrainDepth.GetNoise(x, 0) * 20)) + 8;
            var nextDepthY = terrainY - depth;
            for (int y = 0; y < depth; y++)
            {
                _worldMap.PlaceBlock(x, nextDepthY + y, new BasicBlock(PixelBlock.BrickBrown));
                _backgroundMap.PlaceBlock(x, nextDepthY + y,  new BasicBlock(PixelBlock.BrickBrownBg));
            }
            
            depth = (int) (Math.Max(-2, terrainDepth.GetNoise(x, 100) * 15)) + 5;
            nextDepthY = nextDepthY - depth;
            for (int y = 0; y < depth; y++)
            {
                _worldMap.PlaceBlock(x, nextDepthY + y, new BasicBlock(PixelBlock.BrickOlive));
                _backgroundMap.PlaceBlock(x, nextDepthY + y, new BasicBlock(PixelBlock.BrickOliveBg));
            }
            
            // Fill the rest
            for (int y = nextDepthY; y >= 0; y--)
            {
                // The closer to the bottom, increase black spawn rate.
                if (y > 50)
                {
                    _worldMap.PlaceBlock(x, y, new BasicBlock(PixelBlock.BrickGray));
                    _backgroundMap.PlaceBlock(x, y, new BasicBlock(PixelBlock.BrickGrayBg));
                }
                else
                {
                    if (y <= 10 || y <= _rnd.Next(50))
                    {
                        _worldMap.PlaceBlock(x, y, new BasicBlock(PixelBlock.BrickBlack));
                        _backgroundMap.PlaceBlock(x, y, new BasicBlock(PixelBlock.BrickGrayBg));
                    }
                    else
                    {
                        _worldMap.PlaceBlock(x, y, new BasicBlock(PixelBlock.BrickGray));
                        _backgroundMap.PlaceBlock(x, y, new BasicBlock(PixelBlock.BrickGrayBg));
                    }
                }
                
            }

            // fill the sky.
            for (int y = terrainY + 1; y < WorldHeight; y++)
            {
                _backgroundMap.PlaceBlock(x, y, new BasicBlock(PixelBlock.PastelBlueBg));
            }
        }
        
        DoForAll((x, y) =>
        {
            if (y >= 140) return BlockResult.None;
            if (_rnd.Next(140) < y) return BlockResult.None; 
            
            var noiseValue = stonePatch.GetNoise(x + 3432, y + 432);
            
            if (noiseValue < 0.02) return new BlockResult(PixelBlock.StoneGray, PixelBlock.StoneGrayBg);
            if (noiseValue < 0.03) return new BlockResult(PixelBlock.StoneBlue, PixelBlock.StoneBlueBg);
            if (noiseValue < 0.04) return new BlockResult(PixelBlock.StoneGreen, PixelBlock.StoneGreenBg);
            
           
           
            return BlockResult.None;
        });
    }

    private void GenerateOres()
    {
        FastNoiseLite orePatches = GetNoise();
        orePatches.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        orePatches.SetFrequency(0.5f);

        DoForAll((x, y) =>
        {
            if (y >= TerrainHeight[x] - 20) return null;
            
            var patchNoise = orePatches.GetNoise(x, y);

            if (patchNoise > 0.76 && y >= 25 && y <= 150)
            {
                GeneratePatch(x, y, 4, 8, new BasicBlock(PixelBlock.MineralsOrange));
            }
            return null;
        });
        
        DoForAll((x, y) =>
        {
            if (y >= TerrainHeight[x] - 20) return null;
            
            var patchNoise = orePatches.GetNoise(x, y + 100);

            if (patchNoise > 0.76 && y >= 50)
            {
                GeneratePatch(x, y, 2, 6, new BasicBlock(PixelBlock.MineralsGreen));
            }
            return null;
        });
        
        DoForAll((x, y) =>
        {
            if (y >= TerrainHeight[x] - 20) return null;
            
            var patchNoise = orePatches.GetNoise(x, y + 200);

            if (patchNoise > 0.76 && y >= 100)
            {
                GeneratePatch(x, y, 1, 2, new BasicBlock(PixelBlock.MineralsCyan));
            }
            return null;
        });
    }

    private void GeneratePatch(int x, int y, int min, int max, IPixelBlock block)
    {
        var placeMents = _rnd.NextInt64(max - min) + min;
        for (int i = 0; i < placeMents; i++)
        {
            _worldMap.PlaceBlock(x, y, block);

            if (_rnd.NextDouble() > 0.75) x++;
            if (_rnd.NextDouble() > 0.5) x--;
            if (_rnd.NextDouble() > 0.25) y++;
            else y--;
        }
    }

    private void GenerateCloud(int x, int y, int size)
    {
        // Method to place a block: _worldMap.PlaceBlock(x, y + dy, new BasicBlock(PixelBlock.<BLOCKNAME>));
        // Available blocks:
        // CloudWhiteCenter,
        // CloudWhiteTop,
        // CloudWhiteBottom,
        // CloudWhiteLeft,
        // CloudWhiteRight,
        // CloudWhiteTopRight,
        // CloudWhiteTopLeft,
        // CloudWhiteBottomLeft,
        // CloudWhiteBottomRight,

        Random random = _rnd;

        double width = size * 1.5;  // Wider
        double height = size * 0.75; // Flatter

        for (int dx = -(int)width; dx <= (int)width; dx++)
        {
            for (int dy = -(int)height; dy <= (int)height; dy++)
            {
                double normalizedDistance = Math.Sqrt((dx * dx) / (width * width) + (dy * dy) / (height * height));

                if (normalizedDistance <= 1.0)
                {
                    // Chance to skip blocks near the edge to make it more irregular
                    double edgeThreshold = normalizedDistance; // Closer to 1.0 near the edge
                    if (random.NextDouble() > edgeThreshold * 0.85) // 0.8 adjusts how "gappy" the edges are
                    {
                        _worldMap.PlaceBlock(x + dx, y + dy, new BasicBlock(PixelBlock.CloudWhiteCenter));
                    }
                }
            }
        }
    }
    

    private void GenerateTree(int x, int y, int height)
    {
        // Define valid block pairs
        var woodPairs = new (PixelBlock Foreground, PixelBlock Background)[]
        {
            (PixelBlock.FactoryWood, PixelBlock.MedievalWoodBg),
            (PixelBlock.DomesticWood, PixelBlock.MedievalWoodBg),
            (PixelBlock.EnvironmentLog, PixelBlock.EnvironmentLogBg),
        };

        var leavesPairs = new (PixelBlock Foreground, PixelBlock Background)[]
        {
            (PixelBlock.BasicGreen, PixelBlock.BasicGreenBg),
            (PixelBlock.GardenLeaves, PixelBlock.GardenLeavesBg),
            (PixelBlock.BrickGreen, PixelBlock.BrickGreenBg),
            (PixelBlock.KeyGreenDoor, PixelBlock.BasicGreenBg),
        };

        var random = _rnd;

        // Select random pairs
        var woodPair = woodPairs[random.Next(woodPairs.Length)];
        var leavesPair = leavesPairs[random.Next(leavesPairs.Length)];

        // Generate trunk
        for (int dy = 0; dy < height; dy++)
        {
            if (dy >= 3) _worldMap.PlaceBlock(x, y + dy, new BasicBlock(woodPair.Foreground));
            _backgroundMap.PlaceBlock(x, y + dy, new BasicBlock(woodPair.Background));
        }

        // Generate canopy
        int canopyStart = height - 2;
        int canopyHeight = (int) _rnd.NextInt64(2) + 2;
        for (int dy = canopyStart; dy <= canopyStart + canopyHeight; dy++)
        {
            int radius = canopyStart + canopyHeight - dy + 1; // Tapers toward top
            for (int dx = -radius; dx <= radius; dx++)
            {
                int blockX = x + dx;
                _worldMap.PlaceBlock(blockX, y + dy, new BasicBlock(leavesPair.Foreground));
                _backgroundMap.PlaceBlock(blockX, y + dy, new BasicBlock(leavesPair.Background));
            }
        }
    }
    
    private void GenerateCaves()
    {
        FastNoiseLite caveNoise = GetNoise();
        caveNoise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        caveNoise.SetFrequency(0.1f);
        
        FastNoiseLite edgeGeneration = GetNoise();
        edgeGeneration.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        edgeGeneration.SetFrequency(0.3f);
        
        FastNoiseLite caveOpeningNoise = GetNoise();
        caveOpeningNoise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        caveOpeningNoise.SetFrequency(0.01f);
        
        DoForAll((x, y) =>
        {
            var cave = caveNoise.GetNoise(x, y);
            var edge = edgeGeneration.GetNoise(x, y);

            if (y >= TerrainHeight[x] - 10)
            {
                if (cave > 0.35 && caveOpeningNoise.GetNoise(x, y) > 0.1)
                {
                    return new BlockResult(PixelBlock.Empty, null);
                }

                return null;
            }
            
            if (cave > 0.15)
            {
                if (y <= 20)
                {
                    return new BlockResult(PixelBlock.LiquidLava, PixelBlock.LavaDarkRedBg);
                }
                
                return new BlockResult(PixelBlock.Empty, null);
            }

            if (cave > 0 && edge > 0.2)
            {
                if (y <= 20)
                {
                    return new BlockResult(PixelBlock.LiquidLava, PixelBlock.LavaDarkRedBg);
                }
                
                return new BlockResult(PixelBlock.Empty, null);
            }
            
            return null;
        });
    }

    private void GenerateTopLevelDecorations()
    {
        var random = new Random(Seed);
        var decoList = new List<PixelBlock>()
        {
            PixelBlock.MeadowYellowFlower,
            PixelBlock.MeadowSmallBush,
            PixelBlock.BeachDryBush,

            PixelBlock.FairytaleFlowerPink,
            PixelBlock.FairytaleFlowerOrange,
            PixelBlock.FairytaleFlowerBlue,
            PixelBlock.FairytaleMushroomDecorationOrange,
            PixelBlock.FairytaleMushroomDecorationRed,
            PixelBlock.Empty,
        };

        for (int x = 0; x < WorldWidth; x++)
        {
            // Check if there is a block beneath:
            if (!_worldMap.IsSolidBlock(x, TerrainHeight[x])) continue;
            
            // If decorated:
            var decoChance = random.NextDouble();

            if (decoChance < 0.4)
            {
                _worldMap.PlaceBlock(x, TerrainHeight[x] + 1, new BasicBlock(decoList[random.Next(decoList.Count)]));
                continue;
            }

            if (decoChance < 0.5)
            {
                GenerateTree(x, TerrainHeight[x] + 1, (int) random.NextInt64(4) + 5);
                continue;
            }

            if (decoChance < 0.55)
            {
                GenerateCloud(x, Math.Min(TerrainHeight[x] + (int) random.NextInt64(30) + 15,  190), (int) random.NextInt64(5) + 2);
            }
        }
    }
    
    private delegate BlockResult? GenerateBlock(int x, int y);

    private void DoForAll(GenerateBlock generator)
    {
        for (int x = 0; x < WorldWidth; x++)
        {
            for (int y = 0; y < _world.Height; y++)
            {
                var block = generator(x, y);
                if (block == null) continue;

                if (block.ForeGround != null)
                {
                    _worldMap.PlaceBlock(x, y, block.ForeGround);
                }

                if (block.BackGround != null)
                {
                    _backgroundMap.PlaceBlock(x, y, block.BackGround);
                }
            }
        }
    }

    private FastNoiseLite GetNoise()
    {
        var generator = new FastNoiseLite();
        generator.SetSeed(Seed);
        return generator;
    }
}