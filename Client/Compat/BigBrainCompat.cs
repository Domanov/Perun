using System;
using System.Reflection;
using EFT;
using HarmonyLib;
using Perun.Client.Mode;
using static Perun.Client.Compat.Constants.BigBrainCompat;

namespace Perun.Client.Compat;

internal static class BigBrainCompat
{
    public static void Enable()
    {
        try
        {
            Type patch = AccessTools.TypeByName(TypeAgentPatch);
            MethodInfo prefix = patch != null ? AccessTools.Method(patch, MethodPatchPrefix) : null;
            if (prefix == null)
            {
                Plugin.Log.LogWarning("BigBrain's brain update wasn't found; bot brains may keep running during a pause or while you control a bot");
                return;
            }

            new Harmony(Plugin.Guid + HarmonyBigBrain).Patch(prefix, prefix: new HarmonyMethod(typeof(BigBrainCompat), nameof(Skip)));
            Plugin.Log.LogInfo("BigBrain: bot brains stop with the world and while controlled");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"BigBrain: could not hook the brain update: {ex.Message}");
        }
    }

    private static bool Skip(AICoreAgent<BotLogicDecision> __0, ref bool __result)
    {
        if (!WorldPause.Paused && (__0 == null || !Possession.IsPossessedObject(__0._monoBehObject)))
        {
            return true;
        }
        __result = false;
        return false;
    }
}
