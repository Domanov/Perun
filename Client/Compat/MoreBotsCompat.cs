using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using Perun.Client.Patches;
using static Perun.Client.Compat.Constants.MoreBotsCompat;

namespace Perun.Client.Compat;

internal static class MoreBotsCompat
{
    private static Dictionary<int, string> _factions;

    public static bool Present => Chainloader.PluginInfos.ContainsKey(GuidMoreBots);

    public static IReadOnlyDictionary<int, string> Factions => _factions ??= Load();

    public static bool TryGetFaction(int type, out string faction) => Factions.TryGetValue(type, out faction);

    // MoreBots hunts and takes revenge on players that aren't AI; a controlled bot must still count as a bot there.
    private static readonly (string type, string method)[] HumanChecks =
    {
        (TypeHuntManager, MethodFindFirstHuntTarget),
        (TypeHuntManager, MethodFindNewHuntTarget),
        (TypeFactionManager, MethodOnKill),
    };

    public static void Enable()
    {
        if (!Present)
        {
            return;
        }
        Harmony harmony = new(Plugin.Guid + HarmonyMoreBots);
        foreach ((string type, string method) in HumanChecks)
        {
            try
            {
                Type owner = FindType(type);
                MethodInfo target = owner != null ? AccessTools.Method(owner, method) : null;
                if (target == null)
                {
                    Plugin.Log.LogWarning($"MoreBotsAPI: {type}.{method} not found; its bots may treat a bot you control as a human player");
                    continue;
                }
                AsBotPatch.Apply(harmony, target);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"MoreBotsAPI: could not hook {method}: {ex.Message}");
            }
        }
    }

    private static Dictionary<int, string> Load()
    {
        Dictionary<int, string> result = new();
        Type manager = FindType(TypeWildSpawnTypeManager);
        if (manager == null)
        {
            if (Present)
            {
                Plugin.Log.LogWarning("MoreBotsAPI found but its type list isn't; custom bots are listed under Other");
            }
            return result;
        }

        try
        {
            MethodInfo getTypes = manager.GetMethod(MethodGetCustomWildSpawnTypes, BindingFlags.Public | BindingFlags.Static);
            if (getTypes?.Invoke(null, null) is not IEnumerable types)
            {
                return result;
            }

            foreach (object custom in types)
            {
                Type type = custom.GetType();
                int value = (int)type.GetProperty(PropertyWildSpawnTypeValue).GetValue(custom);
                string faction = type.GetProperty(PropertyScavRole)?.GetValue(custom) as string;
                result[value] = string.IsNullOrWhiteSpace(faction) ? LabelCustomFaction : faction;
            }

            Plugin.Log.LogInfo($"MoreBotsAPI: {result.Count} custom bot type(s) in {new HashSet<string>(result.Values).Count} faction(s)");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"MoreBotsAPI: could not read custom bot types: {ex.Message}");
        }

        return result;
    }

    private static Type FindType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name, false);
            if (type != null)
            {
                return type;
            }
        }
        return null;
    }
}
