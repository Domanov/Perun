using System.Collections.Generic;
using EFT;
using UnityEngine;

namespace Perun.Client.Units;

internal sealed class RemoteSession : IRemoteSession
{
    private const float WatchRefresh = 2f;

    private sealed class RemoteJob : IJobView
    {
        public string Title { get; set; }
        public bool IsSpawn { get; set; }
        public LoadStage Stage { get; set; }
        public float Progress { get; set; }
        public int Created { get; set; }
        public int Total { get; set; }
        public float BaseElapsed;
        public float ReceivedAt;
        public float Elapsed => BaseElapsed + (Time.realtimeSinceStartup - ReceivedAt);
    }

    private readonly IGameMasterLink _link;
    private readonly List<IJobView> _jobs = new();
    private readonly List<RemoteJob> _spare = new();
    private readonly HashSet<string> _ours = new();
    private readonly Dictionary<string, PerunOrderInfo> _orders = new();
    private int _watchRole = -1;
    private int _watchDifficulty;
    private int _watchCount;
    private float _nextWatchAt;
    private int _statusRole = -1;
    private int _statusDifficulty;
    private int _ready;
    private bool _warming;

    public RemoteSession(IGameMasterLink link)
    {
        _link = link;
    }

    public int Pending { get; private set; }
    public string LastResult { get; private set; } = "";
    public int AliveCount { get; private set; }
    public int MaxUnits { get; private set; } = Plugin.MaxUnits.Value;
    public IReadOnlyList<IJobView> Jobs => _jobs;

    public void Apply(PerunStatus status)
    {
        Pending = status.Pending;
        LastResult = status.LastResult ?? "";
        AliveCount = status.Alive;
        MaxUnits = status.Max;
        _statusRole = status.WatchRole;
        _statusDifficulty = status.WatchDifficulty;
        _ready = status.ReadyCount;
        _warming = status.Warming;

        foreach (IJobView job in _jobs)
        {
            _spare.Add((RemoteJob)job);
        }
        _jobs.Clear();
        float now = Time.realtimeSinceStartup;
        foreach (PerunJobInfo info in status.Jobs)
        {
            RemoteJob job;
            if (_spare.Count > 0)
            {
                job = _spare[_spare.Count - 1];
                _spare.RemoveAt(_spare.Count - 1);
            }
            else
            {
                job = new RemoteJob();
            }

            job.Title = info.Title;
            job.IsSpawn = info.IsSpawn;
            job.Stage = (LoadStage)info.Stage;
            job.Progress = info.Progress;
            job.Created = info.Created;
            job.Total = info.Total;
            job.BaseElapsed = info.Elapsed;
            job.ReceivedAt = now;
            _jobs.Add(job);
        }

        _ours.Clear();
        foreach (string id in status.OurUnits)
        {
            _ours.Add(id);
        }

        _orders.Clear();
        foreach (PerunOrderInfo order in status.Orders)
        {
            if (order.ProfileId != null)
            {
                _orders[order.ProfileId] = order;
            }
        }
    }

    public int ReadyCount(WildSpawnType role, BotDifficulty difficulty) =>
        _statusRole == (int)role && _statusDifficulty == (int)SpawnService.AllowedDifficulty(role, difficulty) ? _ready : 0;

    public bool IsWarming(WildSpawnType role, BotDifficulty difficulty) =>
        _statusRole == (int)role && _statusDifficulty == (int)SpawnService.AllowedDifficulty(role, difficulty) && _warming;

    public void Prewarm(WildSpawnType role, BotDifficulty difficulty, int count)
    {
        float now = Time.realtimeSinceStartup;
        if (_watchRole == (int)role && _watchDifficulty == (int)difficulty && _watchCount == count && now < _nextWatchAt)
        {
            return;
        }

        _watchRole = (int)role;
        _watchDifficulty = (int)difficulty;
        _watchCount = count;
        _nextWatchAt = now + WatchRefresh;
        _link.Watch(_watchRole, _watchDifficulty, count);
    }

    public void Spawn(WildSpawnType role, BotDifficulty difficulty, int count, Vector3 position, float yaw)
    {
        _link.Spawn((int)role, (int)difficulty, count, position, yaw);
        LastResult = $"Asked the host for {SpawnNames.Of(role)} ×{count}…";
    }

    public int Move(IReadOnlyList<Player> units, Vector3 point, bool run, bool forced)
    {
        string[] ids = Ids(units);
        if (ids.Length > 0)
        {
            _link.Order(PerunOrderKind.Move, ids, point, run, forced);
        }
        return ids.Length;
    }

    public int Halt(IReadOnlyList<Player> units)
    {
        string[] ids = Ids(units);
        if (ids.Length > 0)
        {
            _link.Order(PerunOrderKind.Halt, ids, Vector3.zero, false, false);
            foreach (string id in ids)
            {
                _orders.Remove(id);
            }
        }
        return ids.Length;
    }

    public void Remove(IReadOnlyList<Player> units, bool kill)
    {
        string[] ids = Ids(units);
        if (ids.Length > 0)
        {
            _link.Order(kill ? PerunOrderKind.Kill : PerunOrderKind.Remove, ids, Vector3.zero, false, false);
        }
    }

    public int Heal(IReadOnlyList<Player> units)
    {
        string[] ids = Ids(units, false);
        if (ids.Length > 0)
        {
            _link.Order(PerunOrderKind.Heal, ids, Vector3.zero, false, false);
        }
        return ids.Length;
    }

    public int Teleport(IReadOnlyList<Player> units, Vector3 point)
    {
        string[] ids = Ids(units, false);
        if (ids.Length > 0)
        {
            _link.Order(PerunOrderKind.Teleport, ids, point, false, false);
        }
        return ids.Length;
    }

    public void SetPaused(bool paused) => _link.Pause(paused);

    public bool IsOurs(Player player) => player != null && player.ProfileId != null && _ours.Contains(player.ProfileId);

    public bool TryGetOrder(Player player, out Vector3 destination, out bool run, out bool forced)
    {
        if (player != null && player.ProfileId != null && _orders.TryGetValue(player.ProfileId, out PerunOrderInfo order))
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

    private static string[] Ids(IReadOnlyList<Player> units, bool botsOnly = true)
    {
        List<string> ids = new(units.Count);
        for (int i = 0; i < units.Count; i++)
        {
            Player unit = units[i];
            if (unit != null && !unit.IsYourPlayer && unit.ProfileId != null && (!botsOnly || Bots.IsBot(unit)))
            {
                ids.Add(unit.ProfileId);
            }
        }
        return ids.ToArray();
    }
}
