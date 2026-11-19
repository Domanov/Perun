using System.Reflection;
using EFT.CameraControl;
using HarmonyLib;
using Perun.Client.Mode;
using SPT.Reflection.Patching;

namespace Perun.Client.Patches;

public class PlayerCameraLateUpdatePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(PlayerCameraController), nameof(PlayerCameraController.LateUpdate));

    [PatchPrefix]
    public static bool PatchPrefix(PlayerCameraController __instance)
    {
        if (!PerunMode.OwnsCamera || __instance.Player == null)
        {
            return true;
        }
        if (Possession.Body != null)
        {
            return ReferenceEquals(__instance.Player, Possession.Body);
        }
        return !__instance.Player.IsYourPlayer;
    }
}
