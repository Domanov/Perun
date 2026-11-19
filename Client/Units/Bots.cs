using System;
using EFT;

namespace Perun.Client.Units;

internal static class Bots
{
    public static Func<Player, bool> ObservedBot { get; set; }

    // The BTR's gunner is part of the vehicle; ordering or removing it breaks the BTR.
    public static bool IsProtected(Player player) => player?.Profile?.Info?.Settings?.Role == WildSpawnType.shooterBTR;

    public static bool IsBot(Player player)
    {
        if (player == null)
        {
            return false;
        }

        if (player.IsAI)
        {
            return true;
        }

        Func<Player, bool> observed = ObservedBot;
        return observed != null && observed(player);
    }
}
