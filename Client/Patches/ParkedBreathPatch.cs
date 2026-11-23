using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Perun.Client.Patches;

// The breathing and pain groans of your parked character would otherwise keep playing as your own voice in RTS mode,
// also while you control a bot (the body stays parked until you leave RTS mode).
public class ParkedBreathPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(Player), nameof(Player.UpdateBreathStatus));

    [PatchPrefix]
    public static bool PatchPrefix(Player __instance)
    {
        if (Plugin.Mode?.IsParked(__instance) != true)
        {
            return true;
        }
        __instance.HeavyBreath = false;
        return false;
    }
}
