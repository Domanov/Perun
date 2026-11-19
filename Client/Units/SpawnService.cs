using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using HarmonyLib;
using EFT.Game.Spawning;
using Diz.Jobs;
using Perun.Client.Mode;
using UnityEngine;
using UnityEngine.AI;

namespace Perun.Client.Units;

internal static class SpawnService
{
    private const float LoadingSpeed = 3f;

    private const float GroupSpacing = 1.3f;

    public static int Pending { get; private set; }
    public static string LastResult { get; private set; } = "";

    private static readonly List<LoadJob> ActiveJobs = new();

    public static IReadOnlyList<LoadJob> Jobs => ActiveJobs;

    private static readonly Dictionary<(WildSpawnType, BotDifficulty), BotCreationData> Ready = new();
    private static readonly RaidBook<(WildSpawnType, BotDifficulty)> Book = new();
    private static readonly Dictionary<(WildSpawnType, BotDifficulty), float> RetryAfter = new();
    private static GameWorld _world;
    private static JobScheduler _scheduler;
    private static bool _forcedByUs;

    private static RaidBook<(WildSpawnType, BotDifficulty)>.Lease BeginBusy((WildSpawnType, BotDifficulty)? warm = null)
    {
        RaidBook<(WildSpawnType, BotDifficulty)>.Lease lease = warm.HasValue ? Book.BeginWarm(warm.Value) : Book.Begin();
        if (Book.BusyCount != 1)
        {
            return lease;
        }

        if (_scheduler == null)
        {
            _scheduler = UnityEngine.Object.FindObjectOfType<JobScheduler>();
        }

        if (_scheduler != null && !_scheduler.IsForceModeEnabled)
        {
            _scheduler.SetForceMode(true, LoadingSpeed);
            _forcedByUs = true;
        }
        return lease;
    }

    private static void EndBusy(RaidBook<(WildSpawnType, BotDifficulty)>.Lease lease)
    {
        if (Book.End(lease) && !Book.Busy && _forcedByUs)
        {
            _forcedByUs = false;
            if (_scheduler != null)
            {
                _scheduler.SetForceMode(false);
            }
        }
    }

    public static int ReadyCount(WildSpawnType role, BotDifficulty difficulty)
    {
        CheckWorld();
        return Ready.TryGetValue((role, difficulty), out BotCreationData data) && data != null ? data.Count : 0;
    }

    public static bool IsWarming(WildSpawnType role, BotDifficulty difficulty) => Book.IsWarming((role, difficulty));

    public static void Prewarm(WildSpawnType role, BotDifficulty difficulty, int count)
    {
        CheckWorld();
        difficulty = AllowedDifficulty(role, difficulty);
        if (!Plugin.PrewarmProfiles.Value || _world == null || !Singleton<IBotGame>.Instantiated)
        {
            return;
        }

        var key = (role, difficulty);
        int have = ReadyCount(role, difficulty);
        if (Book.WarmingCount > 0 || have >= count || (RetryAfter.TryGetValue(key, out float retry) && Time.time < retry))
        {
            return;
        }

        _ = WarmAsync(key, count - have);
    }

    private static async Task WarmAsync((WildSpawnType role, BotDifficulty difficulty) key, int count)
    {
        GameWorld world = _world;
        LoadJob job = new($"Preparing {Units.SpawnNames.Of(key.role)} ×{count}", false, count);
        ActiveJobs.Add(job);
        var lease = BeginBusy(key);
        try
        {
            BotCreationData data = await CreateData(key.role, key.difficulty, count, job);
            if (world != _world || !Book.Owns(lease))
            {
                return;
            }

            if (data == null || data.Count < count)
            {
                RetryAfter[key] = Time.time + 20f;
            }

            if (data == null || data.Count == 0)
            {
                return;
            }

            if (Ready.TryGetValue(key, out BotCreationData existing) && existing != null)
            {
                existing.AddProfiles(new List<Profile>(data.Profiles));
            }
            else
            {
                Ready[key] = data;
            }

            Plugin.Log.LogInfo($"Prepared {key.role} ({key.difficulty}): {ReadyCount(key.role, key.difficulty)} ready");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Prewarm {key.role} failed: {ex.Message}");
        }
        finally
        {
            ActiveJobs.Remove(job);
            EndBusy(lease);
        }
    }

    private static BotCreationData TakeReady(WildSpawnType role, BotDifficulty difficulty, int count)
    {
        if (!Ready.TryGetValue((role, difficulty), out BotCreationData data) || data == null || data.Count == 0)
        {
            return null;
        }

        if (data.Count <= count)
        {
            Ready.Remove((role, difficulty));
            return data;
        }

        return data.Separate(count);
    }

