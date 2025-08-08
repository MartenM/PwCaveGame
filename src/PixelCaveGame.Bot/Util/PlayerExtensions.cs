using System.Drawing;
using PixelPilot.Client.Players;
using PixelPilot.Client.Players.Basic;
using PixelPilot.Client.World;

namespace PixelCaveGame.Bot.Util;

public static class PlayerExtensions
{
    public static Point GetWorldPosition(this IPixelPlayer player, PixelWorld world)
    {
        return new Point(player.BlockX, world.Height - player.BlockY);
    }
}