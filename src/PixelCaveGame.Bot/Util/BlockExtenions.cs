using PixelPilot.Client.World.Constants;

namespace PixelCaveGame.Bot.Util;

public static class BlockExtenions
{
    private static readonly Dictionary<PixelBlock, bool> _diggableCache = new();
    
    public static bool CanDig(this PixelBlock block)
    {
        if (_diggableCache.TryGetValue(block, out var result))
        {
            return result;
        }

        result = _canDig(block);
        _diggableCache.Add(block, result);

        return result;
    }

    private static bool _canDig(PixelBlock block)
    {
        switch (block)
        {
            case PixelBlock.Empty:
            case PixelBlock.LiquidLava:
            case PixelBlock.LiquidMud:
            case PixelBlock.LiquidWater:
            case PixelBlock.LiquidWaste:
            case PixelBlock.ClimbableLadderMetal:
            case PixelBlock.ClimbableLadderWood:
                return false;
        }

        var blockName = block.ToString();

        if (blockName.StartsWith("Tool")) return false;
        if (blockName.StartsWith("Beveled")) return false;
        if (blockName.StartsWith("Gravity")) return false;
        
        // Default is true
        return true;
    }
}