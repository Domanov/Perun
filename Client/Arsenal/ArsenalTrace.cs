using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Perun.Client.Mode;
using UnityEngine;

namespace Perun.Client.Arsenal;

// Verbose logging only. Records each arsenal step with the unit's hands and animator state, then keeps sampling the
// unit for a while after a change, because broken hands (spinning, stuck draw) show up after the arsenal is done.
internal static class ArsenalTrace
{
    private const float WatchSeconds = 20f;
    private const int SampleMs = 200;
    private const float HeartbeatSeconds = 2f;
    private const int MaxLinesPerWatch = 120;

    private static readonly Dictionary<Player, float> WatchUntil = new();

    public static bool On => Plugin.Verbose.Value;

    public static void Log(Player unit, string what)
    {
        if (On)
        {
            Plugin.Log.LogInfo($"Arsenal trace [{Name(unit)}] {what} | {Hands(unit)}");
        }
    }

    public static void Note(Player unit, string what)
    {
        if (On)
        {
            Plugin.Log.LogInfo($"Arsenal trace [{Name(unit)}] {what}");
        }
    }

    public static string Name(Player unit) => unit?.Profile?.Nickname ?? "?";

    public static string Item(Item item) => item == null ? "none" : $"{item.ShortName.Localized()} ({item.TemplateId}, {item.Id})";

    public static void Watch(Player unit, string reason)
    {
        if (!On || unit == null)
        {
            return;
        }
        bool running = WatchUntil.ContainsKey(unit);
        WatchUntil[unit] = Time.time + WatchSeconds;
        Note(unit, $"watching hands for {WatchSeconds:0} s after {reason}");
        if (!running)
        {
            _ = Sample(unit);
        }
    }

    private static async Task Sample(Player unit)
    {
        string last = null;
        float nextHeartbeat = 0f;
        int lines = 0;
        int changes = 0;
        try
        {
            while (WatchUntil.TryGetValue(unit, out float until) && Time.time < until && lines < MaxLinesPerWatch)
            {
                if (unit == null || unit.HealthController == null || !unit.HealthController.IsAlive)
                {
                    Note(unit, "watch ended: unit gone or dead");
                    break;
                }
                string signature = Signature(unit);
                if (signature != last)
                {
                    changes++;
                    Plugin.Log.LogInfo($"Arsenal trace [{Name(unit)}] watch change #{changes} | {Hands(unit)} | animators {Animators(unit, false)}");
                    last = signature;
                    lines++;
                    nextHeartbeat = Time.time + HeartbeatSeconds;
                }
                else if (Time.time >= nextHeartbeat)
                {
                    Plugin.Log.LogInfo($"Arsenal trace [{Name(unit)}] watch | {Hands(unit)} | animators {Animators(unit, true)}");
                    lines++;
                    nextHeartbeat = Time.time + HeartbeatSeconds;
                }
                await Task.Delay(SampleMs, RaidScope.Token);
            }
            if (lines >= MaxLinesPerWatch)
            {
                Note(unit, $"watch stopped after {lines} lines ({changes} changes): hands kept changing");
            }
            else
            {
                Note(unit, $"watch done, {changes} change(s)");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Arsenal trace watch failed: {ex.Message}");
        }
        finally
        {
            WatchUntil.Remove(unit);
        }
    }

    private static string Signature(Player unit) => Hands(unit) + "|" + Animators(unit, false);

    public static string Hands(Player unit)
    {
        if (unit == null)
        {
            return "no unit";
        }
        try
        {
            StringBuilder text = new();
            var hands = unit.HandsController;
            text.Append("hands ").Append(hands == null ? "null" : hands.GetType().Name);
            Item held = hands?.Item;
            text.Append(" item ").Append(Item(held));
            if (held != null)
            {
                text.Append(" at ").Append(Where(unit, held));
            }
            if (hands is Player.FirearmController firearm)
            {
                text.Append(" op ").Append(firearm.CurrentOperation?.GetType().Name ?? "-");
                text.Append(firearm.IsInRemoveOperation() ? " removing" : "");
            }
            BotOwner bot = unit.AIData?.BotOwner;
            if (bot != null && bot.WeaponManager != null)
            {
                BotWeaponManager manager = bot.WeaponManager;
                text.Append($"; bot ready {manager.IsReady} weaponReady {manager.IsWeaponReady}");
                if (manager.Selector != null)
                {
                    text.Append($" changing {manager.Selector.IsChanging} selReady {manager.Selector.IsWeaponReady}");
                }
                text.Append($" mainSlot {SlotWeapon(unit, EquipmentSlot.FirstPrimaryWeapon)}/{SlotWeapon(unit, EquipmentSlot.SecondPrimaryWeapon)}/{SlotWeapon(unit, EquipmentSlot.Holster)}");
            }
            return text.ToString();
        }
        catch (Exception ex)
        {
            return $"hands unreadable ({ex.GetType().Name}: {ex.Message})";
        }
    }

    private static string SlotWeapon(Player unit, EquipmentSlot slot) => ArsenalOps.InSlot(unit, slot)?.ShortName.Localized() ?? "-";

    public static string Where(Player unit, Item item)
    {
        if (item.CurrentAddress == null)
        {
            return "nowhere";
        }
        InventoryEquipment equipment = unit.InventoryController?.Inventory?.Equipment;
        bool owned = equipment != null && equipment.GetAllItems().Contains(item);
        return owned ? item.CurrentAddress.ToString() : $"NOT IN THE UNIT'S INVENTORY ({item.CurrentAddress})";
    }

    private static string Animators(Player unit, bool times)
    {
        Array all = Compat.Animators.All(unit);
        if (all == null)
        {
            return "unavailable";
        }
        StringBuilder text = new();
        for (int i = 0; i < all.Length; i++)
        {
            Animator animator = Compat.Animators.Unity(all.GetValue(i));
            text.Append(i == 0 ? "" : " ; ").Append('#').Append(i).Append(' ');
            if (animator == null)
            {
                text.Append("none");
                continue;
            }
            text.Append(animator.enabled ? "on" : "OFF");
            if (!animator.isActiveAndEnabled)
            {
                text.Append(" inactive");
                continue;
            }
            if (times)
            {
                text.Append($" speed {animator.speed:0.##}");
            }
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                float weight = layer == 0 ? 1f : animator.GetLayerWeight(layer);
                if (weight <= 0.01f)
                {
                    continue;
                }
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
                text.Append($" L{layer}:{state.fullPathHash:X8}");
                if (animator.IsInTransition(layer))
                {
                    text.Append($">{animator.GetNextAnimatorStateInfo(layer).fullPathHash:X8}");
                }
                if (times)
                {
                    text.Append($"@{state.normalizedTime:0.##}");
                }
            }
        }
        return text.ToString();
    }
}
