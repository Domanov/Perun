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

            Harmony harmony = new(Plugin.Guid + ".sain");
            harmony.Patch(update, prefix: new HarmonyMethod(typeof(SainCompat), nameof(UpdatePrefix)));
            Plugin.Log.LogInfo("SAIN: bots pause with the world");
            HookSquadLeader(harmony);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"SAIN: could not hook the bot update: {ex.Message}");
        }
    }

    private static Type _componentType;

    private static MethodInfo _assignLeader;
    private static PropertyInfo _squadBotsGroup;
    private static PropertyInfo _squadLeader;
    private static PropertyInfo _squadMembers;
    private static PropertyInfo _botSquad;
    private static PropertyInfo _squadInfo;
    private static PropertyInfo _componentBotOwner;
    private static bool _assigning;

    private static void HookSquadLeader(Harmony harmony)
    {
        Type squad = AccessTools.TypeByName("SAIN.BotController.Classes.Squad");
        MethodInfo assign = squad != null ? AccessTools.Method(squad, "assignSquadLeader") : null;
        _squadBotsGroup = squad != null ? AccessTools.Property(squad, "BotsGroup") : null;
        _squadLeader = squad != null ? AccessTools.Property(squad, "LeaderComponent") : null;
        _squadMembers = squad != null ? AccessTools.Property(squad, "Members") : null;
        _botSquad = AccessTools.Property(_componentType, "Squad");
        _squadInfo = _botSquad != null ? AccessTools.Property(_botSquad.PropertyType, "SquadInfo") : null;
        _componentBotOwner = AccessTools.Property(_componentType, "BotOwner");
        if (assign == null || _squadBotsGroup == null || _squadLeader == null || _squadMembers == null || _squadInfo == null || _componentBotOwner == null)
        {
            Plugin.Log.LogWarning("SAIN: its squad leader wasn't found; SAIN may lead Perun's groups with another member");
            return;
        }

        harmony.Patch(assign, postfix: new HarmonyMethod(typeof(SainCompat), nameof(AssignLeaderPostfix)));
        _assignLeader = assign;
        Plugin.Log.LogInfo("SAIN: Perun's group leaders lead their SAIN squads");
    }

    // SAIN picks its own squad leader (a boss, else the strongest member) when the squad forms, when a stronger member
    // joins and after the leader dies; for Perun's groups the leader Perun chose is put back.
    private static void AssignLeaderPostfix(object __instance) => Redirect(__instance);

    private static MethodInfo _addMember;
    private static PropertyInfo _enemyController;
    private static MethodInfo _removeEnemy;

    // SAIN keeps its own enemy list per bot and drops an entry only when the bot's original BotsGroup removes that enemy,
    // so a bot moved into a group it used to fight keeps shooting its new squad.
    public static void ForgetEnemy(EFT.BotOwner bot, EFT.Player enemy)
    {
        object component = ComponentOf(bot);
        if (component == null)
        {
            return;
        }
        try
        {
            _enemyController ??= AccessTools.Property(_componentType, "EnemyController");
            object controller = _enemyController?.GetValue(component);
            if (controller == null)
            {
                return;
            }
            _removeEnemy ??= AccessTools.Method(controller.GetType(), "RemoveEnemy", new[] { typeof(string) });
            _removeEnemy?.Invoke(controller, new object[] { enemy.ProfileId });
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"SAIN: couldn't clear {enemy.Profile?.Nickname} from {bot.Profile?.Nickname}'s enemies: {ex.GetBaseException().Message}");
        }
    }

    // SAIN moves a bot that leaves a BotsGroup to its new group's squad but doesn't add it as a member there.
    public static void JoinSquad(EFT.BotOwner bot)
    {
        if (_assignLeader == null)
        {
            return;
        }
        try
        {
            object component = ComponentOf(bot);
            object container = component != null ? _botSquad.GetValue(component) : null;
            object squad = container != null ? _squadInfo.GetValue(container) : null;
            if (squad == null || _squadMembers.GetValue(squad) is not System.Collections.IDictionary members || members.Contains(bot.ProfileId))
            {
                return;
            }
            _addMember ??= AccessTools.Method(squad.GetType(), "AddMember", new[] { _componentType });
            _addMember?.Invoke(squad, new[] { component });
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"SAIN: couldn't add {bot.Profile?.Nickname} to its new squad: {ex.GetBaseException().Message}");
        }
    }

    public static void MatchLeader(EFT.BotOwner leader)
    {
        if (_assignLeader == null)
        {
            return;
        }
        object component = ComponentOf(leader);
        object container = component != null ? _botSquad.GetValue(component) : null;
        Redirect(container != null ? _squadInfo.GetValue(container) : null);
    }

    private static void Redirect(object squad)
    {
        if (_assigning || squad == null)
        {
            return;
        }
        try
        {
            EFT.BotOwner wanted = Units.GroupLeadership.LeaderOf(_squadBotsGroup.GetValue(squad) as BotsGroup);
            object current = _squadLeader.GetValue(squad);
            if (wanted == null || (current != null && ReferenceEquals(_componentBotOwner.GetValue(current), wanted)))
            {
                return;
            }
            if (_squadMembers.GetValue(squad) is not System.Collections.IDictionary members || !members.Contains(wanted.ProfileId))
            {
                return;
            }
            _assigning = true;
            _assignLeader.Invoke(squad, new[] { members[wanted.ProfileId] });
            Plugin.Log.LogInfo($"SAIN: {wanted.Profile.Nickname} leads the squad");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"SAIN: couldn't set the squad leader: {ex.GetBaseException().Message}");
        }
        finally
        {
            _assigning = false;
        }
    }

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
