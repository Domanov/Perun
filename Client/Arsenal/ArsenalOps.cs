using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using Diz.Jobs;
using EFT;
using EFT.InventoryLogic;
using Perun.Client.Compat;
using Perun.Client.Mode;
using UnityEngine;
using static Perun.Client.Arsenal.Constants.ArsenalOps;

namespace Perun.Client.Arsenal;

internal static class ArsenalOps
{
    private static readonly EquipmentSlot[] SlotOrder =
    {
        EquipmentSlot.FirstPrimaryWeapon, EquipmentSlot.SecondPrimaryWeapon, EquipmentSlot.Holster, EquipmentSlot.Scabbard,
        EquipmentSlot.Headwear, EquipmentSlot.Earpiece, EquipmentSlot.FaceCover, EquipmentSlot.Eyewear, EquipmentSlot.ArmBand,
        EquipmentSlot.ArmorVest, EquipmentSlot.TacticalVest, EquipmentSlot.Backpack, EquipmentSlot.SecuredContainer
    };

    private static readonly EquipmentSlot[] ContainerOrder =
    {
        EquipmentSlot.TacticalVest, EquipmentSlot.Pockets, EquipmentSlot.Backpack, EquipmentSlot.SecuredContainer
    };

    public static Item Build(string templateId, int count = 1)
    {
        ItemFactory factory = Singleton<ItemFactory>.Instance;
        if (factory == null || string.IsNullOrEmpty(templateId) || !factory.ItemTemplates.ContainsKey(templateId))
        {
            return null;
        }

        Item item = factory.GetPresetItem(templateId);
        if (item != null && count > 1 && item.StackMaxSize > 1)
        {
            item.StackObjectsCount = Mathf.Clamp(count, 1, item.StackMaxSize);
        }
        return item;
    }

    public static ArsenalResult Blocked(Player target)
    {
        if (target == null || target.HealthController == null || !target.HealthController.IsAlive)
        {
            return ArsenalResult.Invalid(LabelUnitGone);
        }
        if (target.InventoryController == null)
        {
            return ArsenalResult.Invalid(LabelNoInventory);
        }
        if (ReferenceEquals(target, Possession.Body))
        {
            return ArsenalResult.Busy(LabelGiveBackFirst);
        }
        return ArsenalResult.Ok;
    }