    public static async Task<List<Profile>> FetchProfiles(WildSpawnType role, int count)
    {
        List<Profile> result = new();
        if (!Singleton<IBotGame>.Instantiated || count <= 0)
        {
            return result;
        }
        BotSpawner spawner = Singleton<IBotGame>.Instance.BotsController?.BotSpawner;
        BotProfileClient profiles = (spawner?._botCreator as BotCreatorClient)?._creatorProfile as BotProfileClient;
        if (profiles?._backEndSession == null)
        {
            return result;
        }
        BotDifficulty difficulty = AllowedDifficulty(role, BotDifficulty.normal);
        Profile[] loaded = await profiles._backEndSession.LoadBots(new List<CountTypeBotWave> { new(count, role, difficulty) });
        if (loaded != null)
        {
            foreach (Profile profile in loaded)
            {
                if (profile?.Inventory?.Equipment != null)
                {
                    result.Add(profile);
                }
            }
        }
        return result;
    }

    private static async Task<BotCreationData> CreateData(WildSpawnType role, BotDifficulty difficulty, int count, LoadJob job)
    {
        BotSpawner spawner = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
        PerunProfileData profileData = new(new GetProfileDataParams(SideFor(role), role, difficulty, 0f, new BotSpawnParams()));

        BotProfileClient profiles = (spawner._botCreator as BotCreatorClient)?._creatorProfile as BotProfileClient;
        if (profiles?._backEndSession == null)
        {
            try
            {
                job.Enter(LoadStage.Server);
                return await BotCreationData.Create(profileData, spawner._botCreator, count, spawner);
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }

        Stopwatch clock = Stopwatch.StartNew();
        List<Profile> chosen = new();
        while (chosen.Count < count)
        {
            Profile cached = profileData.ChooseProfile(profiles._simpleProfiles, true);
            if (cached == null)
            {
                break;
            }
            chosen.Add(cached);
        }
        int fromCache = chosen.Count;

        int missing = count - chosen.Count;
        if (missing > 0)
        {
            Plugin.Log.LogInfo($"Profiles {role} ×{missing}: asking the server");
            job.Enter(LoadStage.Server);
            Profile[] loaded = await profiles._backEndSession.LoadBots(new List<CountTypeBotWave> { new(missing, role, difficulty) });
            if (loaded != null)
            {
                foreach (Profile profile in loaded)
                {
                    if (profile == null)
                    {
                        continue;
                    }

                    if (chosen.Count < count && profile.Info.Settings.Role == role)
                    {
                        chosen.Add(profile);
                    }
                    else
                    {
                        profiles._simpleProfiles.Add(profile);
                    }
                }
            }
        }
        long serverMs = clock.ElapsedMilliseconds;
        if (missing > 0)
        {
            Plugin.Log.LogInfo($"Profiles {role}: server answered in {serverMs} ms, loading gear");
        }

        if (chosen.Count == 0)
        {
            return null;
        }

        List<ResourceKey> bundles = new();
        foreach (Profile profile in chosen)
        {
            bundles.AddRange(profile.GetAllPrefabPaths(false));
        }
        job.Enter(LoadStage.Gear);
        await Singleton<ObjectsFactory>.Instance.LoadBundlesAndCreatePools(ObjectsFactory.PoolsCategory.Raid, ObjectsFactory.AssemblyType.Local, bundles.ToArray(), JobYieldPriority.Immediate, job, ObjectsFactory.DefaultCancellationToken);
        long bundleMs = clock.ElapsedMilliseconds - serverMs;

        foreach (Profile profile in chosen)
        {
            BotsGlobalStaistics.UseProfile(profile);
        }

        BotCreationData data = new(profileData)
        {
            _botCreator = spawner._botCreator,
            _tokenGetter = spawner
        };
        data.AddProfiles(chosen);

        Plugin.Log.LogInfo($"Profiles {role} ×{chosen.Count}: {fromCache} from the raid cache, {chosen.Count - fromCache} from the server in {serverMs} ms, gear loaded in {bundleMs} ms");
        return data;
    }

    private static void CheckWorld()
    {
        GameWorld world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
        if (world != _world)
        {
            _world = world;
            Ready.Clear();
            Book.Reset();
            RetryAfter.Clear();
            ActiveJobs.Clear();
            if (_forcedByUs && _scheduler != null)
            {
                _scheduler.SetForceMode(false);
            }
            _forcedByUs = false;
            _scheduler = null;
        }
    }

    public static bool TrySpawn(WildSpawnType role, BotDifficulty difficulty, int count, Vector3 position, float yaw)
    {
        if (!Singleton<IBotGame>.Instantiated || Singleton<IBotGame>.Instance.BotsController?.BotSpawner == null)
        {
            LastResult = "No bot system in this raid";
            return false;
        }

        int budget = Plugin.MaxUnits.Value - UnitRegistry.AliveCount - Pending;
        if (budget <= 0)
        {
            LastResult = $"Unit limit reached ({Plugin.MaxUnits.Value})";
            return false;
        }

        count = Mathf.Clamp(count, 1, budget);
        difficulty = AllowedDifficulty(role, difficulty);
        CheckWorld();
        _ = SpawnAsync(role, difficulty, count, position, yaw);
        return true;
    }

    private static async Task SpawnAsync(WildSpawnType role, BotDifficulty difficulty, int count, Vector3 position, float yaw)
    {
        Pending += count;
        string label = $"{role} ×{count}";
        LoadJob job = new($"Spawning {Units.SpawnNames.Of(role)} ×{count}", true, count);
        ActiveJobs.Add(job);
        LastResult = $"Spawning {label}…";
        Stopwatch clock = Stopwatch.StartNew();
        Plugin.Log.LogInfo($"Spawn {label}: requested ({ReadyCount(role, difficulty)} prepared)");
        var lease = BeginBusy();
        GameWorld world = _world;
        CancellationToken raid = RaidScope.Token;
        bool Stale() => raid.IsCancellationRequested || !ReferenceEquals(world, Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null);

        try
        {
            BotsController controller = Singleton<IBotGame>.Instance.BotsController;
            BotSpawner spawner = controller.BotSpawner;

            BotZone zone = spawner.GetClosestZone(position, out _);
            if (zone == null)
            {
                LastResult = "No bot zone on this map";
                return;
            }

            BotCreationData data = TakeReady(role, difficulty, count);
            int fromCache = data?.Count ?? 0;
            int missing = count - (data?.Count ?? 0);
            if (missing > 0)
            {
                LastResult = $"Loading {role} ×{missing} from the server…";
                BotCreationData extra = await CreateData(role, difficulty, missing, job);
                if (Stale())
                {
                    Plugin.Log.LogInfo($"Spawn {label}: dropped, the raid ended while profiles were loading");
                    return;
                }
                if (extra != null && extra.Count > 0)
                {
                    if (data == null)
                    {
                        data = extra;
                    }
                    else
                    {
                        data.AddProfiles(new List<Profile>(extra.Profiles));
                    }
                }
            }

            if (data == null || data.Count == 0)
            {
                LastResult = $"The server sent no {role} profile";
                Plugin.Log.LogWarning($"Spawn {label}: the server returned no matching profile");
                return;
            }

            data.SpawnParams.ShallBeGroup = data.Count > 1 ? new ShallBeGroupParams(true, role.IsBoss(), data.Count) : null;

            int corePointId = NearestCorePoint(controller, position);
            List<ISpawnPoint> points = new();
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i < data.Count; i++)
            {
                points.Add(new SpawnPoint
                {
                    Id = $"perun_{Guid.NewGuid():N}",
                    Name = "Perun",
                    Position = Slot(position, i),
                    Rotation = rotation,
                    Sides = EPlayerSideMask.All,
                    Categories = ESpawnCategoryMask.All,
                    BotZone = zone,
                    CorePointId = corePointId,
                    DelayToCanSpawnSec = 0f
                });
            }

            foreach (ISpawnPoint point in points)
            {
                data.AddPosition(point.Position, point.CorePointId);
            }

            bool shallBeGroup = data.SpawnParams?.ShallBeGroup != null && data.SpawnParams.ShallBeGroup.Group && data.SpawnParams.ShallBeGroup.RemainCount > 0;
            if (shallBeGroup)
            {
                data.SpawnParams.ShallBeGroup.DescreaseCount();
            }

            float profilesAt = clock.ElapsedMilliseconds / 1000f;
            Plugin.Log.LogInfo($"Spawn {label}: {data.Count} profile(s) ready after {profilesAt:F1} s{(fromCache > 0 ? $" ({fromCache} prepared)" : "")}, creating at {position}");
            LastResult = $"Creating {label}…";
            job.Enter(LoadStage.Creating);

            int total = data.Count;
            int spawned = 0;
            spawner._inSpawnProcess += total;
            await spawner._botCreator.ActivateBot(data, zone, shallBeGroup, spawner.GetGroupAndSetEnemies, bot =>
            {
                if (bot == null || Stale())
                {
                    return;
                }
                try
                {
                    spawner.ActivateBotCallback(bot, data, null, shallBeGroup, new Stopwatch());
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Spawn {label}: activating {(bot != null ? bot.Profile?.Nickname : null)} failed: {ex}");
                }

                UnitRegistry.Add(bot);
                spawned++;
                job.Spawned();
                LastResult = $"Spawned {role} {spawned}/{total}";
                Plugin.Log.LogInfo($"Spawn {label}: {bot.Profile.Nickname} ({bot.Profile.Info.Settings.Role}) in after {clock.ElapsedMilliseconds / 1000f:F1} s");
            }, spawner.GetCancelToken());

            if (Stale())
            {
                Plugin.Log.LogInfo($"Spawn {label}: dropped, the raid ended while bots were being created");
                return;
            }
            if (spawned < total)
            {
                LastResult = $"Only {spawned}/{total} {role} spawned (see log)";
                Plugin.Log.LogWarning($"Spawn {label}: only {spawned}/{total} created");
            }
        }
        catch (Exception ex)
        {
            LastResult = $"Spawn failed: {ex.Message}";
            Plugin.Log.LogError($"Spawn {label} failed: {ex}");
        }
        finally
        {
            Pending = Math.Max(0, Pending - count);
            ActiveJobs.Remove(job);
            EndBusy(lease);
            if (!Stale())
            {
                Prewarm(role, difficulty, count);
            }
        }
    }

