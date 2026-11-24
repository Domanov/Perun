using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using Perun.Client.Mode;
using UnityEngine;
using UnityEngine.AI;

namespace Perun.Client.Units;

internal static partial class SpawnService
{
    private static readonly System.Random GroupRandom = new();

    public const float DefaultGroupSpacing = 1.3f;
    public const float MinGroupSpacing = 0.8f;
    public const float MaxGroupSpacing = 4f;

    public static bool TrySpawnGroup(GroupPlan plan, BotDifficulty? difficulty, Vector3 position, float yaw, float spacing)
    {
        if (plan == null || plan.Members.Count == 0)
        {
            return false;
        }
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

        CheckWorld();
        List<(WildSpawnType role, BotDifficulty difficulty, int count)> parts = plan.Roll(GroupRandom, budget, difficulty)
            .Select(p => (p.role, AllowedDifficulty(p.role, p.difficulty), p.count))
            .ToList();
        _ = SpawnGroupAsync(plan.Name, parts, position, Formation.Heading(yaw), Mathf.Clamp(spacing, MinGroupSpacing, MaxGroupSpacing));
        return true;
    }

    public static void PrewarmGroup(GroupPlan plan, BotDifficulty? difficulty)
    {
        if (plan == null)
        {
            return;
        }
        foreach (GroupMember member in plan.Members)
        {
            BotDifficulty wanted = AllowedDifficulty(member.Role, difficulty ?? member.Difficulty);
            if (ReadyCount(member.Role, wanted) < member.Max)
            {
                Prewarm(member.Role, wanted, member.Max);
                return;
            }
        }
    }

    public static int GroupReadyCount(GroupPlan plan, BotDifficulty? difficulty)
    {
        if (plan == null)
        {
            return 0;
        }
        int ready = 0;
        foreach (var byRole in plan.Members.GroupBy(m => (m.Role, AllowedDifficulty(m.Role, difficulty ?? m.Difficulty))))
        {
            ready += Math.Min(ReadyCount(byRole.Key.Item1, byRole.Key.Item2), byRole.Sum(m => m.Max));
        }
        return ready;
    }

