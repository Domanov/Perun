using System;
using EFT.HealthSystem;
using HarmonyLib;
using Perun.Client.Mode;
using static Perun.Client.Patches.Constants.HealthPatches;

namespace Perun.Client.Patches;

internal static class HealthPatches
{
    private static readonly string[] Blocked =
    {
        nameof(ActiveHealthController.Kill),
        nameof(ActiveHealthController.DestroyBodyPart),
        nameof(ActiveHealthController.DoFracture),
        nameof(ActiveHealthController.DoContusion),
        nameof(ActiveHealthController.DoStun),
        nameof(ActiveHealthController.DoBurnEyes),
        nameof(ActiveHealthController.DoZombieInfection),
        nameof(ActiveHealthController.DoRadExposure),
        nameof(ActiveHealthController.DoPain),
        nameof(ActiveHealthController.DoDisorientation),
        nameof(ActiveHealthController.DoSandingScreen),
        nameof(ActiveHealthController.BluntContusion)
    };

    public static void Enable()
    {
        Harmony harmony = new(Plugin.Guid + HarmonyHealth);
        HarmonyMethod block = new(typeof(HealthPatches), nameof(BlockPrefix));
        int count = 0;

        foreach (string name in Blocked)
        {
            count += PatchAll(harmony, name, block);
        }

        foreach (var method in AccessTools.GetDeclaredMethods(typeof(ActiveHealthController)))
        {
            if (method.Name == nameof(ActiveHealthController.DoBleed) && !method.IsGenericMethodDefinition)
            {
                harmony.Patch(method, prefix: block);
                count++;
            }
        }

        var fall = AccessTools.Method(typeof(ActiveHealthController), nameof(ActiveHealthController.HandleFall));
        harmony.Patch(fall, prefix: new HarmonyMethod(typeof(HealthPatches), nameof(FallPrefix)));
        count++;

        var change = AccessTools.Method(typeof(ActiveHealthController), nameof(ActiveHealthController.ChangeHealth));
        harmony.Patch(change, prefix: new HarmonyMethod(typeof(HealthPatches), nameof(ChangeHealthPrefix)));
        count++;

        if (Plugin.Verbose.Value)
        {
            Plugin.Log.LogInfo($"God mode: {count} health methods guarded");
        }
    }

    private static int PatchAll(Harmony harmony, string name, HarmonyMethod prefix)
    {
        int count = 0;
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(ActiveHealthController)))
        {
            if (method.Name != name || method.IsGenericMethodDefinition || method.ReturnType != typeof(void))
            {
                continue;
            }

            try
            {
                harmony.Patch(method, prefix: prefix);
                count++;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"God mode: could not guard {name}: {ex.Message}");
            }
        }
        return count;
    }

    private static bool BlockPrefix(ActiveHealthController __instance)
    {
        return !GodMode.IsProtected(__instance);
    }

    private static bool FallPrefix(ActiveHealthController __instance, ref float __result)
    {
        if (!GodMode.IsProtected(__instance))
        {
            return true;
        }

        __result = 0f;
        return false;
    }

    private static bool ChangeHealthPrefix(ActiveHealthController __instance, float value)
    {
        return value >= 0f || !GodMode.IsProtected(__instance);
    }
}
