namespace Perun.Client.Patches;

internal static class Constants
{
    internal static class PossessedMalfunctionPatch
    {
        internal const string FieldOwner = "_owner";
    }

    internal static class PossessedGuard
    {
        internal const string MatchSain = "SAIN";
        internal const string MatchBigBrain = "DrakiaXYZ";
        internal const string MatchOrbit = "Orbit";
        internal const string MatchBot = "Bot";
        internal const string MatchAiCore = "AICore";
        internal const string MatchAiming = "Aiming";
        internal const string MatchStandartBotBrain = "StandartBotBrain";

        internal const string DumpMove = "move";
        internal const string DumpPose = "pose";
        internal const string DumpProne = "prone";
        internal const string DumpSprint = "sprint";
        internal const string DumpJump = "jump";
        internal const string DumpAim = "aim";
        internal const string DumpTrigger = "trigger";
    }

    internal static class AsBotPatch
    {
        internal const string MethodCollectWarnTargets = "CollectWarnTargets";

        internal const string HarmonyAsBot = ".asbot";
    }

    internal static class HealthPatches
    {
        internal const string HarmonyHealth = ".health";
    }

    internal static class ParkedHitPatch
    {
        internal const string MethodApplyHitDebuff = "ApplyHitDebuff";
    }
}
