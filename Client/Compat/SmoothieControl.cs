using System;
using System.Collections;
using System.Reflection;
using BepInEx.Bootstrap;
using EFT;
using HarmonyLib;
using Perun.Client.Mode;
using UnityEngine;

namespace Perun.Client.Compat;

internal static class SmoothieControl
{
    private const string TypeAnimatorThrottle = "Smoothie.AnimatorThrottle";
    private const string TypeAnimatorPause = "Smoothie.AnimatorPause";
    private const string TypeDistantDetail = "Smoothie.DistantDetail";
    private const string TypeHandsCulling = "Smoothie.HandsCulling";
    private const string TypeLodCulling = "Smoothie.LodCulling";
    private const string TypeState = "State";

    private const string MethodShowAll = "ShowAll";
    private const string MethodBeforeLateUpdateSafe = "BeforeLateUpdateSafe";
    private const string MethodTryGetValue = "TryGetValue";
    private const string MethodRemove = "Remove";
    private const string MethodFinishUnityStep = "FinishUnityStep";
    private const string MethodGiveBack = "GiveBack";
    private const string MethodRelease = "Release";
    private const string MethodForget = "Forget";
    private const string MethodAfterCullerChange = "AfterCullerChange";
    private const string MethodApply = "Apply";

    private const string FieldStates = "States";
    private const string FieldBody = "Body";
    private const string FieldArms = "Arms";
    private const string FieldStepScale = "StepScale";
    private const string FieldReleasing = "Releasing";
    private const string FieldEntries = "Entries";

    private const string DumpFullRate = "full-rate animation";
    private const string DumpThrottleRelease = "throttle release";
    private const string DumpPausedRelease = "paused animator release";
    private const string DumpDistantDetail = "distant detail";
    private const string DumpWeaponVisible = "weapon visible";
    private const string DumpLodRefresh = "LOD refresh";

    private const string LabelSlowedAnimator = "a controlled bot may keep a slowed animator for a moment after you take it";
    private const string LabelFrozen = "a bot Smoothie had paused may stay frozen when you take it";
    private const string LabelLowDetail = "a controlled bot far from your character may stay at low detail";
    private const string LabelWeaponHidden = "a controlled bot's weapon may be hidden";
    private const string LabelBuildingsHidden = "buildings may stay partly hidden after entering RTS mode until Smoothie's A/B switch is toggled";

    private const string HarmonySmoothie = ".smoothie";

    public const string GuidSmoothie = "com.tominocz.smoothie";

    private static bool _enabled;
    private static object _states;
    private static MethodInfo _statesTryGet;
    private static MethodInfo _statesRemove;
    private static FieldInfo _stateBody;
    private static FieldInfo _stateArms;
    private static FieldInfo _stateStepScale;
    private static FieldInfo _stateReleasing;
    private static MethodInfo _finishStep;
    private static MethodInfo _giveBack;
    private static MethodInfo _pauseRelease;
    private static IDictionary _detailEntries;
    private static MethodInfo _detailForget;
    private static MethodInfo _handsApply;
    private static MethodInfo _lodShowAll;
    private static bool _lodWarned;

    public static void Enable()
    {
        if (!Chainloader.PluginInfos.ContainsKey(GuidSmoothie))
        {
            return;
        }

        try
        {
            Harmony harmony = new Harmony(Plugin.Guid + HarmonySmoothie);
            Type throttle = AccessTools.TypeByName(TypeAnimatorThrottle);
            Type pause = AccessTools.TypeByName(TypeAnimatorPause);
            Type detail = AccessTools.TypeByName(TypeDistantDetail);
            Type hands = AccessTools.TypeByName(TypeHandsCulling);
            Type lod = AccessTools.TypeByName(TypeLodCulling);
            _lodShowAll = lod != null ? AccessTools.Method(lod, MethodShowAll, Type.EmptyTypes) : null;

            MethodInfo beforeLate = throttle != null ? AccessTools.Method(throttle, MethodBeforeLateUpdateSafe, new[] { typeof(Player) }) : null;
            if (beforeLate == null)
            {
                Plugin.Log.LogWarning("Smoothie found but its bot animation throttle isn't; controlled bots may freeze");
                return;
            }
            harmony.Patch(beforeLate, prefix: new HarmonyMethod(typeof(SmoothieControl), nameof(SkipControlled)));

            Type stateType = AccessTools.Inner(throttle, TypeState);
            _states = AccessTools.Field(throttle, FieldStates)?.GetValue(null);
            _statesTryGet = _states?.GetType().GetMethod(MethodTryGetValue);
            _statesRemove = _states?.GetType().GetMethod(MethodRemove);
            _stateBody = AccessTools.Field(stateType, FieldBody);
            _stateArms = AccessTools.Field(stateType, FieldArms);
            _stateStepScale = AccessTools.Field(stateType, FieldStepScale);
            _stateReleasing = AccessTools.Field(stateType, FieldReleasing);
            _finishStep = AccessTools.Method(throttle, MethodFinishUnityStep);
            _giveBack = AccessTools.Method(throttle, MethodGiveBack);
            _pauseRelease = pause != null ? AccessTools.Method(pause, MethodRelease, new[] { typeof(Animator) }) : null;

            if (detail != null)
            {
                _detailEntries = AccessTools.Field(detail, FieldEntries)?.GetValue(null) as IDictionary;
                _detailForget = AccessTools.Method(detail, MethodForget);
                MethodInfo afterCuller = AccessTools.Method(detail, MethodAfterCullerChange, new[] { typeof(Player) });
                if (afterCuller != null)
                {
                    harmony.Patch(afterCuller, prefix: new HarmonyMethod(typeof(SmoothieControl), nameof(SkipControlled)));
                }
            }

            if (hands != null)
            {
                _handsApply = AccessTools.Method(hands, MethodApply, new[] { typeof(Player), typeof(bool) });
                if (_handsApply != null)
                {
                    harmony.Patch(_handsApply, prefix: new HarmonyMethod(typeof(SmoothieControl), nameof(ShowControlledHands)));
                }
            }

            _enabled = true;
            Report(
                (DumpFullRate, true, null),
                (DumpThrottleRelease, _statesTryGet != null && _stateBody != null && _giveBack != null, LabelSlowedAnimator),
                (DumpPausedRelease, pause == null || _pauseRelease != null, LabelFrozen),
                (DumpDistantDetail, detail == null || (_detailEntries != null && _detailForget != null), LabelLowDetail),
                (DumpWeaponVisible, hands == null || _handsApply != null, LabelWeaponHidden),
                (DumpLodRefresh, lod == null || _lodShowAll != null, LabelBuildingsHidden));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Smoothie: could not hook its bot throttling: {ex.Message}");
        }
    }