    public static void Despawn(Player player)
    {
        BotOwner bot = player != null ? player.AIData?.BotOwner : null;
        if (bot == null || bot.LeaveData == null)
        {
            LastResult = "Only bots can be removed";
            return;
        }

        bot.LeaveData.RemoveFromMap();
        LastResult = $"Removed {player.Profile.Nickname}";
    }

    private static readonly AccessTools.FieldRef<Player, IPlayer> LastAggressor = AccessTools.FieldRefAccess<Player, IPlayer>("LastAggressor");

    public static void Kill(Player player)
    {
        if (player == null || player.IsYourPlayer || player.ActiveHealthController == null)
        {
            return;
        }

        string hands = player.HandsController?.GetType().Name ?? "none";
        // Fika's corpse sync turns the killer's profile id into a MongoID and throws without one, which left the bot
        // dead but frozen. A kill from the game master has no killer, so the bot is its own.
        LastAggressor(player) ??= player;
        player.ActiveHealthController.Kill(EDamageType.Undefined);
        LastResult = $"Killed {player.Profile.Nickname}";
        Plugin.Log.LogInfo($"Killed {player.Profile.Nickname} (hands: {hands})");
    }

    private static readonly BotDifficulty[] DifficultyPreference = { BotDifficulty.normal, BotDifficulty.hard, BotDifficulty.easy, BotDifficulty.impossible };

