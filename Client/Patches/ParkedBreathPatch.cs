using System.Reflection;
using EFT;
using HarmonyLib;
using Perun.Client.Mode;
using SPT.Reflection.Patching;

namespace Perun.Client.Patches;

// The breathing and pain groans of your parked character would otherwise keep playing as your own voice
// while you control a bot.
public class ParkedBreathPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(Player), nameof(Player.UpdateBreathStatus));

    [PatchPrefix]
    public static bool PatchPrefix(Player __instance)
    {
        if (!ReferenceEquals(__instance, Takeover.Parked))
        {
            return true;
        }
        __instance.HeavyBreath = false;
        return false;
    }
}
