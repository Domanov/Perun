using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using EFT;
using HarmonyLib;
using Perun.Client.Mode;
using UnityEngine;
using static Perun.Client.Compat.Constants.OrbitCompat;

namespace Perun.Client.Compat;

// ORBIT's ghost mode switches off bots far from human players, and it doesn't count the RTS camera or the bot you
// control as one. It also keeps steering that bot.
internal static class OrbitCompat
{
    private static FieldInfo _humans;
    private static FieldInfo _agentBot;
    private static Func<string, bool> _isDormant;
    private static object _hidden;

    public static void Enable()
    {
        Type dormancy = AccessTools.TypeByName(TypeDormancy);
        if (dormancy == null)
        {
            return;
        }

        try
        {
            Harmony harmony = new(Plugin.Guid + HarmonyOrbit);
            List<string> found = new();

            _humans = AccessTools.Field(dormancy, FieldHumanPositions);
            MethodInfo scan = AccessTools.Method(dormancy, MethodScanWorld);
            if (_humans != null && scan != null)
            {
                harmony.Patch(scan, postfix: new HarmonyMethod(typeof(OrbitCompat), nameof(AddEyes)));
                found.Add("bots wake around the RTS camera and the controlled bot");
            }

            Type agent = AccessTools.TypeByName(TypeAgent);
            _agentBot = agent != null ? AccessTools.Field(agent, FieldBot) : null;
            if (_agentBot != null)
            {
                int skipped = 0;
                foreach (string type in new[] { TypeMovement, TypeLook })
                {
                    MethodInfo update = AccessTools.Method(AccessTools.TypeByName(type), MethodUpdate, new[] { typeof(List<>).MakeGenericType(agent) });
                    if (update != null)
                    {
                        harmony.Patch(update, prefix: new HarmonyMethod(typeof(OrbitCompat), nameof(HidePossessed)), finalizer: new HarmonyMethod(typeof(OrbitCompat), nameof(RestorePossessed)));
                        skipped++;
                    }
                }
                if (skipped == 2)
                {
                    found.Add("it leaves the controlled bot's movement and aim to you");
                }
            }

            MethodInfo dormant = AccessTools.Method(AccessTools.TypeByName(TypeTelemetry), MethodIsBotDormant, new[] { typeof(string) });
            if (dormant != null)
            {
                _isDormant = (Func<string, bool>)Delegate.CreateDelegate(typeof(Func<string, bool>), dormant);
            }

            Plugin.Log.LogInfo(found.Count == 0
                ? "ORBIT: none of its ghost mode hooks were found; bots far from your character may vanish from the RTS view"
                : $"ORBIT: {string.Join("; ", found)}");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"ORBIT: could not hook ghost mode: {ex.Message}");
        }
    }

    public static bool IsAsleep(Player player) => _isDormant != null && player != null && _isDormant(player.ProfileId);

    private static void AddEyes(object __instance)
    {
        if (_humans.GetValue(__instance) is not List<Vector3> humans)
        {
            return;
        }
        PerunMode mode = Plugin.Mode;
        if (mode != null && mode.Active)
        {
            humans.Add(mode.Camera.Pivot);
        }
        Player body = Possession.Body;
        if (body != null)
        {
            humans.Add(body.Position);
        }
    }

    private static void HidePossessed(object __0, out int __state)
    {
        __state = -1;
        BotOwner bot = Possession.Bot;
        if (bot == null || __0 is not IList agents)
        {
            return;
        }
        for (int i = 0; i < agents.Count; i++)
        {
            if (ReferenceEquals(_agentBot.GetValue(agents[i]), bot))
            {
                __state = i;
                _hidden = agents[i];
                agents.RemoveAt(i);
                return;
            }
        }
    }

    private static Exception RestorePossessed(object __0, int __state, Exception __exception)
    {
        if (__state >= 0 && __0 is IList agents && _hidden != null)
        {
            agents.Insert(Math.Min(__state, agents.Count), _hidden);
        }
        _hidden = null;
        return __exception;
    }
}
