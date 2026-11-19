using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Comfort.Common;
using EFT;
using Perun.Client.Compat;
using Perun.Client.Mode;
using Perun.Client.Orders;
using Perun.Client.Units;
using Perun.Client.World;
using UnityEngine;

namespace Perun.Client;

public enum PerunOrderKind : byte
{
    Move,
    Halt,
    Remove,
    Kill,
    Heal,
    Teleport
}

public sealed class PerunJobInfo
{
    public string Title;
    public bool IsSpawn;
    public byte Stage;
    public float Progress;
    public int Created;
    public int Total;
    public float Elapsed;
}

public struct PerunOrderInfo
{
    public string ProfileId;
    public Vector3 Destination;
    public bool Run;
    public bool Forced;
}

public sealed class PerunStatus
{
    public string LastResult = "";
    public int Pending;
    public int Alive;
    public int Max;
    public int WatchRole = -1;
    public int WatchDifficulty;
    public int ReadyCount;
    public bool Warming;
    public readonly List<PerunJobInfo> Jobs = new();
    public readonly List<string> OurUnits = new();
    public readonly List<PerunOrderInfo> Orders = new();
}

public interface IGameMasterLink
{
    bool CanClaim { get; }
    void Claim(string password, string profileId);
    void Release();
    void Spawn(int role, int difficulty, int count, Vector3 position, float yaw);
    void Watch(int role, int difficulty, int count);
    void Order(PerunOrderKind kind, string[] profileIds, Vector3 point, bool run, bool forced);
    void Pause(bool paused);
}

public static class PerunGameMaster
{
    public static IGameMasterLink Link { get; set; }

    public static bool IsGameMaster => Commands.Remote != null;

    public static void OnClaimResult(bool granted, string message) => Plugin.Mode?.OnClaimResult(granted, message);

    public static void OnStatus(PerunStatus status) => (Commands.Remote as RemoteSession)?.Apply(status);

    public static void OnLost(string reason) => Plugin.Mode?.OnRemoteLost(reason);
}

public static class PerunHost
{

    private static readonly PerunStatus Status = new();
    private static readonly List<Player> Buffer = new();
    private static readonly Stack<PerunJobInfo> SpareJobs = new();
    private static int _watchRole = -1;
    private static int _watchDifficulty;
    private static int _watchCount;

    public static bool IsHeadless => FikaInfo.IsHeadless;

    public static bool AcceptsGameMaster => FikaInfo.IsHeadless && !string.IsNullOrEmpty(Plugin.GameMasterPassword.Value);

    public static bool CheckPassword(string password)
    {
        string expected = Plugin.GameMasterPassword.Value;
        if (string.IsNullOrEmpty(expected) || password == null)
        {
            return false;
        }

        using SHA256 sha = SHA256.Create();
        byte[] a = sha.ComputeHash(Encoding.UTF8.GetBytes(expected));
        byte[] b = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        int diff = 0;
        for (int i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }
        return diff == 0;
    }

    public static string NameOf(string profileId)
    {
        Player player = Find(profileId);
        return player != null && player.Profile != null ? player.Profile.Nickname : profileId;
    }

    public static void Protect(string profileId)
    {
        Player player = Find(profileId);
        if (player != null)
        {
            AiIgnore.Enable(player);
        }
    }

    public static void Unprotect()
    {
        AiIgnore.Disable(true);
        _watchRole = -1;
        if (WorldPause.Paused)
        {
            WorldPause.Set(false, true);
        }
    }

    public static void Spawn(int role, int difficulty, int count, Vector3 position, float yaw)
    {
        WildSpawnType type = (WildSpawnType)role;
        SpawnService.TrySpawn(type, SpawnService.AllowedDifficulty(type, (BotDifficulty)difficulty), count, position, yaw);
    }

