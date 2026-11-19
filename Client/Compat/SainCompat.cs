using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using Perun.Client.Mode;

namespace Perun.Client.Compat;

internal static class SainCompat
{
    public const string PluginGuid = "me.sol.sain";
    private const string BotComponentType = "SAIN.Components.BotComponent";

    public static void Enable()
    {
        if (!Chainloader.PluginInfos.ContainsKey(PluginGuid))
        {
            return;
        }

        try
        {
            Type component = AccessTools.TypeByName(BotComponentType);
            _componentType = component;
            MethodInfo update = component != null ? AccessTools.Method(component, "ManualUpdate", new[] { typeof(float), typeof(float) }) : null;
            if (update == null)
            {
                Plugin.Log.LogWarning("SAIN found but its bot update isn't; SAIN bots may keep acting during a pause");
                return;
            }

            new Harmony(Plugin.Guid + ".sain").Patch(update, prefix: new HarmonyMethod(typeof(SainCompat), nameof(UpdatePrefix)));
            Plugin.Log.LogInfo("SAIN: bots pause with the world");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"SAIN: could not hook the bot update: {ex.Message}");
        }
    }

    private static Type _componentType;

    private static bool UpdatePrefix(object __instance) => !WorldPause.Paused && (Possession.SainComponent == null || !ReferenceEquals(__instance, Possession.SainComponent));

    private static PropertyInfo _stuckProperty;
    private static FieldInfo _dontUnstuckField;
    private static FieldInfo _unstuckCoroutineField;
    private static bool _unstuckResolved;

    public static bool SuspendUnstuck(object component)
    {
        object stuck = UnstuckOf(component);
        if (stuck == null)
        {
            return false;
        }

        try
        {
            bool before = (bool)_dontUnstuckField.GetValue(stuck);
            _dontUnstuckField.SetValue(stuck, true);
            if (_unstuckCoroutineField.GetValue(stuck) is UnityEngine.Coroutine routine && component is UnityEngine.MonoBehaviour behaviour)
            {
                behaviour.StopCoroutine(routine);
            }
            _unstuckCoroutineField.SetValue(stuck, null);
            return before;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"SAIN: could not pause unstuck: {ex.Message}");
            return false;
        }
    }

    public static void ResumeUnstuck(object component, bool dontUnstuck)
    {
        object stuck = UnstuckOf(component);
        if (stuck == null)
        {
            return;
        }

        try
        {
            _dontUnstuckField.SetValue(stuck, dontUnstuck);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"SAIN: could not resume unstuck: {ex.Message}");
        }
    }

    private static object UnstuckOf(object component)
    {
        if (component == null || _componentType == null)
        {
            return null;
        }

        if (!_unstuckResolved)
        {
            _unstuckResolved = true;
            _stuckProperty = AccessTools.Property(_componentType, "BotStuck");
            Type stuckType = _stuckProperty?.PropertyType;
            _dontUnstuckField = stuckType != null ? AccessTools.Field(stuckType, "DontUnstuckMe") : null;
            _unstuckCoroutineField = stuckType != null ? AccessTools.Field(stuckType, "_botUnstuckCoroutine") : null;
            if (_dontUnstuckField == null || _unstuckCoroutineField == null)
            {
                Plugin.Log.LogWarning("SAIN: unstuck not found, a controlled bot may get teleported by SAIN");
                _stuckProperty = null;
            }
        }

        try
        {
            return _stuckProperty?.GetValue(component);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static object ComponentOf(EFT.BotOwner bot)
    {
        if (_componentType == null || bot == null)
        {
            return null;
        }
        return bot.GetComponent(_componentType);
    }
}
