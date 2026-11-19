using System.Reflection;
using EFT;
using HarmonyLib;
using Perun.Client.Mode;
using SPT.Reflection.Patching;

namespace Perun.Client.Patches;

public class AddEnemyPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(BotsGroup), nameof(BotsGroup.AddEnemy));

    [PatchPrefix]
    public static bool PatchPrefix(IPlayer person, ref bool __result)
    {
        if (!AiIgnore.IsIgnored(person))
        {
            return true;
        }

        __result = false;
        return false;
    }
}
