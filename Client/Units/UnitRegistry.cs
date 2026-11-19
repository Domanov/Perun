using System.Collections.Generic;
using EFT;

namespace Perun.Client.Units;

internal static class UnitRegistry
{
    private static readonly Dictionary<string, BotOwner> Units = new();
    private static readonly List<string> Dead = new();

    public static int AliveCount
    {
        get
        {
            Prune();
            return Units.Count;
        }
    }

    public static void Add(BotOwner bot)
    {
        if (bot != null && bot.Profile != null)
        {
            Units[bot.Profile.Id] = bot;
        }
    }

    public static bool IsOurs(Player player) => player != null && player.Profile != null && Units.ContainsKey(player.Profile.Id);

    public static void Clear() => Units.Clear();

    public static void CopyIds(List<string> target)
    {
        Prune();
        foreach (string id in Units.Keys)
        {
            target.Add(id);
        }
    }

    private static void Prune()
    {
        Dead.Clear();
        foreach (KeyValuePair<string, BotOwner> unit in Units)
        {
            BotOwner bot = unit.Value;
            Player player = bot != null ? bot.GetPlayer : null;
            if (bot == null || bot.IsDead || player == null || player.HealthController == null || !player.HealthController.IsAlive)
            {
                Dead.Add(unit.Key);
            }
        }
        foreach (string id in Dead)
        {
            Units.Remove(id);
        }
    }
}
