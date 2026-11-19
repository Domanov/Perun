using System.Reflection;
using EFT;
using HarmonyLib;
using Perun.Client.Mode;
using SPT.Reflection.Patching;

namespace Perun.Client.Patches;

public class BotUpdatePausePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(BotOwner), nameof(BotOwner.UpdateManual));

    [PatchPrefix]
    public static bool PatchPrefix(BotOwner __instance) => !Possession.IsPossessed(__instance) && !WorldPause.ShouldSkipBot(__instance);
}

public class BotFixedUpdatePausePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(BotOwner), nameof(BotOwner.FixedUpdate));

    [PatchPrefix]
    public static bool PatchPrefix(BotOwner __instance) => !Possession.IsPossessed(__instance) && !WorldPause.ShouldSkipBot(__instance);
}

public class RaidEndTimerPausePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(EndByTimerScenario), nameof(EndByTimerScenario.Update));

    [PatchPrefix]
    public static bool PatchPrefix() => !WorldPause.Paused;
}

public class BotBrainPausePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(AICoreAgent<BotLogicDecision>), nameof(AICoreAgent<BotLogicDecision>.Update));

    [PatchPrefix]
    public static bool PatchPrefix(AICoreAgent<BotLogicDecision> __instance) => !WorldPause.Paused && !Possession.IsPossessedObject(__instance._monoBehObject);
}

public class BotShootPausePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(ShootData), nameof(ShootData.Shoot));

    [PatchPrefix]
    public static bool PatchPrefix(ShootData __instance, ref bool __result)
    {
        if (!WorldPause.Paused && !Possession.IsPossessedShooter(__instance))
        {
            return true;
        }

        __result = false;
        return false;
    }
}
