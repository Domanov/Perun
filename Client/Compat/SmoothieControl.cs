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
    public const string PluginGuid = "com.tominocz.smoothie";

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
        if (!Chainloader.PluginInfos.ContainsKey(PluginGuid))
        {
            return;
        }

        try
        {
            Harmony harmony = new Harmony(Plugin.Guid + ".smoothie");
            Type throttle = AccessTools.TypeByName("Smoothie.AnimatorThrottle");
            Type pause = AccessTools.TypeByName("Smoothie.AnimatorPause");
            Type detail = AccessTools.TypeByName("Smoothie.DistantDetail");
            Type hands = AccessTools.TypeByName("Smoothie.HandsCulling");
            Type lod = AccessTools.TypeByName("Smoothie.LodCulling");
            _lodShowAll = lod != null ? AccessTools.Method(lod, "ShowAll", Type.EmptyTypes) : null;

            MethodInfo beforeLate = throttle != null ? AccessTools.Method(throttle, "BeforeLateUpdateSafe", new[] { typeof(Player) }) : null;
            if (beforeLate == null)
            {
                Plugin.Log.LogWarning("Smoothie found but its bot animation throttle isn't; controlled bots may freeze");
                return;
            }
            harmony.Patch(beforeLate, prefix: new HarmonyMethod(typeof(SmoothieControl), nameof(SkipControlled)));

            Type stateType = AccessTools.Inner(throttle, "State");
            _states = AccessTools.Field(throttle, "States")?.GetValue(null);
            _statesTryGet = _states?.GetType().GetMethod("TryGetValue");
            _statesRemove = _states?.GetType().GetMethod("Remove");
            _stateBody = AccessTools.Field(stateType, "Body");
            _stateArms = AccessTools.Field(stateType, "Arms");
            _stateStepScale = AccessTools.Field(stateType, "StepScale");
            _stateReleasing = AccessTools.Field(stateType, "Releasing");
            _finishStep = AccessTools.Method(throttle, "FinishUnityStep");
            _giveBack = AccessTools.Method(throttle, "GiveBack");
            _pauseRelease = pause != null ? AccessTools.Method(pause, "Release", new[] { typeof(Animator) }) : null;

            if (detail != null)
            {
                _detailEntries = AccessTools.Field(detail, "Entries")?.GetValue(null) as IDictionary;
                _detailForget = AccessTools.Method(detail, "Forget");
                MethodInfo afterCuller = AccessTools.Method(detail, "AfterCullerChange", new[] { typeof(Player) });
                if (afterCuller != null)
                {
                    harmony.Patch(afterCuller, prefix: new HarmonyMethod(typeof(SmoothieControl), nameof(SkipControlled)));
                }
            }

            if (hands != null)
            {
                _handsApply = AccessTools.Method(hands, "Apply", new[] { typeof(Player), typeof(bool) });
                if (_handsApply != null)
                {
                    harmony.Patch(_handsApply, prefix: new HarmonyMethod(typeof(SmoothieControl), nameof(ShowControlledHands)));
                }
            }

            _enabled = true;
            Report(
                ("full-rate animation", true, null),
                ("throttle release", _statesTryGet != null && _stateBody != null && _giveBack != null, "a controlled bot may keep a slowed animator for a moment after you take it"),
                ("paused animator release", pause == null || _pauseRelease != null, "a bot Smoothie had paused may stay frozen when you take it"),
                ("distant detail", detail == null || (_detailEntries != null && _detailForget != null), "a controlled bot far from your character may stay at low detail"),
                ("weapon visible", hands == null || _handsApply != null, "a controlled bot's weapon may be hidden"),
                ("LOD refresh", lod == null || _lodShowAll != null, "buildings may stay partly hidden after entering RTS mode until Smoothie's A/B switch is toggled"));
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
