using System;
using System.Collections.Generic;
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

    private static bool _weaponsResolved;
    private static Type _playerComponentType;
    private static PropertyInfo _equipmentProperty;
    private static PropertyInfo _weaponInfosProperty;
    private static MethodInfo _getAllWeapons;
    private static MethodInfo _recalcPower;

    // SAIN fills its per-slot weapon infos when the bot spawns; Perun's arsenal swaps guns afterwards.
    public static string RefreshWeapons(EFT.Player player)
    {
        if (_componentType == null || player == null)
        {
            return null;
        }
        if (!_weaponsResolved)
        {
            _weaponsResolved = true;
            _playerComponentType = AccessTools.TypeByName("SAIN.Components.PlayerComponentSpace.PlayerComponent");
            _equipmentProperty = _playerComponentType != null ? AccessTools.Property(_playerComponentType, "Equipment") : null;
            Type equipment = _equipmentProperty?.PropertyType;
            _weaponInfosProperty = equipment != null ? AccessTools.Property(equipment, "WeaponInfos") : null;
            _getAllWeapons = equipment != null ? AccessTools.Method(equipment, "getAllWeapons") : null;
            _recalcPower = equipment != null ? AccessTools.Method(equipment, "ReCalcPowerOfEquipment") : null;
            if (_weaponInfosProperty == null || _getAllWeapons == null)
            {
                Plugin.Log.LogWarning("SAIN: its weapon list wasn't found, bots given new guns by the arsenal may keep switching weapons");
            }
        }
        if (_weaponInfosProperty == null || _getAllWeapons == null)
        {
            return null;
        }

        try
        {
            object component = player.GetComponent(_playerComponentType) ?? player.GetComponentInChildren(_playerComponentType);
            object equipment = component != null ? _equipmentProperty.GetValue(component) : null;
            if (equipment == null)
            {
                return "no SAIN component";
            }
            // getAllWeapons replaces an entry whose slot holds a different gun but never drops one whose slot is now
            // empty. SAIN's distance-based weapon choice then keeps asking for the removed gun (it still has a round
            // chambered), so those entries are disposed and removed here first.
            int dropped = 0;
            if (_weaponInfosProperty.GetValue(equipment) is System.Collections.IDictionary infos)
            {
                EFT.InventoryLogic.InventoryEquipment gear = player.InventoryController?.Inventory?.Equipment;
                List<object> stale = new();
                foreach (System.Collections.DictionaryEntry entry in infos)
                {
                    object held = entry.Value != null ? AccessTools.Property(entry.Value.GetType(), "Weapon")?.GetValue(entry.Value) : null;
                    object inSlot = gear?.GetSlot((EFT.InventoryLogic.EquipmentSlot)entry.Key)?.ContainedItem;
                    if (held == null || !ReferenceEquals(held, inSlot))
                    {
                        stale.Add(entry.Key);
                    }
                }
                foreach (object slot in stale)
                {
                    object info = infos[slot];
                    if (info != null)
                    {
                        AccessTools.Method(info.GetType(), "Dispose")?.Invoke(info, null);
                    }
                    infos.Remove(slot);
                    dropped++;
                }
            }
            _getAllWeapons.Invoke(equipment, null);
            _recalcPower?.Invoke(equipment, null);
            int count = _weaponInfosProperty.GetValue(equipment) is System.Collections.IDictionary refreshed ? refreshed.Count : -1;
            return $"dropped {dropped} stale, now {count} weapon(s)";
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"SAIN: couldn't refresh {player.Profile?.Nickname}'s weapons: {ex.GetBaseException().Message}");
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
