using System.Collections.Generic;
using EFT;
using Perun.Client.Mode;
using Perun.Client.Orders;
using UnityEngine;

namespace Perun.Client.Units;

internal interface IJobView
{
    string Title { get; }
    bool IsSpawn { get; }
    LoadStage Stage { get; }
    float Progress { get; }
    int Created { get; }
    int Total { get; }
    float Elapsed { get; }
}

internal interface IRemoteSession
{
    int Pending { get; }
    string LastResult { get; }
    int AliveCount { get; }
    int MaxUnits { get; }
    IReadOnlyList<IJobView> Jobs { get; }
    int ReadyCount(WildSpawnType role, BotDifficulty difficulty);
    bool IsWarming(WildSpawnType role, BotDifficulty difficulty);
    void Prewarm(WildSpawnType role, BotDifficulty difficulty, int count);
    void Spawn(WildSpawnType role, BotDifficulty difficulty, int count, Vector3 position, float yaw);
    bool SpawnGroup(PerunGroupSpawn group);
    int Move(IReadOnlyList<Player> units, Vector3 point, bool run, bool forced);
    int Halt(IReadOnlyList<Player> units);
    void Remove(IReadOnlyList<Player> units, bool kill);
    int Heal(IReadOnlyList<Player> units);
    int Teleport(IReadOnlyList<Player> units, Vector3 point);
    void SetPaused(bool paused);
    bool IsOurs(Player player);
    bool TryGetOrder(Player player, out Vector3 destination, out bool run, out bool forced);
}

internal static class Commands
{
    public static IRemoteSession Remote { get; set; }

    public static bool IsRemote => Remote != null;

    public static int Pending => Remote?.Pending ?? SpawnService.Pending;

    public static string LastResult => Remote != null ? Remote.LastResult : SpawnService.LastResult;

    public static int AliveCount => Remote?.AliveCount ?? UnitRegistry.AliveCount;

    public static int MaxUnits => Remote?.MaxUnits ?? Plugin.MaxUnits.Value;

    public static IReadOnlyList<IJobView> Jobs => Remote != null ? Remote.Jobs : SpawnService.Jobs;

    public static int ReadyCount(WildSpawnType role, BotDifficulty difficulty) =>
        Remote?.ReadyCount(role, difficulty) ?? SpawnService.ReadyCount(role, difficulty);

    public static bool IsWarming(WildSpawnType role, BotDifficulty difficulty) =>
        Remote?.IsWarming(role, difficulty) ?? SpawnService.IsWarming(role, difficulty);

    public static void Prewarm(WildSpawnType role, BotDifficulty difficulty, int count)
    {
        if (Remote != null)
        {
            Remote.Prewarm(role, difficulty, count);
            return;
        }
        SpawnService.Prewarm(role, difficulty, count);
    }

    public static void Spawn(WildSpawnType role, BotDifficulty difficulty, int count, Vector3 position, float yaw)
    {
        if (Remote != null)
        {
            Remote.Spawn(role, difficulty, count, position, yaw);
            return;
        }
        SpawnService.TrySpawn(role, difficulty, count, position, yaw);
    }

    public static bool SpawnGroup(GroupPlan plan, BotDifficulty? difficulty, Vector3 position, float yaw, float spacing)
    {
        if (Remote != null)
        {
            return Remote.SpawnGroup(PerunGroupSpawn.From(plan, difficulty, position, yaw, spacing));
        }
        SpawnService.TrySpawnGroup(plan, difficulty, position, yaw, spacing);
        return true;
    }

    public static void PrewarmGroup(GroupPlan plan, BotDifficulty? difficulty)
    {
        if (Remote == null)
        {
            SpawnService.PrewarmGroup(plan, difficulty);
        }
    }

    public static int GroupReadyCount(GroupPlan plan, BotDifficulty? difficulty) =>
        Remote != null ? 0 : SpawnService.GroupReadyCount(plan, difficulty);

    public static int Move(IReadOnlyList<Player> units, Vector3 point, bool run, bool forced)
    {
        if (Remote != null)
        {
            return Remote.Move(Copy(units), point, run, forced);
        }
        return OrderService.Move(Copy(units), point, run, forced);
    }

    public static int Halt(IReadOnlyList<Player> units)
    {
        if (Remote != null)
        {
            return Remote.Halt(Copy(units));
        }
        return OrderService.Halt(Copy(units));
    }

    public static void Remove(IReadOnlyList<Player> units, bool kill)
    {
        if (Remote != null)
        {
            Remote.Remove(Copy(units), kill);
            return;
        }

        List<Player> copy = Copy(units);
        for (int i = 0; i < copy.Count; i++)
        {
            Player unit = copy[i];
            if (unit == null || unit.IsYourPlayer)
            {
                continue;
            }

            if (kill)
            {
                SpawnService.Kill(unit);
            }
            else
            {
                SpawnService.Despawn(unit);
            }
        }
    }

    public static int Heal(IReadOnlyList<Player> units)
    {
        if (Remote != null)
        {
            return Remote.Heal(Copy(units));
        }
        return Healing.Heal(Copy(units));
    }

    public static int Teleport(IReadOnlyList<Player> units, Vector3 point)
    {
        if (Remote != null)
        {
            return Remote.Teleport(Copy(units), point);
        }
        return Teleporting.Teleport(Copy(units), point);
    }

    public static void SetPaused(bool paused)
    {
        if (Remote != null)
        {
            Remote.SetPaused(paused);
            return;
        }
        WorldPause.Set(paused, true);
    }

    public static bool IsOurs(Player player) => Remote?.IsOurs(player) ?? UnitRegistry.IsOurs(player);

    public static bool TryGetOrder(Player player, out Vector3 destination, out bool run, out bool forced)
    {
        if (Remote != null)
        {
            return Remote.TryGetOrder(player, out destination, out run, out forced);
        }

        BotOwner bot = player != null ? player.AIData?.BotOwner : null;
        if (bot != null && OrderService.TryGet(bot, out MoveOrder order))
        {
            destination = order.Destination;
            run = order.Run;
            forced = order.Forced;
            return true;
        }

        destination = default;
        run = false;
        forced = false;
        return false;
    }

    public static bool HasOrder(Player player) => TryGetOrder(player, out _, out _, out _);

    private static List<Player> Copy(IReadOnlyList<Player> units)
    {
        List<Player> copy = new(units.Count);
        for (int i = 0; i < units.Count; i++)
        {
            if (!Bots.IsProtected(units[i]))
            {
                copy.Add(units[i]);
            }
        }
        return copy;
    }
}
