using System;
using EFT;

namespace Perun.Client.Units;

internal static class Bots
{
    public static Func<Player, bool> ObservedBot { get; set; }

    // The BTR's gunner is part of the vehicle; ordering or removing it breaks the BTR.
    public static bool IsProtected(Player player) => player?.Profile?.Info?.Settings?.Role == WildSpawnType.shooterBTR;

    public static BotOwner LeaderOf(BotOwner bot)
    {
        BotFollower follower = bot?.BotFollower;
        if (follower == null || !follower.HaveBoss || follower.BossToFollow is not BotBoss boss)
        {
            return null;
        }
        BotOwner leader = boss.Owner;
        return leader != null && leader != bot && !leader.IsDead ? leader : null;
    }

    public static bool IsLeader(BotOwner bot) => bot != null && !bot.IsDead && bot.Boss != null && bot.Boss.IamBoss;

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