    private static async Task SpawnGroupAsync(string name, List<(WildSpawnType role, BotDifficulty difficulty, int count)> parts, Vector3 position, Vector3 heading, float spacing)
    {
        int total = parts.Sum(p => p.count);
        Pending += total;
        string label = $"{name} ({total})";
        LoadJob job = new($"Spawning {label}", true, total);
        ActiveJobs.Add(job);
        LastResult = $"Spawning {label}…";
        Stopwatch clock = Stopwatch.StartNew();
        Plugin.Log.LogInfo($"Group {label}: requested {string.Join(", ", parts.Select(p => $"{p.role} ×{p.count} ({p.difficulty})"))}");
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

            List<BotCreationData> datas = new();
            foreach (var part in parts)
            {
                BotCreationData data = TakeReady(part.role, part.difficulty, part.count);
                int missing = part.count - (data?.Count ?? 0);
                if (missing > 0)
                {
                    LastResult = $"Loading {part.role} ×{missing} from the server…";
                    BotCreationData extra = await CreateData(part.role, part.difficulty, missing, job);
                    if (Stale())
                    {
                        Plugin.Log.LogInfo($"Group {label}: dropped, the raid ended while profiles were loading");
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
                    if (datas.Count == 0)
                    {
                        LastResult = $"The server sent no {part.role} profile for the leader";
                        Plugin.Log.LogWarning($"Group {label}: no leader profile ({part.role})");
                        return;
                    }
                    Plugin.Log.LogWarning($"Group {label}: no {part.role} profile, the group spawns without them");
                    continue;
                }
                datas.Add(data);
            }

            BotCreationData leaderData = datas[0].Count > 1 ? datas[0].Separate(1) : datas[0];
            List<BotCreationData> memberDatas = new();
            if (!ReferenceEquals(leaderData, datas[0]))
            {
                memberDatas.Add(datas[0]);
            }
            memberDatas.AddRange(datas.Skip(1));
            int followers = memberDatas.Sum(d => d.Count);

            int corePointId = NearestCorePoint(controller, position);
            int slot = 0;
            leaderData.AddPosition(WedgeSlot(position, slot++, heading, spacing), corePointId);
            foreach (BotCreationData data in memberDatas)
            {
                for (int i = 0; i < data.Count; i++)
                {
                    data.AddPosition(WedgeSlot(position, slot++, heading, spacing), corePointId);
                }
            }

            leaderData.SpawnParams.ShallBeGroup = new ShallBeGroupParams(true, true, followers);
            Plugin.Log.LogInfo($"Group {label}: profiles ready after {clock.ElapsedMilliseconds / 1000f:F1} s, creating at {position}");
            LastResult = $"Creating {label}…";
            job.Enter(LoadStage.Creating);

            int spawned = 0;
            BotsGroup group = null;
            BotOwner leader = null;
            CancellationToken token = spawner.GetCancelToken();

            spawner._inSpawnProcess += 1;
            await spawner._botCreator.ActivateBot(leaderData, zone, false, (bot, botZone) => group = NewGroup(spawner, bot, botZone, followers + 1), bot =>
            {
                if (bot == null || Stale())
                {
                    return;
                }
                try
                {
                    spawner.ActivateBotCallback(bot, leaderData, null, true, new Stopwatch());
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Group {label}: activating the leader {bot.Profile?.Nickname} failed: {ex}");
                }
                leader = bot;
                UnitRegistry.Add(bot);
                spawned++;
                job.Spawned();
                Plugin.Log.LogInfo($"Group {label}: leader {bot.Profile.Nickname} ({bot.Profile.Info.Settings.Role}) in after {clock.ElapsedMilliseconds / 1000f:F1} s");
            }, token);

            if (Stale())
            {
                return;
            }
            if (leader == null || group == null)
            {
                LastResult = $"{name}: the leader didn't spawn (see log)";
                Plugin.Log.LogWarning($"Group {label}: the leader was not created");
                return;
            }

            GroupLeadership.Handle leadership = GroupLeadership.Track(leader, name);

            foreach (BotCreationData data in memberDatas)
            {
                BotCreationData members = data;
                spawner._inSpawnProcess += members.Count;
                await spawner._botCreator.ActivateBot(members, zone, false, (bot, _) =>
                {
                    spawner.SetBotAsEnemy(bot);
                    return group;
                }, bot =>
                {
                    if (bot == null || Stale())
                    {
                        return;
                    }
                    try
                    {
                        spawner.ActivateBotCallback(bot, members, null, false, new Stopwatch());
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogError($"Group {label}: activating {bot.Profile?.Nickname} failed: {ex}");
                    }
                    GroupLeadership.QueueAttach(leadership, bot);
                    UnitRegistry.Add(bot);
                    spawned++;
                    job.Spawned();
                    LastResult = $"Spawned {name} {spawned}/{total}";
                    Plugin.Log.LogInfo($"Group {label}: {bot.Profile.Nickname} ({bot.Profile.Info.Settings.Role}) in after {clock.ElapsedMilliseconds / 1000f:F1} s");
                }, token);
                if (Stale())
                {
                    return;
                }
            }

            LastResult = spawned < total ? $"Only {spawned}/{total} of {name} spawned (see log)" : $"Spawned {name} ({spawned})";
            Plugin.Log.LogInfo($"Group {label}: {spawned}/{total} in, leader {leader.Profile.Nickname}; followers attach once each bot is active");
        }
        catch (Exception ex)
        {
            LastResult = $"Group spawn failed: {ex.Message}";
            Plugin.Log.LogError($"Group {label} failed: {ex}");
        }
        finally
        {
            Pending = Math.Max(0, Pending - total);
            ActiveJobs.Remove(job);
            EndBusy(lease);
        }
    }

    private static Vector3 WedgeSlot(Vector3 point, int index, Vector3 heading, float spacing)
    {
        if (index == 0)
        {
            return point;
        }
        Vector3 candidate = point + Formation.Wedge(index, spacing, heading);
        return NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) ? hit.position : point;
    }

    // A bot taken out of its group in the link view gets a group of its own, set up like a fresh spawn's.
    public static BotsGroup LoneGroup(BotOwner bot)
    {
        BotSpawner spawner = Singleton<IBotGame>.Instantiated ? Singleton<IBotGame>.Instance.BotsController?.BotSpawner : null;
        BotZone zone = bot.BotsGroup?.BotZone;
        return spawner != null && zone != null ? NewGroup(spawner, bot, zone, 1) : null;
    }

    // Mirrors the boss branch of BotSpawner.GetGroupAndSetEnemies, but always makes a fresh group so a Perun group
    // never merges into a map group of the same side and role.
    private static BotsGroup NewGroup(BotSpawner spawner, BotOwner bot, BotZone zone, int members)
    {
        List<BotOwner> enemies = new(spawner.GetBotEnemiesList(bot));
        spawner.SetBotAsEnemy(bot);
        BotsGroup group = new(zone, spawner.BotGame, bot, enemies, spawner._deadBodiesController, spawner._allPlayers, true)
        {
            TargetMembersCount = members
        };
        spawner.Groups.Add(zone, bot.Profile.Info.Side, group, true);
        return group;
    }
}
