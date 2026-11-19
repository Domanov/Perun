using System;
using System.Collections.Generic;
using EFT;

namespace Perun.Client.Units;

internal static class StandByGuard
{
    private static readonly Dictionary<BotOwner, (bool canStandBy, int holds)> Held = new();

    public static bool Hold(BotOwner bot)
    {
        if (bot == null || bot.StandBy == null)
        {
            return false;
        }

        try
        {
            if (Held.TryGetValue(bot, out var held))
            {
                Held[bot] = (held.canStandBy, held.holds + 1);
            }
            else
            {
                Held[bot] = (bot.StandBy.CanDoStandBy, 1);
            }
            bot.StandBy.CanDoStandBy = false;
            if (bot.StandBy.StandByType != BotStandByType.active)
            {
                Plugin.Log.LogInfo($"Woke {bot.Profile?.Nickname} up (standby {bot.StandBy.StandByType})");
                bot.StandBy.Activate();
                return true;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Couldn't wake {bot.Profile?.Nickname}: {ex.Message}");
        }
        return false;
    }

    public static void Release(BotOwner bot)
    {
        if (bot == null || !Held.TryGetValue(bot, out var held))
        {
            return;
        }

        if (held.holds > 1)
        {
            Held[bot] = (held.canStandBy, held.holds - 1);
            return;
        }

        Held.Remove(bot);
        try
        {
            if (bot.StandBy != null)
            {
                bot.StandBy.CanDoStandBy = held.canStandBy;
            }
        }
        catch (Exception)
        {
        }
    }

    public static void Clear() => Held.Clear();
}
