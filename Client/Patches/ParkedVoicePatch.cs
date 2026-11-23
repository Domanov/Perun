using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Perun.Client.Patches;

// Your parked character is held under the map by teleporting it back every frame, so it keeps falling and landing;
// the landing and fall damage make it grunt through Say even with god mode zeroing the damage.
public class ParkedVoicePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player), nameof(Player.Say));

    [PatchPrefix]
    public static bool PatchPrefix(Player __instance) => Plugin.Mode?.IsParked(__instance) != true;
}