    public static async Task<ArsenalResult> GiveAsync(Player target, Item tree, EquipmentSlot? slot, bool equip = true)
    {
        try
        {
            ArsenalTrace.Log(target, $"give {ArsenalTrace.Item(tree)} to {(slot.HasValue ? slot.Value.ToString() : "anywhere")}, equip {equip}");
            ArsenalResult result = tree == null ? ArsenalResult.Invalid(LabelUnknownItem) : Blocked(target);
            if (result.IsOk)
            {
                await Load(tree);
                result = Blocked(target);
            }
            Held held = null;
            if (result.IsOk && slot.HasValue && SlotOf(target, slot.Value) is Slot occupied && occupied.ContainedItem != null)
            {
                if (!occupied.CheckCompatibility(tree))
                {
                    return ArsenalResult.Incompatible(string.Format(LabelWrongSlot, tree.ShortName.Localized()));
                }
                (result, held) = await HoldAsync(target, slot.Value);
            }
            if (result.IsOk)
            {
                result = Place(target, tree, slot);
                string lost = !result.IsOk && held != null ? Restore(target, held) : null;
                if (lost != null)
                {
                    return ArsenalResult.RollbackFailed(string.Format(LabelNotRestored, result.Message, lost));
                }
            }
            if (result.IsOk && equip && tree is Weapon && HandsEmpty(target))
            {
                await EquipBest(target);
            }
            ArsenalTrace.Log(target, $"give {ArsenalTrace.Item(tree)} done: {result.Status} {result.Message}");
            if (equip)
            {
                ArsenalTrace.Watch(target, $"giving {tree?.ShortName.Localized()}");
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            return ArsenalResult.Cancelled();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Arsenal give failed: {ex}");
            return ArsenalResult.Failed(LabelFailedSeeLog);
        }
    }

    public static async Task<ArsenalResult> ClearSlotAsync(Player target, EquipmentSlot slot)
    {
        Item item = SlotOf(target, slot)?.ContainedItem;
        if (item == null)
        {
            return ArsenalResult.Ok;
        }
        try
        {
            ArsenalResult blocked = Blocked(target);
            ArsenalResult result = blocked.IsOk ? await RemoveAsync(target, item) : blocked;
            ArsenalTrace.Watch(target, $"clearing {slot}");
            return result;
        }
        catch (OperationCanceledException)
        {
            return ArsenalResult.Cancelled();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Arsenal clear failed: {ex}");
            return ArsenalResult.Failed(LabelFailedSeeLog);
        }
    }

    internal sealed class Held
    {
        public Held(Item item, EquipmentSlot slot, Grid grid)
        {
            Item = item;
            Slot = slot;
            Grid = grid;
        }

        public Item Item { get; }

        public EquipmentSlot Slot { get; }

        public Grid Grid { get; }
    }

    internal sealed class HeldContent
    {
        public HeldContent(Item item, ItemAddress from, Grid grid)
        {
            Item = item;
            From = from;
            Grid = grid;
        }

        public Item Item { get; }

        public ItemAddress From { get; }

        public Grid Grid { get; }
    }

    private static async Task<(ArsenalResult result, Grid grid)> MoveToHolding(Player target, Item item)
    {
        ArsenalResult result = await EmptyHandsIfHolding(target, item);
        if (!result.IsOk)
        {
            return (result, null);
        }

        ArsenalTrace.Log(target, $"holding {ArsenalTrace.Item(item)} from {item.CurrentAddress}");
        InventoryController controller = target.InventoryController;
        Stash stash = Singleton<ItemFactory>.Instance.CreateFakeStash();
        new ItemController(stash, controller.ID, ObjectHoldController, false, EOwnerType.Profile);
        ItemAddress address = stash.Grid.FindLocationForItem(item);
        if (address == null || ItemManipulator.Move(item, address, controller, true).Failed)
        {
            return (ArsenalResult.Failed(string.Format(LabelCouldNotTakeOut, item.ShortName.Localized())), null);
        }
        var moved = ItemManipulator.Move(item, address, controller, false);
        if (moved.Failed)
        {
            return (ArsenalResult.Failed(string.Format(LabelCouldNotTakeOutError, item.ShortName.Localized(), moved.Error)), null);
        }
        AfterChange(target);
        return (ArsenalResult.Ok, stash.Grid);
    }

    public static async Task<(ArsenalResult result, Held held)> HoldAsync(Player target, EquipmentSlot slot)
    {
        Item item = InSlot(target, slot);
        if (item == null)
        {
            return (ArsenalResult.Ok, null);
        }
        (ArsenalResult result, Grid grid) = await MoveToHolding(target, item);
        return (result, result.IsOk ? new Held(item, slot, grid) : null);
    }

    public static async Task<(ArsenalResult result, HeldContent held)> HoldContentAsync(Player target, Item item)
    {
        ItemAddress from = item?.CurrentAddress;
        if (from == null)
        {
            return (ArsenalResult.Invalid(LabelItemNowhere), null);
        }
        (ArsenalResult result, Grid grid) = await MoveToHolding(target, item);
        return (result, result.IsOk ? new HeldContent(item, from, grid) : null);
    }

    public static string Restore(Player target, Held held)
    {
        if (held == null)
        {
            return null;
        }
        ItemAddress address = SlotOf(target, held.Slot)?.CreateItemAddress();
        string error = address == null ? LabelNoSuchSlot : TryMove(target, held.Item, address);
        if (error == null)
        {
            Plugin.Log.LogInfo($"Arsenal: put {held.Item.ShortName.Localized()} back on {target.Profile?.Nickname}'s {held.Slot}");
            return null;
        }
        Plugin.Log.LogError($"Arsenal: couldn't put {held.Item.ShortName.Localized()} back on {target.Profile?.Nickname}'s {held.Slot}: {error}");
        return Drop(target, held.Item, held.Grid);
    }

    public static string RestoreContent(Player target, HeldContent held, CompoundItem container)
    {
        if (held == null)
        {
            return null;
        }
        string error = TryMove(target, held.Item, held.From);
        if (error != null && container != null)
        {
            ItemAddress elsewhere = FindInContainer(container, held.Item, target.InventoryController);
            error = elsewhere == null ? error : TryMove(target, held.Item, elsewhere);
        }
        if (error == null)
        {
            return null;
        }
        Plugin.Log.LogError($"Arsenal: couldn't put {held.Item.ShortName.Localized()} back into {target.Profile?.Nickname}'s {container?.ShortName.Localized()}: {error}");
        return Drop(target, held.Item, held.Grid);
    }

    private static string TryMove(Player target, Item item, ItemAddress address)
    {
        var moved = ItemManipulator.Move(item, address, target.InventoryController, false);
        if (moved.Failed)
        {
            return moved.Error?.ToString() ?? LabelUnknownError;
        }
        AfterChange(target);
        return null;
    }

    private static string Drop(Player target, Item item, Grid grid)
    {
        string name = item.ShortName.Localized();
        try
        {
            if (grid != null && !grid.Remove(item, false).Failed && Singleton<GameWorld>.Instantiated && Singleton<GameWorld>.Instance.ThrowItem(item, target, null) != null)
            {
                Plugin.Log.LogWarning($"Arsenal: dropped {name} next to {target.Profile?.Nickname} instead");
                return string.Format(LabelDroppedNextToUnit, name);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Arsenal: couldn't drop {name} either: {ex.Message}");
        }
        return string.Format(LabelLost, name);
    }

    private static async Task<ArsenalResult> EmptyHandsIfHolding(Player target, Item item)
    {
        Item inHands = target.HandsController?.Item;
        if (inHands == null || (!ReferenceEquals(inHands, item) && !item.GetAllItems().Contains(inHands)))
        {
            return ArsenalResult.Ok;
        }
        ArsenalTrace.Log(target, $"emptying hands to take {ArsenalTrace.Item(item)}");
        TaskCompletionSource<bool> emptied = new();
        target.SetEmptyHands(done =>
        {
            ArsenalTrace.Log(target, $"hands emptied: {(done.Failed ? $"FAILED {done.Error}" : "ok")}");
            emptied.TrySetResult(true);
        });
        Task finished = await Task.WhenAny(emptied.Task, Task.Delay(4000, RaidScope.Token));
        if (finished != emptied.Task)
        {
            ArsenalTrace.Log(target, "emptying hands timed out after 4 s");
            return ArsenalResult.Busy(LabelHandsBusy);
        }
        return Blocked(target);
    }

    private static bool HandsEmpty(Player target) => target.HandsController == null || target.HandsController.Item == null || target.HandsController is Player.EmptyHandsController;

    public static Item InSlot(Player target, EquipmentSlot slot) => SlotOf(target, slot)?.ContainedItem;

    private static readonly EquipmentSlot[] WeaponSlots = { EquipmentSlot.FirstPrimaryWeapon, EquipmentSlot.SecondPrimaryWeapon, EquipmentSlot.Holster };

    public static async Task EquipBest(Player target)
    {
        if (!Blocked(target).IsOk)
        {
            return;
        }
        EquipmentSlot slot = default;
        Item weapon = null;
        foreach (EquipmentSlot candidate in WeaponSlots)
        {
            if (InSlot(target, candidate) is Weapon found)
            {
                slot = candidate;
                weapon = found;
                break;
            }
        }
        if (weapon == null || ReferenceEquals(target.HandsController?.Item, weapon))
        {
            ArsenalTrace.Log(target, weapon == null ? "equip best: no weapon in any slot" : $"equip best: already holds {ArsenalTrace.Item(weapon)}");
            return;
        }
        ArsenalTrace.Log(target, $"equip best: drawing {ArsenalTrace.Item(weapon)} from {slot}");

        string name = target.Profile?.Nickname;
        TaskCompletionSource<bool> done = new();
        bool started = false;
        BotOwner bot = target.AIData?.BotOwner;
        if (bot != null && !bot.IsDead && bot.WeaponManager?.Selector != null)
        {
            try
            {
                bot.WeaponManager.UpdateWeaponsList();
                started = bot.WeaponManager.Selector.TryChangeToSlot(slot, true, drawn =>
                {
                    ArsenalTrace.Log(target, $"equip best: AI draw callback {(drawn.Failed ? $"FAILED {drawn.Error}" : "ok")}");
                    done.TrySetResult(true);
                });
                ArsenalTrace.Note(target, $"equip best: AI TryChangeToSlot({slot}) returned {started}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Arsenal: {name}'s AI couldn't take its {slot}: {ex.Message}");
            }
        }
        if (!started)
        {
            ArsenalTrace.Note(target, $"equip best: SetSlotItem({slot})");
            target.SetSlotItem(slot, drawn =>
            {
                ArsenalTrace.Log(target, $"equip best: draw callback {(drawn.Failed ? $"FAILED {drawn.Error}" : "ok")}");
                done.TrySetResult(true);
            });
        }

        Task finished = await Task.WhenAny(done.Task, Task.Delay(5000, RaidScope.Token));
        if (finished != done.Task)
        {
            Plugin.Log.LogWarning($"Arsenal: {name} didn't finish drawing its {weapon.ShortName.Localized()} in 5 s");
            ArsenalTrace.Log(target, "equip best: draw timed out");
            return;
        }
        if (bot != null && !bot.IsDead && bot.WeaponManager != null)
        {
            try
            {
                bot.WeaponManager.UpdateWeaponsList();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Arsenal: {name}'s AI couldn't refresh its weapons: {ex.Message}");
            }
        }
        Plugin.Log.LogInfo($"Arsenal: {name} now holds its {weapon.ShortName.Localized()}");
    }

    private static Slot SlotOf(Player target, EquipmentSlot slot) =>
        target?.InventoryController?.Inventory?.Equipment?.GetSlot(slot);

    internal static Task Load(Item tree)
    {
        HashSet<ResourceKey> resources = new();
        foreach (Item part in tree.GetAllItems())
        {
            foreach (ResourceKey key in part.Template.AllResources)
            {
                resources.Add(key);
            }
        }
        if (resources.Count == 0 || !Singleton<ObjectsFactory>.Instantiated)
        {
            return Task.CompletedTask;
        }
        return Singleton<ObjectsFactory>.Instance.LoadBundlesAndCreatePools(ObjectsFactory.PoolsCategory.Raid, ObjectsFactory.AssemblyType.Local, resources, JobYieldPriority.Immediate, null, ObjectsFactory.DefaultCancellationToken);
    }

    private static ArsenalResult Place(Player target, Item tree, EquipmentSlot? slot) =>
        Place(target, tree, controller => slot.HasValue ? SlotOf(target, slot.Value)?.CreateItemAddress() : FindPlace(target, tree, controller), slot.HasValue ? ArsenalResult.Invalid(LabelUnitNoSuchSlot) : ArsenalResult.NoRoom(LabelNoRoomForItem));

    private static ArsenalResult Place(Player target, Item tree, Func<InventoryController, ItemAddress> where, ArsenalResult nowhere)
    {
        InventoryController controller = target.InventoryController;
        Stash stash = Singleton<ItemFactory>.Instance.CreateFakeStash();
        var staged = stash.Grid.AddAnywhere(tree, EErrorHandlingType.Ignore);
        if (staged.Failed)
        {
            return ArsenalResult.Failed(string.Format(LabelCouldNotStage, staged.Error));
        }
        new ItemController(stash, controller.ID, ObjectArsenalController, false, EOwnerType.Profile);

        ItemAddress address = where(controller);
        if (address == null)
        {
            return nowhere;
        }

        var test = ItemManipulator.Move(tree, address, controller, true);
        if (test.Failed)
        {
            return ArsenalResult.Incompatible(string.Format(LabelDoesNotFit, test.Error));
        }

        var moved = ItemManipulator.Move(tree, address, controller, false);
        if (moved.Failed)
        {
            return ArsenalResult.Failed(string.Format(LabelCouldNotPlace, moved.Error));
        }
        AfterChange(target);
        Plugin.Log.LogInfo($"Arsenal: gave {tree.ShortName.Localized()} ({tree.TemplateId}) to {target.Profile?.Nickname} at {address}");
        return ArsenalResult.Ok;
    }

    // Secure containers can't leave their slot during a raid (CantRemoveFromSlotsDuringRaid), so normal moves fail.
    // Only for bots: a human's secure container goes home with them, so theirs only gets new contents.
    public static async Task<ArsenalResult> ReplaceRaidLockedAsync(Player target, Item tree, EquipmentSlot slot)
    {
        try
        {
            if (tree == null)
            {
                return ArsenalResult.Invalid(LabelUnknownItem);
            }
            if (!target.IsAI)
            {
                return ArsenalResult.Invalid(LabelOnlySecureContainer);
            }
            ArsenalResult result = Blocked(target);
            if (!result.IsOk)
            {
                return result;
            }
            await Load(tree);
            result = Blocked(target);
            Slot place = SlotOf(target, slot);
            if (!result.IsOk || place == null)
            {
                return result.IsOk ? ArsenalResult.Invalid(LabelUnitNoSuchSlot) : result;
            }
            if (!place.CheckCompatibility(tree))
            {
                return ArsenalResult.Incompatible(string.Format(LabelWrongSlot, tree.ShortName.Localized()));
            }

            Item old = place.ContainedItem;
            if (old != null)
            {
                result = await EmptyHandsIfHolding(target, old);
                if (!result.IsOk)
                {
                    return result;
                }
                var removed = place.RemoveItemWithoutRestrictions();
                if (removed.Failed)
                {
                    return ArsenalResult.Failed(string.Format(LabelCouldNotTakeOutError, old.ShortName.Localized(), removed.Error));
                }
            }

            var added = place.AddWithoutRestrictions(tree);
            if (added.Failed)
            {
                string error = string.Format(LabelCouldNotPutIn, tree.ShortName.Localized(), added.Error);
                if (old != null && place.AddWithoutRestrictions(old).Failed)
                {
                    Plugin.Log.LogError($"Arsenal: {target.Profile?.Nickname} lost its {old.ShortName.Localized()}: {error}");
                    return ArsenalResult.RollbackFailed(string.Format(LabelNotRestored, error, old.ShortName.Localized()));
                }
                return ArsenalResult.Failed(error);
            }
            AfterChange(target);
            Plugin.Log.LogInfo($"Arsenal: swapped {target.Profile?.Nickname}'s {slot} {old?.ShortName.Localized() ?? "(none)"} for {tree.ShortName.Localized()} with {tree.GetAllItems().Count() - 1} item(s) inside");
            return ArsenalResult.Ok;
        }
        catch (OperationCanceledException)
        {
            return ArsenalResult.Cancelled();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Arsenal container swap failed: {ex}");
            return ArsenalResult.Failed(LabelFailedSeeLog);
        }
    }

    // Tries the item's spot in the source container first (same layout when the containers match), then any free
    // spot in the container, then the unit's other containers, so a smaller container doesn't lose the overflow.
    public static async Task<ArsenalResult> GiveContentAsync(Player target, Item tree, CompoundItem container, string gridId, LocationInGrid at)
    {
        try
        {
            if (tree == null || container == null)
            {
                return ArsenalResult.Invalid(tree == null ? LabelUnknownItem : LabelNoContainer);
            }
            ArsenalResult result = Blocked(target);
            if (!result.IsOk)
            {
                return result;
            }
            await Load(tree);
            result = Blocked(target);
            if (!result.IsOk)
            {
                return result;
            }
            string placedBy = null;
            result = Place(target, tree, controller =>
            {
                ItemAddress address = OriginalSpot(container, tree, gridId, at, controller);
                placedBy = DumpOriginalSpot;
                if (address == null)
                {
                    address = FindInContainer(container, tree, controller);
                    placedBy = DumpFreeSpot;
                }
                if (address == null)
                {
                    address = FindInOtherContainers(target, tree, container, controller);
                    placedBy = DumpOverflow;
                }
                return address;
            }, ArsenalResult.NoRoom(string.Format(LabelNoRoomInContainers, container.ShortName.Localized())));
            ArsenalTrace.Note(target, result.IsOk ? $"content {ArsenalTrace.Item(tree)} went to {placedBy}" : $"content {ArsenalTrace.Item(tree)} not placed: {result.Message}");
            return result;
        }
        catch (OperationCanceledException)
        {
            return ArsenalResult.Cancelled();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Arsenal give into a container failed: {ex}");
            return ArsenalResult.Failed(LabelFailedSeeLog);
        }
    }

    private static ItemAddress OriginalSpot(CompoundItem container, Item tree, string gridId, LocationInGrid at, InventoryController controller)
    {
        if (at == null || container.Grids == null)
        {
            return null;
        }
        Grid grid = container.Grids.FirstOrDefault(g => g.ID == gridId) ?? (container.Grids.Length == 1 ? container.Grids[0] : null);
        if (grid == null || !grid.LiesWithinGrid(tree, at))
        {
            return null;
        }
        ItemAddress address = grid.CreateItemAddress(at);
        return ItemManipulator.Move(tree, address, controller, true).Succeeded ? address : null;
    }

    private static ItemAddress FindInOtherContainers(Player target, Item tree, CompoundItem except, InventoryController controller)
    {
        foreach (EquipmentSlot name in ContainerOrder)
        {
            if (SlotOf(target, name)?.ContainedItem is CompoundItem other && !ReferenceEquals(other, except))
            {
                ItemAddress address = FindInContainer(other, tree, controller);
                if (address != null)
                {
                    return address;
                }
            }
        }
        return null;
    }

    public static List<Item> ContentsOf(CompoundItem container)
    {
        List<Item> contents = new();
        if (container?.Grids != null)
        {
            foreach (var grid in container.Grids)
            {
                contents.AddRange(grid.Items);
            }
        }
        return contents;
    }

    private static ItemAddress FindInContainer(CompoundItem container, Item tree, InventoryController controller)
    {
        if (container.Grids == null)
        {
            return null;
        }
        foreach (var grid in container.Grids)
        {
            ItemAddress address = grid.FindLocationForItem(tree);
            if (address != null && ItemManipulator.Move(tree, address, controller, true).Succeeded)
            {
                return address;
            }
        }
        return null;
    }

    private static ItemAddress FindPlace(Player target, Item tree, InventoryController controller)
    {
        foreach (EquipmentSlot name in SlotOrder)
        {
            Slot slot = SlotOf(target, name);
            if (slot == null || slot.ContainedItem != null || !slot.CheckCompatibility(tree))
            {
                continue;
            }
            ItemAddress address = slot.CreateItemAddress();
            if (ItemManipulator.Move(tree, address, controller, true).Succeeded)
            {
                return address;
            }
        }

        foreach (EquipmentSlot name in ContainerOrder)
        {
            if (SlotOf(target, name)?.ContainedItem is not CompoundItem container || container.Grids == null)
            {
                continue;
            }
            foreach (var grid in container.Grids)
            {
                ItemAddress address = grid.FindLocationForItem(tree);
                if (address != null && ItemManipulator.Move(tree, address, controller, true).Succeeded)
                {
                    return address;
                }
            }
        }
        return null;
    }

    private static async Task<ArsenalResult> RemoveAsync(Player target, Item item)
    {
        if (item == null)
        {
            return ArsenalResult.Invalid(LabelNothingToRemove);
        }

        ArsenalResult hands = await EmptyHandsIfHolding(target, item);
        if (!hands.IsOk)
        {
            return hands;
        }

        InventoryController controller = target.InventoryController;
        var discard = ItemManipulator.Discard(item, controller, true, true);
        if (discard.Failed)
        {
            return ArsenalResult.Incompatible(string.Format(LabelCantRemove, discard.Error));
        }

        IResult result = null;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            ArsenalResult blocked = Blocked(target);
            if (!blocked.IsOk)
            {
                return blocked;
            }
            if (attempt > 0)
            {
                discard = ItemManipulator.Discard(item, controller, true, true);
                if (discard.Failed)
                {
                    return ArsenalResult.Incompatible(string.Format(LabelCantRemove, discard.Error));
                }
            }
            result = await controller.TryRunNetworkTransaction(discard);
            if (result.Succeed)
            {
                break;
            }
            ArsenalTrace.Log(target, $"removing {ArsenalTrace.Item(item)} attempt {attempt + 1} failed: {result.Error}");
            await Task.Delay(250, RaidScope.Token);
        }
        if (result == null || result.Failed)
        {
            return ArsenalResult.Busy(string.Format(LabelUnitBusy, result?.Error));
        }
        AfterChange(target);
        Plugin.Log.LogInfo($"Arsenal: removed {item.ShortName.Localized()} from {target.Profile?.Nickname}");
        return ArsenalResult.Ok;
    }

    // During a loadout every slot change used to make the AI draw its main weapon while the next slot was still being
    // emptied, and the overlapping hand changes left bots switching weapons. Draws wait until the whole kit is in.
    private static readonly HashSet<Player> DrawsHeld = new();

    public static void HoldDraws(Player target)
    {
        if (target != null)
        {
            DrawsHeld.Add(target);
        }
    }

    public static void ReleaseDraws(Player target) => DrawsHeld.Remove(target);

    // The AI's weapon list, its cached "no ammo for reload" verdict and SAIN's per-slot weapon infos all still describe
    // the old guns after a swap; stale entries make the bot keep changing to weapons it no longer has or can't reload.
    public static void RefreshAi(Player target)
    {
        BotOwner bot = target?.AIData?.BotOwner;
        if (bot == null || bot.IsDead || bot.WeaponManager == null)
        {
            return;
        }
        try
        {
            bot.WeaponManager.UpdateWeaponsList();
            if (bot.WeaponManager.Reload != null)
            {
                bot.WeaponManager.Reload.NoAmmoForReloadCached = false;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Arsenal: {target.Profile?.Nickname}'s AI couldn't refresh its weapons: {ex.Message}");
        }
        string sain = SainCompat.RefreshWeapons(target);
        if (sain != null)
        {
            ArsenalTrace.Note(target, $"SAIN weapons: {sain}");
        }
    }

    private const int BotAmmoStacks = 20;

    // Bots reload from loose rounds in their inventory (the server puts 20 stacks in the secure container). A kit taken
    // from another unit may have no rounds for its guns, and a bot with nothing to reload with keeps switching weapons.
    public static async Task TopUpAmmoAsync(Player target)
    {
        if (target == null || !target.IsAI || SlotOf(target, EquipmentSlot.SecuredContainer)?.ContainedItem is not CompoundItem container)
        {
            return;
        }
        HashSet<string> done = new();
        foreach (EquipmentSlot slot in WeaponSlots)
        {
            if (InSlot(target, slot) is not Weapon weapon || string.IsNullOrEmpty(weapon.AmmoCaliber) || !done.Add(weapon.AmmoCaliber))
            {
                continue;
            }
            string ammo = AmmoFor(weapon);
            Item sample = ammo != null ? Build(ammo, int.MaxValue) : null;
            if (sample == null)
            {
                ArsenalTrace.Note(target, $"ammo: no ammo type found for {ArsenalTrace.Item(weapon)}");
                continue;
            }
            int stack = Math.Max(1, sample.StackMaxSize);
            int loose = LooseRounds(target, weapon.AmmoCaliber);
            int missing = (BotAmmoStacks * stack - loose) / stack;
            int added = 0;
            for (int i = 0; i < missing; i++)
            {
                Item rounds = i == 0 ? sample : Build(ammo, int.MaxValue);
                if (!(await GiveContentAsync(target, rounds, container, null, null)).IsOk)
                {
                    break;
                }
                added++;
            }
            if (added > 0 || ArsenalTrace.On)
            {
                Plugin.Log.LogInfo($"Arsenal: {target.Profile?.Nickname} had {loose} spare {weapon.AmmoCaliber} round(s) for its {weapon.ShortName.Localized()}, added {added} stack(s) of {sample.ShortName.Localized()}");
            }
        }
    }

    private static string AmmoFor(Weapon weapon)
    {
        Item loaded = weapon.GetAllItems().OfType<Ammo>().FirstOrDefault();
        if (loaded != null)
        {
            return loaded.TemplateId;
        }
        return weapon.Template is WeaponTemplate template && !string.IsNullOrEmpty(template.defAmmo) ? template.defAmmo : null;
    }

    private static int LooseRounds(Player target, string caliber)
    {
        InventoryEquipment equipment = target.InventoryController?.Inventory?.Equipment;
        if (equipment == null)
        {
            return 0;
        }
        int rounds = 0;
        foreach (Ammo ammo in equipment.GetAllItems().OfType<Ammo>())
        {
            Item holder = ammo.Parent?.Container?.ParentItem;
            if (ammo.Caliber == caliber && holder is not Magazine && holder is not Weapon)
            {
                rounds += ammo.StackObjectsCount;
            }
        }
        return rounds;
    }

    private static void AfterChange(Player target)
    {
        BotOwner bot = target.AIData?.BotOwner;
        if (bot != null && !bot.IsDead && bot.WeaponManager != null)
        {
            if (DrawsHeld.Contains(target))
            {
                ArsenalTrace.Log(target, "after change: AI draw held until the loadout is done");
                return;
            }
            RefreshAi(target);
            try
            {
                bool? changing = bot.WeaponManager.Selector?.TryChangeToMain();
                ArsenalTrace.Log(target, $"after change: AI TryChangeToMain returned {changing?.ToString() ?? "no selector"}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Arsenal: {target.Profile?.Nickname}'s AI couldn't refresh its weapons: {ex.Message}");
            }
            return;
        }

        if (target.HandsController is Player.EmptyHandsController)
        {
            ArsenalTrace.Log(target, "after change: empty hands, TrySetLastEquippedWeapon");
            target.TrySetLastEquippedWeapon();
        }
    }
}
