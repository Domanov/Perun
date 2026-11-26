using System;
using System.Reflection;
using EFT;
using HarmonyLib;
using Perun.Client.Mode;

namespace Perun.Client.Compat;

// Under Fika, "your player" dying runs Fika's own death flow, which ends the raid for you. The bot you control is
// "your player" while you control it, so when it dies hand that back to your parked character first.
internal static class FikaDeathGuard
{
    private const string MethodOnDead = "OnDead";

    private const string HarmonyFikaDeath = ".fikadeath";

    private const string TypeFikaPlayer = "Fika.Core.Main.Players.FikaPlayer";

    public static void Enable()
    {
        if (!FikaInfo.Present)
        {
            return;
        }

        MethodInfo onDead = AccessTools.Method(AccessTools.TypeByName(TypeFikaPlayer), MethodOnDead, new[] { typeof(EDamageType) });
        if (onDead == null)
        {
            Plugin.Log.LogWarning("Fika: its player death handler wasn't found; a controlled bot dying could end your raid");
            return;
        }
        try
        {
            new Harmony(Plugin.Guid + HarmonyFikaDeath).Patch(onDead, prefix: new HarmonyMethod(typeof(FikaDeathGuard), nameof(NotYourDeath)));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Fika: couldn't guard the controlled bot's death: {ex.Message}");
        }
    }

    private static void NotYourDeath(Player __instance)
    {
        Player parked = Takeover.Parked;
        if (parked == null || !__instance.IsYourPlayer || ReferenceEquals(__instance, parked))
        {
            return;
        }
        __instance.IsYourPlayer = false;
        parked.IsYourPlayer = true;
        Plugin.Log.LogInfo($"Control: {__instance.Profile?.Nickname} died; your character is \"your player\" again");
    }
}
