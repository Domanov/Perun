using System.Reflection;
using EFT;
using HarmonyLib;
using Perun.Client.Mode;
using SPT.Reflection.Patching;
using UnityEngine;

namespace Perun.Client.Patches;

public class PossessedWeaponDirectionPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.PropertyGetter(typeof(AIFirearmController), nameof(AIFirearmController.WeaponDirection));

    [PatchPrefix]
    public static bool PatchPrefix(AIFirearmController __instance, ref Vector3 __result)
    {
        Player body = Possession.Body;
        if (body == null || !ReferenceEquals(body.HandsController, __instance))
        {
            return true;
        }

        __result = __instance.CurrentFireport.Original.TransformDirection(body.LocalShotDirection);
        return false;
    }
}

public class PossessedMoverSnapPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(BotMoverImpostor), nameof(BotMoverImpostor.OnMotionApplied));

    [PatchPrefix]
    public static bool PatchPrefix(BotMoverImpostor __instance)
    {
        return Possession.Bot == null || !ReferenceEquals(Possession.Bot.Mover, __instance);
    }
}

public class PossessedInputScopePatch : ModulePatch
{
    public static bool FromInput;

    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(MoveInputTranslator), nameof(MoveInputTranslator.TranslateAxes));

    [PatchPrefix]
    public static void PatchPrefix() => FromInput = true;

    [PatchPostfix]
    public static void PatchPostfix() => FromInput = false;
}

public class PossessedRotatePatch : ModulePatch
{
    private static int _logged;

    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(Player), nameof(Player.Rotate), new[] { typeof(Vector2), typeof(bool) });

    [PatchPrefix]
    public static bool PatchPrefix(Player __instance)
    {
        if (Possession.Body == null || PossessedInputScopePatch.FromInput || !ReferenceEquals(__instance, Possession.Body))
        {
            return true;
        }

        if (_logged < 3)
        {
            _logged++;
            string trace = new System.Diagnostics.StackTrace(1, false).ToString();
            Plugin.Log.LogWarning($"Control: blocked a turn of {__instance.Profile?.Nickname} that didn't come from you\n{(trace.Length > 700 ? trace.Substring(0, 700) : trace)}");
        }
        return false;
    }
}

internal static class PossessedGuard
{
    private static readonly System.Collections.Generic.HashSet<string> Logged = new();

    public static bool AllowHands(Player.FirearmController hands, string action)
    {
        Player body = Possession.Body;
        if (body == null || !ReferenceEquals(body.HandsController, hands))
        {
            return true;
        }
        return Allow(body, action);
    }

    public static bool Allow(Player player, string action)
    {
        if (Possession.Body == null || player == null || !ReferenceEquals(player, Possession.Body) || PossessedInputScopePatch.FromInput)
        {
            return true;
        }

        // Walking the stack on a hot path is normally avoided. It stays because AI commands reach the
        // controlled body from many places (BotOwner, BigBrain layers, SAIN, aiming), and a flag set in
        // each of those entry points could not be verified outside the game. Bounded to one body and 16 frames.
        System.Diagnostics.StackTrace stack = new(2, false);
        int frames = System.Math.Min(stack.FrameCount, 16);
        for (int i = 0; i < frames; i++)
        {
            System.Type type = stack.GetFrame(i)?.GetMethod()?.DeclaringType;
            if (type == null)
            {
                continue;
            }
            string space = type.Namespace ?? "";
            string name = type.Name;
            if (space.StartsWith("SAIN") || space.StartsWith("DrakiaXYZ") || space.StartsWith("Orbit") || name.StartsWith("Bot") || name.StartsWith("AICore") || name.StartsWith("Aiming") || name == "StandartBotBrain")
            {
                if (Logged.Add(action + type.FullName))
                {
                    Plugin.Log.LogWarning($"Control: blocked {action} on {player.Profile?.Nickname} from {type.FullName}");
                }
                return false;
            }
        }
        return true;
    }
}

public class PossessedMovePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player), nameof(Player.Move), new[] { typeof(Vector2) });

    [PatchPrefix]
    public static bool PatchPrefix(Player __instance) => PossessedGuard.Allow(__instance, "move");
}

public class PossessedPosePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player), nameof(Player.ChangePose), new[] { typeof(float) });

    [PatchPrefix]
    public static bool PatchPrefix(Player __instance) => PossessedGuard.Allow(__instance, "pose");
}

public class PossessedPronePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player), nameof(Player.ToggleProne));

    [PatchPrefix]
    public static bool PatchPrefix(Player __instance) => PossessedGuard.Allow(__instance, "prone");
}

public class PossessedSprintPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player), nameof(Player.EnableSprint), new[] { typeof(bool) });

    [PatchPrefix]
    public static bool PatchPrefix(Player __instance) => PossessedGuard.Allow(__instance, "sprint");
}

public class PossessedJumpPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player), nameof(Player.Jump));

    [PatchPrefix]
    public static bool PatchPrefix(Player __instance) => PossessedGuard.Allow(__instance, "jump");
}

public class PossessedAimPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.SetAim), new[] { typeof(bool) });

    [PatchPrefix]
    public static bool PatchPrefix(Player.FirearmController __instance) => PossessedGuard.AllowHands(__instance, "aim");
}

public class PossessedTriggerPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.SetTriggerPressed), new[] { typeof(bool) });

    [PatchPrefix]
    public static bool PatchPrefix(Player.FirearmController __instance) => PossessedGuard.AllowHands(__instance, "trigger");
}

public class KeepCameraPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(EFT.CameraControl.CameraManager), nameof(EFT.CameraControl.CameraManager.SetCameraFromSettings));

    [PatchPrefix]
    public static bool PatchPrefix() => !Takeover.KeepCamera;
}

public class KeepCameraVisorPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(EFT.CameraControl.PlayerCameraController), nameof(EFT.CameraControl.PlayerCameraController.OnFaceShieldComponentChanged));

    [PatchFinalizer]
    public static System.Exception PatchFinalizer(System.Exception __exception) => Takeover.KeepCamera || Possession.Body != null ? null : __exception;
}
