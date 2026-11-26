namespace Perun.Client.Compat;

internal static class Constants
{
    internal static class Animators
    {
        internal const string TypeUnityAnimatorWrapper = "AnimationSystem.UnityAnimatorWrapper";

        internal const string FieldAnimators = "_animators";
        internal const string FieldAnimator = "_animator";
    }

    internal static class BigBrainCompat
    {
        internal const string MethodPatchPrefix = "PatchPrefix";

        internal const string HarmonyBigBrain = ".bigbrain";

        internal const string TypeAgentPatch = "DrakiaXYZ.BigBrain.Patches.BotAgentUpdatePatch";
    }

    internal static class ConfigManagerCompat
    {
        internal const string PropertyDisplayingWindow = "DisplayingWindow";

        internal const string GuidConfigManager = "com.bepis.bepinex.configurationmanager";
    }

    internal static class FikaBridgeLoader
    {
        internal const string TypeBridgePlugin = "Perun.Fika.Plugin";

        internal const string MethodStart = "Start";

        internal const string FieldVersion = "Version";

        internal const string FileName = "Perun.Fika.dll";
    }

    internal static class FikaDeathGuard
    {
        internal const string MethodOnDead = "OnDead";

        internal const string HarmonyFikaDeath = ".fikadeath";

        internal const string TypeFikaPlayer = "Fika.Core.Main.Players.FikaPlayer";
    }

    internal static class FikaInfo
    {
        internal const string GuidFika = "com.fika.core";

        internal const string TypeFikaBackendUtils = "Fika.Core.Main.Utils.FikaBackendUtils, Fika.Core";

        internal const string PropertyIsServer = "IsServer";
        internal const string PropertyIsHeadless = "IsHeadless";
    }

    internal static class FovFixCompat
    {
        internal const string MethodSetCompensationScale = "SetCompensationScale";

        internal const string PropertyFovController = "FovController";
        internal const string PropertyEnableFovScaleFix = "EnableFovScaleFix";
        internal const string PropertyFovScale = "FovScale";

        internal const string FieldPlayer = "_player";

        internal const string HarmonyFovFix = ".fovfix";

        internal const string TypeScalePatch = "FOVFix.CalculateScaleValueByFovPatch";
        internal const string TypeFovFixPlugin = "FOVFix.Plugin";
    }

    internal static class MoreBotsCompat
    {
        internal const string TypeHuntManager = "MoreBotsAPI.Components.HuntManager";
        internal const string TypeFactionManager = "MoreBotsAPI.Components.FactionManager";
        internal const string TypeWildSpawnTypeManager = "MoreBotsAPI.CustomWildSpawnTypeManager";

        internal const string MethodFindFirstHuntTarget = "FindFirstHuntTarget";
        internal const string MethodFindNewHuntTarget = "FindNewHuntTarget";
        internal const string MethodOnKill = "OnKill";
        internal const string MethodGetCustomWildSpawnTypes = "GetCustomWildSpawnTypes";

        internal const string PropertyWildSpawnTypeValue = "WildSpawnTypeValue";
        internal const string PropertyScavRole = "ScavRole";

        internal const string LabelCustomFaction = "Custom";

        internal const string HarmonyMoreBots = ".morebots";

        internal const string GuidMoreBots = "com.morebotsapi.tacticaltoaster";
    }

    internal static class OrbitCompat
    {
        internal const string MethodScanWorld = "ScanWorld";
        internal const string MethodUpdate = "Update";
        internal const string MethodIsBotDormant = "IsBotDormant";

        internal const string FieldHumanPositions = "_humanPositions";
        internal const string FieldBot = "Bot";

        internal const string HarmonyOrbit = ".orbit";

        internal const string TypeDormancy = "Orbit.Systems.DormancySystem";
        internal const string TypeMovement = "Orbit.Systems.MovementSystem";
        internal const string TypeLook = "Orbit.Systems.LookSystem";
        internal const string TypeAgent = "Orbit.Entities.Agent";
        internal const string TypeTelemetry = "Orbit.Api.OrbitTelemetry";
    }

    internal static class RealismCompat
    {
        internal const string MethodCheckIsReady = "CheckIsReady";
        internal const string MethodSetChosen = "SetChosen";
        internal const string MethodReconcile = "Reconcile";

        internal const string PropertyPlayerIsReady = "PlayerIsReady";
        internal const string PropertyPlayer = "Player";

        internal const string FieldPlayerStateInstance = "PlayerStateInstance";
        internal const string FieldStanceControllerInstance = "StanceControllerInstance";
        internal const string FieldInputHandler = "_inputHandler";

        internal const string HarmonyRealism = ".realism";

