using System;
using System.Reflection;
using EFT;
using HarmonyLib;
using Perun.Client.Mode;

namespace Perun.Client.Patches;

// Game and mod checks that tell bots from human players, run with the controlled bot counted as a bot.
internal static class AsBotPatch
{
    private const string MethodCollectWarnTargets = "CollectWarnTargets";

    private const string HarmonyAsBot = ".asbot";

    private static readonly HarmonyMethod Prefix = new(typeof(AsBotPatch), nameof(ShowAsBot));
    private static readonly HarmonyMethod Finalizer = new(typeof(AsBotPatch), nameof(EndShowAsBot));

    public static void Enable()
    {
        Harmony harmony = new(Plugin.Guid + HarmonyAsBot);
        Apply(harmony, AccessTools.Method(typeof(BotsGroup), nameof(BotsGroup.IsPlayerEnemy)));
        // A group warns a human whose side its settings say to warn, but a bot only if its role is on the warn list;
        // seen as human, the controlled bot was warned and then shot by its former teammates.
        Apply(harmony, AccessTools.Method(typeof(BotGroupWarnData), MethodCollectWarnTargets));
    }

    public static void Apply(Harmony harmony, MethodBase target) => harmony.Patch(target, prefix: Prefix, finalizer: Finalizer);

    private static void ShowAsBot(out bool __state) => __state = Possession.ShowAsBot();

    private static Exception EndShowAsBot(Exception __exception, bool __state)
    {
        Possession.EndShowAsBot(__state);
        return __exception;
    }
}