    private static void Report(params (string name, bool found, string effect)[] hooks)
    {
        string summary = string.Join(", ", Array.ConvertAll(hooks, hook => $"{hook.name} {(hook.found ? "yes" : "no")}"));
        Plugin.Log.LogInfo($"Smoothie: {summary}");
        foreach ((string name, bool found, string effect) in hooks)
        {
            if (!found)
            {
                Plugin.Log.LogWarning($"Smoothie: this version lacks what Perun uses for {name}; {effect}");
            }
        }
    }

    // Smoothie keeps hiding the LODs of culling groups it saw the game switch off until it sees the game switch them
    // on again. RTS mode switches groups directly, which Smoothie never sees, so it has to re-check after each switch.
    public static void ResyncLodCulling()
    {
        if (_lodShowAll == null)
        {
            if (_enabled && !_lodWarned)
            {
                _lodWarned = true;
                Plugin.Log.LogWarning("Smoothie: couldn't refresh its LOD culling; toggle Smoothie's A/B switch if buildings look incomplete");
            }
            return;
        }
        try
        {
            _lodShowAll.Invoke(null, null);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Smoothie: couldn't refresh its LOD culling: {ex.GetBaseException().Message}");
        }
    }

    private static bool IsControlled(Player player) => player != null && Possession.Body != null && ReferenceEquals(player, Possession.Body);

    private static bool SkipControlled(Player player) => !IsControlled(player);

    private static void ShowControlledHands(Player player, ref bool hide)
    {
        if (hide && IsControlled(player))
        {
            hide = false;
        }
    }

    public static void Free(Player player)
    {
        if (!_enabled || player == null)
        {
            return;
        }

        string name = player.Profile?.Nickname;
        try
        {
            object[] args = { player, null };
            if (_statesTryGet != null && (bool)_statesTryGet.Invoke(_states, args) && args[1] != null)
            {
                object state = args[1];
                if (_stateStepScale != null && (float)_stateStepScale.GetValue(state) > 0f && _finishStep != null)
                {
                    _stateReleasing?.SetValue(state, true);
                    _finishStep.Invoke(null, new[] { state, (object)0f });
                }
                GiveBack(_stateBody?.GetValue(state));
                GiveBack(_stateArms?.GetValue(state));
                _statesRemove?.Invoke(_states, new object[] { player });
                Plugin.Log.LogInfo($"Smoothie: {name}'s animations run at full rate while controlled");
            }

            if (Animators.All(player) is Array animators)
            {
                foreach (object animator in animators)
                {
                    ReleaseAnimator(animator);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Smoothie: couldn't unthrottle {name}: {ex.Message}");
        }

        try
        {
            if (_detailEntries != null && _detailForget != null && _detailEntries.Contains(player))
            {
                _detailForget.Invoke(null, new[] { player, _detailEntries[player] });
                Plugin.Log.LogInfo($"Smoothie: {name} shows full detail while controlled");
            }
            _handsApply?.Invoke(null, new object[] { player, false });
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Smoothie: couldn't restore {name}'s detail: {ex.Message}");
        }
    }

    private static void GiveBack(object animator)
    {
        if (animator != null && _giveBack != null)
        {
            _giveBack.Invoke(null, new[] { animator });
        }
    }

    private static void ReleaseAnimator(object animator)
    {
        if (animator == null || _pauseRelease == null)
        {
            return;
        }
        Animator unity = Animators.Unity(animator);
        if (unity != null)
        {
            _pauseRelease.Invoke(null, new object[] { unity });
            if (!unity.enabled)
            {
                unity.enabled = true;
            }
        }
    }
}
