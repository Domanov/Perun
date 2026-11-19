using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using Diz.Jobs;
using EFT;
using EFT.InventoryLogic;
using Perun.Client.Mode;
using UnityEngine;

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
            return ArsenalResult.Invalid("that unit is gone");
        }
        if (target.InventoryController == null)
        {
            return ArsenalResult.Invalid("that unit has no inventory");
        }
        if (ReferenceEquals(target, Possession.Body))
        {
            return ArsenalResult.Busy("give the bot back first");
        }
        return ArsenalResult.Ok;
    }

    public static async Task<ArsenalResult> GiveAsync(Player target, Item tree, EquipmentSlot? slot, bool equip = true)
    {
        try
        {
            ArsenalResult result = tree == null ? ArsenalResult.Invalid("unknown item") : Blocked(target);
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
                    return ArsenalResult.Incompatible($"{tree.ShortName.Localized()} doesn't go in that slot");
                }
                (result, held) = await HoldAsync(target, slot.Value);
            }
            if (result.IsOk)
            {
                result = Place(target, tree, slot);
                string lost = !result.IsOk && held != null ? Restore(target, held) : null;
                if (lost != null)
                {
                    return ArsenalResult.RollbackFailed($"{result.Message}; couldn't put back {lost}");
                }
            }
            if (result.IsOk && equip && tree is Weapon && HandsEmpty(target))
            {
                await EquipBest(target);
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
            return ArsenalResult.Failed("it failed (see log)");
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
            return blocked.IsOk ? await RemoveAsync(target, item) : blocked;
        }
        catch (OperationCanceledException)
        {
            return ArsenalResult.Cancelled();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Arsenal clear failed: {ex}");
            return ArsenalResult.Failed("it failed (see log)");
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

        InventoryController controller = target.InventoryController;
        Stash stash = Singleton<ItemFactory>.Instance.CreateFakeStash();
        new ItemController(stash, controller.ID, "Perun arsenal hold", false, EOwnerType.Profile);
        ItemAddress address = stash.Grid.FindLocationForItem(item);
        if (address == null || ItemManipulator.Move(item, address, controller, true).Failed)
        {
            return (ArsenalResult.Failed($"couldn't take out {item.ShortName.Localized()}"), null);
        }
        var moved = ItemManipulator.Move(item, address, controller, false);
        if (moved.Failed)
        {
            return (ArsenalResult.Failed($"couldn't take out {item.ShortName.Localized()}: {moved.Error}"), null);
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
            return (ArsenalResult.Invalid("that item isn't anywhere"), null);
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
        string error = address == null ? "no such slot" : TryMove(target, held.Item, address);
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
            return moved.Error?.ToString() ?? "unknown error";
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
                return $"{name} (dropped next to the unit)";
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Arsenal: couldn't drop {name} either: {ex.Message}");
        }
        return $"{name} (lost)";
    }

    private static async Task<ArsenalResult> EmptyHandsIfHolding(Player target, Item item)
    {
        Item inHands = target.HandsController?.Item;
        if (inHands == null || (!ReferenceEquals(inHands, item) && !item.GetAllItems().Contains(inHands)))
        {
            return ArsenalResult.Ok;
        }
        TaskCompletionSource<bool> emptied = new();
        target.SetEmptyHands(_ => emptied.TrySetResult(true));
        Task finished = await Task.WhenAny(emptied.Task, Task.Delay(4000, RaidScope.Token));
        if (finished != emptied.Task)
        {
            return ArsenalResult.Busy("the unit's hands are busy");
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
            return;
        }

        string name = target.Profile?.Nickname;
        TaskCompletionSource<bool> done = new();
        bool started = false;
        BotOwner bot = target.AIData?.BotOwner;
        if (bot != null && !bot.IsDead && bot.WeaponManager?.Selector != null)
        {
            try
            {
                bot.WeaponManager.UpdateWeaponsList();
                started = bot.WeaponManager.Selector.TryChangeToSlot(slot, true, _ => done.TrySetResult(true));
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Arsenal: {name}'s AI couldn't take its {slot}: {ex.Message}");
            }
        }
        if (!started)
        {
            target.SetSlotItem(slot, _ => done.TrySetResult(true));
        }

        Task finished = await Task.WhenAny(done.Task, Task.Delay(5000, RaidScope.Token));
        if (finished != done.Task)
        {
            Plugin.Log.LogWarning($"Arsenal: {name} didn't finish drawing its {weapon.ShortName.Localized()} in 5 s");
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
        Place(target, tree, controller => slot.HasValue ? SlotOf(target, slot.Value)?.CreateItemAddress() : FindPlace(target, tree, controller), slot.HasValue ? ArsenalResult.Invalid("that unit has no such slot") : ArsenalResult.NoRoom("no slot or free space for it"));

    private static ArsenalResult Place(Player target, Item tree, Func<InventoryController, ItemAddress> where, ArsenalResult nowhere)
    {
        InventoryController controller = target.InventoryController;
        Stash stash = Singleton<ItemFactory>.Instance.CreateFakeStash();
        var staged = stash.Grid.AddAnywhere(tree, EErrorHandlingType.Ignore);
        if (staged.Failed)
        {
            return ArsenalResult.Failed($"couldn't stage the item: {staged.Error}");
        }
        new ItemController(stash, controller.ID, "Perun arsenal", false, EOwnerType.Profile);

        ItemAddress address = where(controller);
        if (address == null)
        {
            return nowhere;
        }

        var test = ItemManipulator.Move(tree, address, controller, true);
        if (test.Failed)
        {
            return ArsenalResult.Incompatible($"it doesn't fit: {test.Error}");
        }

        var moved = ItemManipulator.Move(tree, address, controller, false);
        if (moved.Failed)
        {
            return ArsenalResult.Failed($"couldn't place it: {moved.Error}");
        }
        AfterChange(target);
        Plugin.Log.LogInfo($"Arsenal: gave {tree.ShortName.Localized()} ({tree.TemplateId}) to {target.Profile?.Nickname} at {address}");
        return ArsenalResult.Ok;
    }

    public static async Task<ArsenalResult> GiveIntoAsync(Player target, Item tree, CompoundItem container)
    {
        try
        {
            if (tree == null || container == null)
            {
                return ArsenalResult.Invalid(tree == null ? "unknown item" : "no container");
            }
            ArsenalResult result = Blocked(target);
            if (!result.IsOk)
            {
                return result;
            }
            await Load(tree);
            result = Blocked(target);
            return result.IsOk ? Place(target, tree, controller => FindInContainer(container, tree, controller), ArsenalResult.NoRoom($"no room in {container.ShortName.Localized()}")) : result;
        }
        catch (OperationCanceledException)
        {
            return ArsenalResult.Cancelled();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Arsenal give into a container failed: {ex}");
            return ArsenalResult.Failed("it failed (see log)");
        }
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
            return ArsenalResult.Invalid("nothing to remove");
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
            return ArsenalResult.Incompatible($"can't remove it: {discard.Error}");
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
                    return ArsenalResult.Incompatible($"can't remove it: {discard.Error}");
                }
            }
            result = await controller.TryRunNetworkTransaction(discard);
            if (result.Succeed)
            {
                break;
            }
            await Task.Delay(250, RaidScope.Token);
        }
        if (result == null || result.Failed)
        {
            return ArsenalResult.Busy($"the unit is busy, try again ({result?.Error})");
        }
        AfterChange(target);
        Plugin.Log.LogInfo($"Arsenal: removed {item.ShortName.Localized()} from {target.Profile?.Nickname}");
        return ArsenalResult.Ok;
    }

    private static void AfterChange(Player target)
    {
        BotOwner bot = target.AIData?.BotOwner;
        if (bot != null && !bot.IsDead && bot.WeaponManager != null)
        {
            try
            {
                bot.WeaponManager.UpdateWeaponsList();
                bot.WeaponManager.Selector?.TryChangeToMain();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Arsenal: {target.Profile?.Nickname}'s AI couldn't refresh its weapons: {ex.Message}");
            }
            return;
        }

        if (target.HandsController is Player.EmptyHandsController)
        {
            target.TrySetLastEquippedWeapon();
        }
    }
}
