using System.Drawing;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using PixelPilot.Client;
using PixelPilot.Client.World.Blocks;
using PixelPilot.Client.World.Blocks.Placed;
using PixelPilot.Client.World.Constants;
using PixelPilot.Structures.Extensions;

namespace PixelCaveGame.Bot.Generation;

public class VisionManager
{
    private PixelPilotClient _client;

    /// <summary>
    /// Reference to the blocks that are generated.
    /// </summary>
    public PixelWalkerBitMap WorldBlocks { get; set; } = new(0, 0, 1);
    public PixelWalkerBitMap BackgroundBlocks { get; set; } = new(0, 0, 1);

    /// <summary>
    /// The discovered blocks.
    /// </summary>
    private bool[,] DiscoveredBlocks { get; set; } = new bool[0, 0];
    
    public bool[,] FloodFilledBlocks { get; set; }
    
    /// <summary>
    /// Newly discovered blocks.
    /// </summary>
    private Stack<Point> NewlyDiscoveredBlocks { get; } = new();
    
    public VisionManager(PixelPilotClient client)
    {
        _client = client;
    }

    public void AttachWorldMap(PixelWalkerBitMap worldMap, PixelWalkerBitMap backgroundMap)
    {
        WorldBlocks = worldMap;
        BackgroundBlocks = backgroundMap;
        
        DiscoveredBlocks = new bool[WorldBlocks.Width, WorldBlocks.Height];
        FloodFilledBlocks = new bool[WorldBlocks.Width, WorldBlocks.Height];
    }


    public void DiscoverTile(int x, int y)
    {
        if (x < 0 || x >= WorldBlocks.Width) return;
        if (y < 0 || y >= WorldBlocks.Height) return;
        
        // Don't resend blocks. Already discovered.
        if (DiscoveredBlocks[x, y]) return;
        
        DiscoveredBlocks[x, y] = true;
        NewlyDiscoveredBlocks.Push(new Point(x, y));
    }
    
    public void DiscoverTile(Point tileLocation)
    {
       DiscoverTile(tileLocation.X, tileLocation.Y);
    }

    public void HideAll()
    {
        var blocks = new List<IPlacedBlock>();
        for (int x = 0; x < WorldBlocks.Width; x++)
        {
            for (int y = 0; y < WorldBlocks.Height; y++)
            {
                DiscoveredBlocks[x, y] = false;
                FloodFilledBlocks[x, y] = false;
                blocks.Add(new PlacedBlock(x, y, WorldLayer.Foreground, new BasicBlock(PixelBlock.GenericBlackTransparent)));
            }
        }
        
        _client.SendRange(blocks.ToChunkedPackets());
    }

    public void SendBlockUpdate()
    {
        var blocks = new List<IPlacedBlock>();
        while (NewlyDiscoveredBlocks.Count > 0)
        {
            var point = NewlyDiscoveredBlocks.Pop();
            blocks.Add(new PlacedBlock(point.X, WorldBlocks.Height - point.Y - 1, WorldLayer.Foreground, WorldBlocks.BlockAt(point.X, point.Y)));
            
            var background = BackgroundBlocks.BlockAt(point.X, point.Y);
            if (background.Block != PixelBlock.Empty)
            {
                blocks.Add(new PlacedBlock(point.X, WorldBlocks.Height - point.Y - 1, WorldLayer.Background, background));
            }
        }

        _client.SendRange(blocks.ToChunkedPackets());
    }

    public void FloodDiscovery(Point tilePosition)
    {
        FloodDiscovery(tilePosition.X, tilePosition.Y);
    }

    public void FloodDiscovery(int x, int y)
    {
        if (x < 0 || x >= WorldBlocks.Width) return;
        if (y < 0 || y >= WorldBlocks.Height) return;
        
        Stack<Point> toCheck = new Stack<Point>();
        Stack<Point> edgeDiscovery = new Stack<Point>();
        var totalFlooded = 0;
        
        toCheck.Push(new Point(x, y));
        edgeDiscovery.Push(new Point(x, y));
        
        while (toCheck.Count > 0)
        {
            var point = toCheck.Pop();

            // Don't do anything with tiles we have already discovered.
            if (FloodFilledBlocks[point.X, point.Y]) continue;
            FloodFilledBlocks[point.X, point.Y] = true;
            
            DiscoverTile(point.X, point.Y);
            totalFlooded++;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    // Ensure we stay inside the world.
                    if (point.X + dx < 0 || point.X + dx >= WorldBlocks.Width) continue;
                    if (point.Y + dy < 0 || point.Y + dy >= WorldBlocks.Height) continue;
                    
                    // Block already discovered. Don't do anything.
                    if (DiscoveredBlocks[point.X + dx, point.Y + dy]) continue;
                    
                    // If it's empty add it such that we can traferse
                    if (!WorldBlocks.IsSolidBlock(point.X + dx, point.Y + dy))
                    {
                        toCheck.Push(new Point(point.X + dx, point.Y + dy));
                    }
                    else
                    {
                        // Just discover the tile (edges) but don't check it's neighbours.
                        edgeDiscovery.Push(new Point(point.X + dx, point.Y + dy));
                    }
                }
            }
        }

        while (edgeDiscovery.Count > 0)
        {
            var point = edgeDiscovery.Pop();
            CubicDiscovery(point.X, point.Y, 3);
        }
    }

    public void CubicDiscovery(int x, int y, int size)
    {
        DiscoverTile(x, y);
        
        for (int dx = -1 * (size / 2); dx <= (size / 2); dx++)
        {
            for (int dy = -1 * (size / 2); dy <= (size / 2); dy++)
            {
                // Ensure we stay inside the world.
                if (x + dx < 0 || x + dx >= WorldBlocks.Width) continue;
                if (y + dy < 0 || y + dy >= WorldBlocks.Height) continue;
                
                // Just discover the tile (edges) but don't check it's neighbours.
                DiscoverTile(x + dx, y + dy);
            }
        }
    }
}