        internal const string TypeRealismPlugin = "RealismCommonLib.Plugin";
        internal const string TypeStancePlugin = "StanceOverhaul.Plugin";
        internal const string TypeGameState = "RealismCommonLib.StateControllers.GlobalState.GameState";
    }

    internal static class SainCompat
    {
        internal const string DumpWeaponRefresh = "dropped {0} stale, now {1} weapon(s)";
        internal const string DumpNoComponent = "no SAIN component";

        internal const string GuidSain = "me.sol.sain";

        internal const string TypeBotComponent = "SAIN.Components.BotComponent";
        internal const string TypeSquad = "SAIN.BotController.Classes.Squad";
        internal const string TypePlayerComponent = "SAIN.Components.PlayerComponentSpace.PlayerComponent";

        internal const string MethodManualUpdate = "ManualUpdate";
        internal const string MethodAssignSquadLeader = "assignSquadLeader";
        internal const string MethodIsPlayerAnEnemy = "IsPlayerAnEnemy";
        internal const string MethodRemoveEnemy = "RemoveEnemy";
        internal const string MethodAddMember = "AddMember";
        internal const string MethodGetAllWeapons = "getAllWeapons";
        internal const string MethodReCalcPowerOfEquipment = "ReCalcPowerOfEquipment";
        internal const string MethodDispose = "Dispose";

        internal const string PropertyBotsGroup = "BotsGroup";
        internal const string PropertyLeaderComponent = "LeaderComponent";
        internal const string PropertyMembers = "Members";
        internal const string PropertySquad = "Squad";
        internal const string PropertySquadInfo = "SquadInfo";
        internal const string PropertyBotOwner = "BotOwner";
        internal const string PropertyEnemyController = "EnemyController";
        internal const string PropertyBotStuck = "BotStuck";
        internal const string PropertyEquipment = "Equipment";
        internal const string PropertyWeaponInfos = "WeaponInfos";
        internal const string PropertyWeapon = "Weapon";

        internal const string FieldDontUnstuckMe = "DontUnstuckMe";
        internal const string FieldBotUnstuckCoroutine = "_botUnstuckCoroutine";

        internal const string HarmonySain = ".sain";
    }

    internal static class SmoothieControl
    {
        internal const string TypeAnimatorThrottle = "Smoothie.AnimatorThrottle";
        internal const string TypeAnimatorPause = "Smoothie.AnimatorPause";
        internal const string TypeDistantDetail = "Smoothie.DistantDetail";
        internal const string TypeHandsCulling = "Smoothie.HandsCulling";
        internal const string TypeLodCulling = "Smoothie.LodCulling";
        internal const string TypeState = "State";

        internal const string MethodShowAll = "ShowAll";
        internal const string MethodBeforeLateUpdateSafe = "BeforeLateUpdateSafe";
        internal const string MethodTryGetValue = "TryGetValue";
        internal const string MethodRemove = "Remove";
        internal const string MethodFinishUnityStep = "FinishUnityStep";
        internal const string MethodGiveBack = "GiveBack";
        internal const string MethodRelease = "Release";
        internal const string MethodForget = "Forget";
        internal const string MethodAfterCullerChange = "AfterCullerChange";
        internal const string MethodApply = "Apply";

        internal const string FieldStates = "States";
        internal const string FieldBody = "Body";
        internal const string FieldArms = "Arms";
        internal const string FieldStepScale = "StepScale";
        internal const string FieldReleasing = "Releasing";
        internal const string FieldEntries = "Entries";

        internal const string DumpFullRate = "full-rate animation";
        internal const string DumpThrottleRelease = "throttle release";
        internal const string DumpPausedRelease = "paused animator release";
        internal const string DumpDistantDetail = "distant detail";
        internal const string DumpWeaponVisible = "weapon visible";
        internal const string DumpLodRefresh = "LOD refresh";

        internal const string LabelSlowedAnimator = "a controlled bot may keep a slowed animator for a moment after you take it";
        internal const string LabelFrozen = "a bot Smoothie had paused may stay frozen when you take it";
        internal const string LabelLowDetail = "a controlled bot far from your character may stay at low detail";
        internal const string LabelWeaponHidden = "a controlled bot's weapon may be hidden";
        internal const string LabelBuildingsHidden = "buildings may stay partly hidden after entering RTS mode until Smoothie's A/B switch is toggled";

        internal const string HarmonySmoothie = ".smoothie";

        internal const string GuidSmoothie = "com.tominocz.smoothie";
    }

    internal static class SmoothieSettings
    {
        internal const string ConfigSectionSmoothieOtherFixes = "3. Other fixes";

        internal const string ConfigKeySmoothieHideCovered = "Hide objects covered by nearer ones (experimental)";

        internal const string GuidSmoothie = "com.tominocz.smoothie";
    }
}
