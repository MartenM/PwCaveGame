using PixelPilot.Client.World.Blocks;
using PixelPilot.Client.World.Constants;

namespace PixelCaveGame.Bot.Generation;

public class BlockResult
{
    public IPixelBlock? ForeGround = null;
    public IPixelBlock? BackGround = null;

    private BlockResult()
    {
        
    }

    public BlockResult(IPixelBlock? foreGround, IPixelBlock? backGround)
    {
        ForeGround = foreGround;
        BackGround = backGround;
    }
    
    public BlockResult(PixelBlock? foreGround, PixelBlock? backGround)
    {
        if (foreGround != null)
        {
            ForeGround = new BasicBlock(foreGround.Value);
        }
        if (backGround != null)
        {
            BackGround = new BasicBlock(backGround.Value);
        }
    }

    public static BlockResult None => new BlockResult();
    
    public static BlockResult Foreground(IPixelBlock block)
    {
        return new BlockResult(block, null);
    }
    
    public static BlockResult Foreground(PixelBlock block)
    {
        return new BlockResult(new BasicBlock(block), null);
    }
}