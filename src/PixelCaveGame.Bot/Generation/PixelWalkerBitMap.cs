using Google.Protobuf;
using PixelPilot.Client.World.Blocks;
using PixelPilot.Client.World.Blocks.Placed;
using PixelPilot.Client.World.Constants;
using PixelPilot.Structures.Extensions;

namespace PixelCaveGame.Bot.Generation;

public class PixelWalkerBitMap
{
    public int Height { get; private set; }
    public int Width { get; private set; }
    private int _layer;

    private IPixelBlock[,] _blocks;

    public PixelWalkerBitMap(int height, int width, int layer)
    {
        Height = height;
        Width = width;
        _layer = layer;

        _blocks = new IPixelBlock[width, height];
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                _blocks[x, y] = new BasicBlock(PixelBlock.Empty);
            }
        }
    }

    public void PlaceBlock(int x, int y, IPixelBlock block)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        _blocks[x, y] = block;
    }

    public IPixelBlock BlockAt(int x, int y)
    {
        return _blocks[x, y];
    }
    public bool IsSolidBlock(int x, int y)
    {
        return _blocks[x,y].Block != PixelBlock.Empty;
    }
    public List<IMessage> GeneratePackets()
    {
        var blockList = new List<IPlacedBlock>();
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var block = _blocks[x, y];
                blockList.Add(new PlacedBlock(x, Height - y - 1, _layer, block));
            }
        }

        var chunks = blockList.ToChunkedPackets();
        return chunks;
    }
}