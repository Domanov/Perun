using System;
using System.Reflection;
using BepInEx.Configuration;
using EFT;
using HarmonyLib;
using Perun.Client.Mode;
using static Perun.Client.Compat.Constants.FovFixCompat;

namespace Perun.Client.Compat;

// Fontaine's FOV Fix computes aim FOV from, and applies its weapon FOV scale to, GameWorld.MainPlayer: your parked
// character, not the bot you control.
internal static class FovFixCompat
{
    private static MethodInfo _setScale;
    private static PropertyInfo _controller;
    private static FieldInfo _controllerPlayer;
    private static ConfigEntry<bool> _enabled;
    private static ConfigEntry<float> _scale;

    public static void Enable()
    {
        Type patch = AccessTools.TypeByName(TypeScalePatch);
        _setScale = patch != null ? AccessTools.Method(patch, MethodSetCompensationScale, new[] { typeof(float) }) : null;
        if (_setScale == null)
        {
            return;
        }
        try
        {
            new Harmony(Plugin.Guid + HarmonyFovFix).Patch(_setScale, prefix: new HarmonyMethod(typeof(FovFixCompat), nameof(ScaleYourBody)));
            Type plugin = AccessTools.TypeByName(TypeFovFixPlugin);
            _controller = plugin != null ? AccessTools.Property(plugin, PropertyFovController) : null;
            _controllerPlayer = _controller != null ? AccessTools.Field(_controller.PropertyType, FieldPlayer) : null;
            _enabled = plugin != null ? AccessTools.Property(plugin, PropertyEnableFovScaleFix)?.GetValue(null) as ConfigEntry<bool> : null;
            _scale = plugin != null ? AccessTools.Property(plugin, PropertyFovScale)?.GetValue(null) as ConfigEntry<float> : null;
            Plugin.Log.LogInfo("FOV Fix: its weapon FOV scale follows the bot you control");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"FOV Fix: could not hook its FOV scale: {ex.Message}");
        }
    }

    public static void Follow(Player body)
    {
        if (_setScale == null || body == null)
        {
            return;
        }
        try
        {
            object controller = _controller?.GetValue(null);
            if (controller != null && _controllerPlayer != null)
            {
                _controllerPlayer.SetValue(controller, body);
            }
            if (_enabled?.Value == true && _scale != null)
            {
                Apply(body, _scale.Value);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"FOV Fix: could not switch to {body.Profile?.Nickname}: {ex.Message}");
        }
    }

    private static bool ScaleYourBody(float scale)
    {
        Player body = Possession.Body;
        if (body == null || !body.IsYourPlayer)
        {
            return true;
        }
        Apply(body, scale);
        return false;
    }

    private static void Apply(Player body, float scale)
    {
        body.RibcageScaleCurrentTarget = scale;
        body.RibcageScaleCurrent = scale;
        body.ProceduralWeaponAnimation.ResetFovAdjustments(body);
        body.ProceduralWeaponAnimation.SetFovParams(scale);
    }
}