    public static BotDifficulty AllowedDifficulty(WildSpawnType role, BotDifficulty wanted)
    {
        var excluded = BotInternalSettingsController.ExcludedDifficulties;
        if (excluded == null || !excluded.TryGetValue(role, out List<BotDifficulty> blocked) || blocked == null || !blocked.Contains(wanted))
        {
            return wanted;
        }

        foreach (BotDifficulty difficulty in DifficultyPreference)
        {
            if (!blocked.Contains(difficulty))
            {
                return difficulty;
            }
        }
        return wanted;
    }

    private static EPlayerSide SideFor(WildSpawnType role)
    {
        return role switch
        {
            WildSpawnType.pmcUSEC => EPlayerSide.Usec,
            WildSpawnType.pmcBEAR => EPlayerSide.Bear,
            _ => EPlayerSide.Savage
        };
    }

    private static Vector3 Slot(Vector3 center, int index)
    {
        if (index == 0)
        {
            return center;
        }

        Vector3 candidate = center + Formation.Offset(index, GroupSpacing);
        return NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) ? hit.position : center;
    }

    private static int NearestCorePoint(BotsController controller, Vector3 position)
    {
        var points = controller.CoversData?.AICorePointsHolder?.CorePoints;
        if (points == null || points.Count == 0)
        {
            return 0;
        }

        int best = points[0].Id;
        float bestDistance = float.MaxValue;
        foreach (AICorePoint point in points)
        {
            float distance = (point.Position - position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = point.Id;
            }
        }
        return best;
    }
}
