using System;
using System.Collections.Generic;
using System.Linq;
using EFT;

namespace Perun.Client.Units;

internal static class BossPresets
{
    private static readonly HashSet<WildSpawnType> Skipped = new()
    {
        WildSpawnType.pmcUSEC,
        WildSpawnType.pmcBEAR,
        WildSpawnType.gifter
    };

    private static readonly Dictionary<WildSpawnType, string> Names = new()
    {
        { WildSpawnType.bossBully, "Reshala" },
        { WildSpawnType.bossKojaniy, "Shturman" },
        { WildSpawnType.bossGluhar, "Glukhar" },
        { WildSpawnType.bossSanitar, "Sanitar" },
        { WildSpawnType.bossKilla, "Killa" },
        { WildSpawnType.bossTagilla, "Tagilla" },
        { WildSpawnType.bossKnight, "Goons" },
        { WildSpawnType.bossZryachiy, "Zryachiy" },
        { WildSpawnType.bossBoar, "Kaban" },
        { WildSpawnType.bossBoarSniper, "Kaban's snipers" },
        { WildSpawnType.bossKolontay, "Kollontay" },
        { WildSpawnType.bossPartisan, "Partisan" },
        { WildSpawnType.bossKillaAgro, "Killa (Labyrinth)" },
        { WildSpawnType.bossTagillaAgro, "Tagilla (Labyrinth)" },
        { WildSpawnType.tagillaHelperAgro, "Labyrinth shadows" },
        { WildSpawnType.pmcBot, "Raiders" },
        { WildSpawnType.exUsec, "Rogues" },
        { WildSpawnType.sectantPriest, "Cultists" },
        { WildSpawnType.peacemaker, "Peacemakers" },
        { WildSpawnType.crazyAssaultEvent, "Crazy scavs" },
        { WildSpawnType.arenaFighterEvent, "Bloodhounds" }
    };

