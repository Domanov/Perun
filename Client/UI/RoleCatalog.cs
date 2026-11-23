using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using Perun.Client.Compat;
using Perun.Client.Units;

namespace Perun.Client.UI;

// The spawnable roles by faction, shared by the Bots tab and the group editor.
internal sealed class RoleCatalog
{
    private static readonly (string name, Func<WildSpawnType, string, bool> match)[] Categories =
    {
        ("Scavs", (t, n) => t is WildSpawnType.assault or WildSpawnType.marksman or WildSpawnType.cursedAssault or WildSpawnType.crazyAssaultEvent),
        ("PMC", (t, n) => t is WildSpawnType.pmcUSEC or WildSpawnType.pmcBEAR),
        ("Raiders", (t, n) => t is WildSpawnType.pmcBot or WildSpawnType.exUsec or WildSpawnType.arenaFighter or WildSpawnType.arenaFighterEvent),
        ("Bosses", (t, n) => n.StartsWith("boss", StringComparison.Ordinal)),
        ("Followers", (t, n) => n.StartsWith("follower", StringComparison.Ordinal) || n == "tagillaHelperAgro"),
        ("Cultists", (t, n) => n.StartsWith("sectant", StringComparison.Ordinal) || n == "sectactPriestEvent"),
        ("Infected", (t, n) => n.StartsWith("infected", StringComparison.Ordinal)),
        ("Other", null)
    };

    private static RoleCatalog _shared;

    public static RoleCatalog Shared => _shared ??= new RoleCatalog();

    public string[] CategoryNames { get; }
    public List<WildSpawnType>[] Roles { get; }
    public string[][] Names { get; }

    private RoleCatalog()
    {
        List<string> names = Categories.Select(c => c.name).ToList();
        List<List<WildSpawnType>> roles = names.Select(_ => new List<WildSpawnType>()).ToList();
        int other = Categories.Length - 1;

        foreach (WildSpawnType type in Enum.GetValues(typeof(WildSpawnType)).Cast<WildSpawnType>().Distinct())
        {
            if (PerunRules.IsHiddenRole(type))
            {
                continue;
            }

            if (MoreBotsCompat.TryGetFaction((int)type, out string faction))
            {
                int factionIndex = names.IndexOf(faction);
                if (factionIndex < 0)
                {
                    names.Insert(other, faction);
                    roles.Insert(other, new List<WildSpawnType>());
                    factionIndex = other;
                    other++;
                }
                roles[factionIndex].Add(type);
                continue;
            }

            string name = SpawnNames.Id(type);
            int index = other;
            for (int i = 0; i < Categories.Length - 1; i++)
            {
                if (Categories[i].match(type, name))
                {
                    index = i;
                    break;
                }
            }
            roles[index].Add(type);
        }

        foreach (List<WildSpawnType> list in roles)
        {
            list.Sort((a, b) => string.CompareOrdinal(SpawnNames.Id(a), SpawnNames.Id(b)));
        }

        CategoryNames = names.ToArray();
        Roles = roles.ToArray();
        Names = new string[Roles.Length][];
        for (int i = 0; i < Roles.Length; i++)
        {
            Names[i] = Roles[i].Select(SpawnNames.Of).ToArray();
        }
    }

    // Every category's matches, each name suffixed with its category in dim text.
    public void Search(string needle, List<WildSpawnType> roles, List<string> names)
    {
        roles.Clear();
        names.Clear();
        needle = needle.Trim();
        for (int c = 0; c < Roles.Length; c++)
        {
            for (int i = 0; i < Roles[c].Count; i++)
            {
                string name = Names[c][i];
                if (name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 || SpawnNames.Id(Roles[c][i]).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    roles.Add(Roles[c][i]);
                    names.Add(name + "  <color=#7f8a95>" + CategoryNames[c] + "</color>");
                }
            }
        }
    }
}
