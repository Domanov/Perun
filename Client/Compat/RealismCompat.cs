using System;
using System.Reflection;
using EFT;
using HarmonyLib;
using Perun.Client.Mode;

namespace Perun.Client.Compat;

// Fontaine's Realism library reads aiming, firing and weapon state from one player set at raid start. Stance Overhaul
// goes back to the stance chosen on that player after every shot, so the bot also starts with no stance chosen.
// The library also counts the raid as running only while your own character is "your player", and Stance Overhaul
// stops updating (stance keys, poses) when it isn't.
internal static class RealismCompat
{
    private const string PluginType = "RealismCommonLib.Plugin";
    private const string StancePluginType = "StanceOverhaul.Plugin";
    private const string GameStateType = "RealismCommonLib.StateControllers.GlobalState.GameState";

    private static FieldInfo _state;
    private static PropertyInfo _player;
    private static FieldInfo _stanceController;
    private static FieldInfo _inputHandler;
    private static MethodInfo _setChosen;
    private static MethodInfo _reconcile;
    private static MethodInfo _setReady;

    public static void Follow(Player body)
    {
        if (_player == null || !_player.CanWrite || body == null)
        {
            return;
        }
        try
        {
            object state = _state.GetValue(null);
            if (state != null)
            {
                _player.SetValue(state, body);
                ClearStance();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Realism: could not switch its player to {body.Profile?.Nickname}: {ex.Message}");
        }
    }

    private static void ClearStance()
    {
        object controller = _stanceController?.GetValue(null);
        object handler = controller != null ? _inputHandler?.GetValue(controller) : null;
        if (handler == null || _setChosen == null || _reconcile == null)
        {
            return;
        }
        _setChosen.Invoke(handler, new object[] { null });
        _reconcile.Invoke(handler, null);
    }

    private static void KeepRaidRunning(object __instance)
    {
        Player body = Possession.Body;
        if (body != null && body.IsYourPlayer)
        {
            _setReady.Invoke(__instance, new object[] { true });
        }
    }

    public static void Enable()
    {
        Type plugin = AccessTools.TypeByName(PluginType);
        if (plugin == null)
        {
            return;
        }

        Type gameState = AccessTools.TypeByName(GameStateType);
        MethodInfo check = gameState != null ? AccessTools.Method(gameState, "CheckIsReady") : null;
        _setReady = gameState != null ? AccessTools.PropertySetter(gameState, "PlayerIsReady") : null;
        if (check != null && _setReady != null)
        {
            new Harmony(Plugin.Guid + ".realism").Patch(check, postfix: new HarmonyMethod(typeof(RealismCompat), nameof(KeepRaidRunning)));
        }
        else
        {
            Plugin.Log.LogWarning("Realism: its raid check wasn't found; Stance Overhaul may stop working on a controlled bot");
        }
        _state = AccessTools.Field(plugin, "PlayerStateInstance");
        _player = _state != null ? AccessTools.Property(_state.FieldType, "Player") : null;
        if (_player == null)
        {
            Plugin.Log.LogWarning("Realism: its player state wasn't found; stances may misbehave on a controlled bot");
        }

        Type stancePlugin = AccessTools.TypeByName(StancePluginType);
        if (stancePlugin == null)
        {
            return;
        }
        _stanceController = AccessTools.Field(stancePlugin, "StanceControllerInstance");
        _inputHandler = _stanceController != null ? AccessTools.Field(_stanceController.FieldType, "_inputHandler") : null;
        _setChosen = _inputHandler != null ? AccessTools.Method(_inputHandler.FieldType, "SetChosen") : null;
        _reconcile = _inputHandler != null ? AccessTools.Method(_inputHandler.FieldType, "Reconcile") : null;
        if (_setChosen == null || _reconcile == null)
        {
            Plugin.Log.LogWarning("Stance Overhaul: its stance input wasn't found; a controlled bot may keep your character's stance");
        }
    }
}