    private static readonly Dictionary<string, string> MapNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "bigmap", "Customs" },
        { "factory4_day", "Factory" },
        { "factory4_night", "Night Factory" },
        { "interchange", "Interchange" },
        { "laboratory", "Labs" },
        { "lighthouse", "Lighthouse" },
        { "rezervbase", "Reserve" },
        { "shoreline", "Shoreline" },
        { "woods", "Woods" },
        { "tarkovstreets", "Streets" },
        { "sandbox", "Ground Zero" },
        { "sandbox_high", "Ground Zero" },
        { "labyrinth", "Labyrinth" }
    };

    private static List<GroupPlan> _cached;

    // Null until the session has its location settings; the HUD asks every layout pass, so a missing session must stay cheap.
    public static IReadOnlyList<GroupPlan> All => _cached ??= Load();

    public static string MapName(string id) => MapNames.TryGetValue(id ?? "", out string name) ? name : id;

    private static List<GroupPlan> Load()
    {
        try
        {
            var locations = SPT.Reflection.Utils.ClientAppUtils.GetClientApp()?.Session?.LocationSettings?.locations;
            if (locations == null)
            {
                return null;
            }

            List<(string map, BossLocationSpawn wave)> waves = new();
            foreach (var location in locations.Values)
            {
                if (location?.BossLocationSpawn == null)
                {
                    continue;
                }
                foreach (BossLocationSpawn wave in location.BossLocationSpawn)
                {
                    if (wave != null)
                    {
                        waves.Add((location.Id, wave));
                    }
                }
            }

            List<GroupPlan> plans = Build(waves);
            Plugin.Log.LogInfo($"Boss presets: {plans.Count} from {waves.Count} map waves");
            return plans;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Boss presets: could not read the maps' boss waves: {ex.Message}");
            return new List<GroupPlan>();
        }
    }

    // SPT keeps its PMC and gifter waves in BossLocationSpawn too; those aren't boss groups.
    // With supports set, the game spawns the supports and ignores the plain escort.
    public static List<GroupPlan> Build(IEnumerable<(string map, BossLocationSpawn wave)> waves)
    {
        Dictionary<string, Entry> entries = new();
        List<string> order = new();

        foreach (var (map, wave) in waves)
        {
            if (!TryRole(wave.BossName, out WildSpawnType boss) || Skipped.Contains(boss) || PerunRules.IsHiddenRole(boss))
            {
                continue;
            }

            List<(WildSpawnType role, BotDifficulty difficulty, int[] counts)> members = new()
            {
                (boss, Difficulty(wave.BossDifficult), new[] { 1 })
            };

            bool supports = wave.Supports != null && wave.Supports.Any(s => s != null && s.BossEscortAmount > 0);
            if (supports)
            {
                foreach (var support in wave.Supports)
                {
                    if (support != null && support.BossEscortAmount > 0 && !PerunRules.IsHiddenRole(support.BossEscortType))
                    {
                        members.Add((support.BossEscortType, Difficulty(support.BossEscortDifficult?.FirstOrDefault()), new[] { support.BossEscortAmount }));
                    }
                }
            }
            else if (TryRole(wave.BossEscortType, out WildSpawnType escort) && !PerunRules.IsHiddenRole(escort))
            {
                int[] counts = Counts(wave.BossEscortAmount);
                if (counts.Any(c => c > 0))
                {
                    members.Add((escort, Difficulty(wave.BossEscortDifficult), counts));
                }
            }

            string key = string.Join("|", members.Select(m => m.role.ToString()));
            if (!entries.TryGetValue(key, out Entry entry))
            {
                entry = new Entry { Boss = boss, Members = members };
                entries[key] = entry;
                order.Add(key);
            }
            else
            {
                for (int i = 0; i < members.Count; i++)
                {
                    var known = entry.Members[i];
                    entry.Members[i] = (known.role, known.difficulty, known.counts.Union(members[i].counts).OrderBy(c => c).ToArray());
                }
            }
            if (!string.IsNullOrEmpty(map) && !entry.Maps.Contains(MapName(map)))
            {
                entry.Maps.Add(MapName(map));
            }
        }

        List<Entry> list = order.Select(k => entries[k]).ToList();
        foreach (Entry entry in list)
        {
            entry.Name = Names.TryGetValue(entry.Boss, out string name) ? name : SpawnNames.Of(entry.Boss);
        }
        foreach (var same in list.GroupBy(e => e.Name).Where(g => g.Count() > 1))
        {
            foreach (Entry entry in same)
            {
                entry.Name += entry.Maps.Count > 0 ? $" · {entry.Maps[0]}" : "";
            }
        }
        foreach (var same in list.GroupBy(e => e.Name).Where(g => g.Count() > 1))
        {
            foreach (Entry entry in same)
            {
                entry.Name += $" ({entry.Members.Sum(m => m.counts.Max())})";
            }
        }

        return list
            .Select(e => new GroupPlan(GroupSource.Spt, e.Name, e.Members.Select(m => new GroupMember(m.role, m.difficulty, m.counts)).ToList(), e.Maps, null))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed class Entry
    {
        public WildSpawnType Boss;
        public List<(WildSpawnType role, BotDifficulty difficulty, int[] counts)> Members;
        public readonly List<string> Maps = new();
        public string Name;
    }

    private static bool TryRole(string name, out WildSpawnType role)
    {
        role = default;
        return !string.IsNullOrEmpty(name) && Enum.TryParse(name.Trim(), true, out role) && Enum.IsDefined(typeof(WildSpawnType), role);
    }

    private static BotDifficulty Difficulty(string text)
    {
        string first = text?.Split(',')[0].Trim();
        return !string.IsNullOrEmpty(first) && Enum.TryParse(first, true, out BotDifficulty difficulty) ? difficulty : BotDifficulty.normal;
    }

    private static int[] Counts(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new[] { 0 };
        }
        List<int> counts = new();
        foreach (string part in text.Split(','))
        {
            if (int.TryParse(part.Trim(), out int value) && value >= 0)
            {
                counts.Add(value);
            }
        }
        return counts.Count > 0 ? counts.ToArray() : new[] { 0 };
    }
}
