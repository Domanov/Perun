using System;
using System.Collections.Generic;
using EFT;

namespace Perun.Client.Units;

internal static class StandByGuard
{
    private sealed class Entry
    {
        public bool CanStandBy;
        public int Holds;
        public List<BotOwner> Followers;
    }

    private static readonly Dictionary<BotOwner, Entry> Held = new();

    public static bool Hold(BotOwner bot)
    {
        bool woke = HoldOne(bot);
        // A follower left in standby stands still while its woken boss walks away.
        if (bot?.Boss != null && bot.Boss.IamBoss && Held.TryGetValue(bot, out Entry entry) && entry.Holds == 1)
        {
            try
            {
                foreach (BotOwner follower in bot.Boss._followers.Followers)
                {
                    Join(bot, follower);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Couldn't wake {bot.Profile?.Nickname}'s followers: {ex.Message}");
            }
        }
        return woke;
    }

    public static void Join(BotOwner boss, BotOwner follower)
    {
        if (boss == null || follower == null || follower == boss || !Held.TryGetValue(boss, out Entry entry))
        {
            return;
        }
        entry.Followers ??= new List<BotOwner>();
        if (!entry.Followers.Contains(follower))
        {
            entry.Followers.Add(follower);
            HoldOne(follower);
        }
    }

    public static void Release(BotOwner bot)
    {
        if (bot == null || !Held.TryGetValue(bot, out Entry entry))
        {
            return;
        }

        if (entry.Holds > 1)
        {
            entry.Holds--;
            return;
        }

        Held.Remove(bot);
        try
        {
            if (bot.StandBy != null)
            {
                bot.StandBy.CanDoStandBy = entry.CanStandBy;
            }
        }
        catch (Exception)
        {
        }
        if (entry.Followers != null)
        {
            foreach (BotOwner follower in entry.Followers)
            {
                Release(follower);
            }
        }
    }

    public static void Clear() => Held.Clear();

    private static bool HoldOne(BotOwner bot)
    {
        if (bot == null || bot.StandBy == null)
        {
            return false;
        }

        try
        {
            if (Held.TryGetValue(bot, out Entry entry))
            {
                entry.Holds++;
            }
            else
            {
                Held[bot] = new Entry { CanStandBy = bot.StandBy.CanDoStandBy, Holds = 1 };
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
}