    public static void Watch(int role, int difficulty, int count)
    {
        _watchRole = role;
        _watchDifficulty = (int)SpawnService.AllowedDifficulty((WildSpawnType)role, (BotDifficulty)difficulty);
        _watchCount = count;
        SpawnService.Prewarm((WildSpawnType)_watchRole, (BotDifficulty)_watchDifficulty, _watchCount);
    }

    public static int Order(PerunOrderKind kind, string[] profileIds, Vector3 point, bool run, bool forced)
    {
        Buffer.Clear();
        if (profileIds != null)
        {
            foreach (string id in profileIds)
            {
                Player player = Find(id);
                if (player != null && !Units.Bots.IsProtected(player) && (player.IsAI || kind == PerunOrderKind.Heal || kind == PerunOrderKind.Teleport))
                {
                    Buffer.Add(player);
                }
            }
        }

        if (Buffer.Count == 0)
        {
            return 0;
        }

        List<Player> units = new(Buffer);
        switch (kind)
        {
            case PerunOrderKind.Move:
                return OrderService.Move(units, point, run, forced);
            case PerunOrderKind.Halt:
                return OrderService.Halt(units);
            case PerunOrderKind.Heal:
                return Healing.Heal(units);
            case PerunOrderKind.Teleport:
                return Teleporting.Teleport(units, point);
            case PerunOrderKind.Kill:
                units.ForEach(SpawnService.Kill);
                return units.Count;
            case PerunOrderKind.Remove:
                units.ForEach(SpawnService.Despawn);
                return units.Count;
            default:
                return 0;
        }
    }

    public static void Pause(bool paused) => WorldPause.Set(paused, true);

    public static void ApplyWeather(PerunWeather weather) => PerunNetwork.ApplyWeatherFromHost(weather);

    public static void ApplyTime(DateTime time, float speed)
    {
        if (WorldEnvironment.ApplyTime(time, speed))
        {
            WorldEnvironment.AnnounceTime();
        }
    }

    public static PerunStatus BuildStatus()
    {
        PerunStatus s = Status;
        s.LastResult = SpawnService.LastResult ?? "";
        s.Pending = SpawnService.Pending;
        s.Alive = UnitRegistry.AliveCount;
        s.Max = Plugin.MaxUnits.Value;

        s.WatchRole = _watchRole;
        s.WatchDifficulty = _watchDifficulty;
        if (_watchRole >= 0)
        {
            WildSpawnType role = (WildSpawnType)_watchRole;
            BotDifficulty difficulty = (BotDifficulty)_watchDifficulty;
            SpawnService.Prewarm(role, difficulty, _watchCount);
            s.ReadyCount = SpawnService.ReadyCount(role, difficulty);
            s.Warming = SpawnService.IsWarming(role, difficulty);
        }
        else
        {
            s.ReadyCount = 0;
            s.Warming = false;
        }

        foreach (PerunJobInfo info in s.Jobs)
        {
            SpareJobs.Push(info);
        }
        s.Jobs.Clear();
        foreach (LoadJob job in SpawnService.Jobs)
        {
            PerunJobInfo info = SpareJobs.Count > 0 ? SpareJobs.Pop() : new PerunJobInfo();
            info.Title = job.Title;
            info.IsSpawn = job.IsSpawn;
            info.Stage = (byte)job.Stage;
            info.Progress = job.Progress;
            info.Created = job.Created;
            info.Total = job.Total;
            info.Elapsed = job.Elapsed;
            s.Jobs.Add(info);
        }

        s.OurUnits.Clear();
        UnitRegistry.CopyIds(s.OurUnits);
        s.Orders.Clear();
        OrderService.CopyTo(s.Orders);
        return s;
    }

    private static Player Find(string profileId)
    {
        if (string.IsNullOrEmpty(profileId) || !Singleton<GameWorld>.Instantiated)
        {
            return null;
        }
        return Singleton<GameWorld>.Instance.GetAlivePlayerByProfileID(profileId);
    }
}
