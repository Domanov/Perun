using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;

namespace Perun.Client.Compat;

internal static class MoreBotsCompat
{
    private const string PluginGuid = "com.morebotsapi.tacticaltoaster";
    private const string ManagerType = "MoreBotsAPI.CustomWildSpawnTypeManager";

    private static Dictionary<int, string> _factions;

    public static bool Present => Chainloader.PluginInfos.ContainsKey(PluginGuid);

    public static IReadOnlyDictionary<int, string> Factions => _factions ??= Load();

    public static bool TryGetFaction(int type, out string faction) => Factions.TryGetValue(type, out faction);

    private static Dictionary<int, string> Load()
    {
        Dictionary<int, string> result = new();
        Type manager = FindType(ManagerType);
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
            MethodInfo getTypes = manager.GetMethod("GetCustomWildSpawnTypes", BindingFlags.Public | BindingFlags.Static);
            if (getTypes?.Invoke(null, null) is not IEnumerable types)
            {
                return result;
            }

            foreach (object custom in types)
            {
                Type type = custom.GetType();
                int value = (int)type.GetProperty("WildSpawnTypeValue").GetValue(custom);
                string faction = type.GetProperty("ScavRole")?.GetValue(custom) as string;
                result[value] = string.IsNullOrWhiteSpace(faction) ? "Custom" : faction;
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
