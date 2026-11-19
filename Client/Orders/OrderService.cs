using System.Collections.Generic;
using Comfort.Common;
using EFT;
using Perun.Client.Units;
using UnityEngine;
using UnityEngine.AI;

namespace Perun.Client.Orders;

internal sealed class MoveOrder
{
    public Vector3 Destination;
    public bool Run;
    public bool Forced;
    public float BestDistance = float.MaxValue;
    public float LastProgressAt;
    public int Repaths;
}

internal static class OrderService
{
    private const float Spacing = 1.6f;
    private const float PruneInterval = 0.5f;

    private static readonly Dictionary<BotOwner, MoveOrder> Orders = new();
    private static readonly List<BotOwner> Gone = new();
    private static GameWorld _world;
    private static float _nextPrune;

    public static int Count => Orders.Count;

    public static void CopyTo(List<PerunOrderInfo> target)
    {
        foreach (KeyValuePair<BotOwner, MoveOrder> entry in Orders)
        {
            BotOwner bot = entry.Key;
            if (bot == null || bot.Profile == null)
            {
                continue;
            }

            target.Add(new PerunOrderInfo
            {
                ProfileId = bot.Profile.Id,
                Destination = entry.Value.Destination,
                Run = entry.Value.Run,
                Forced = entry.Value.Forced
            });
        }
    }

    public static void Tick()
    {
        if (Orders.Count == 0 && _world == null)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (now < _nextPrune)
        {
            return;
        }

        _nextPrune = now + PruneInterval;
        CheckWorld();
    }

    public static bool TryGet(BotOwner bot, out MoveOrder order)
    {
        if (bot != null && Orders.TryGetValue(bot, out order))
        {
            return true;
        }

        order = null;
        return false;
    }

    public static int Move(IList<Player> units, Vector3 point, bool run, bool forced)
    {
        CheckWorld();
        List<BotOwner> bots = new();
        foreach (Player unit in units)
        {
            BotOwner bot = unit != null ? unit.AIData?.BotOwner : null;
            if (bot != null && IsAlive(unit))
            {
                bots.Add(bot);
            }
        }

        bots.Sort((a, b) => (a.Position - point).sqrMagnitude.CompareTo((b.Position - point).sqrMagnitude));
        for (int i = 0; i < bots.Count; i++)
        {
            if (!Orders.ContainsKey(bots[i]))
            {
                Units.StandByGuard.Hold(bots[i]);
            }
            Orders[bots[i]] = new MoveOrder
            {
                Destination = Slot(point, i),
                Run = run,
                Forced = forced,
                LastProgressAt = Time.time
            };
        }

        if (bots.Count > 0)
        {
            Plugin.Log.LogInfo($"Order: {bots.Count} unit(s) {(forced ? "forced move" : run ? "run" : "move")} to {point}");
        }
        return bots.Count;
    }

    public static int Halt(IList<Player> units)
    {
        int count = 0;
        foreach (Player unit in units)
        {
            BotOwner bot = unit != null ? unit.AIData?.BotOwner : null;
            if (bot != null && Orders.Remove(bot))
            {
                Units.StandByGuard.Release(bot);
                bot.Mover?.Stop();
                count++;
            }
        }
        return count;
    }

    public static void Complete(BotOwner bot)
    {
        if (bot != null && Orders.Remove(bot))
        {
            Units.StandByGuard.Release(bot);
        }
    }

    public static void Clear()
    {
        foreach (BotOwner bot in Orders.Keys)
        {
            Units.StandByGuard.Release(bot);
        }
        Orders.Clear();
    }

    private static void CheckWorld()
    {
        GameWorld world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
        if (world != _world)
        {
            _world = world;
            Orders.Clear();
            Units.StandByGuard.Clear();
            return;
        }

        Gone.Clear();
        foreach (BotOwner bot in Orders.Keys)
        {
            if (bot == null || bot.IsDead || !IsAlive(bot.GetPlayer))
            {
                Gone.Add(bot);
            }
        }
        foreach (BotOwner bot in Gone)
        {
            Orders.Remove(bot);
            Units.StandByGuard.Release(bot);
        }
    }

    private static bool IsAlive(Player player) => player != null && player.HealthController != null && player.HealthController.IsAlive;

    internal static Vector3 Slot(Vector3 center, int index)
    {
        if (index == 0)
        {
            return center;
        }

        Vector3 candidate = center + Formation.Offset(index, Spacing);
        return NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas) ? hit.position : center;
    }
}
