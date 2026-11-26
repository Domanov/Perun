using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using JsonType;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static Perun.Client.Arsenal.Constants.Loadouts;

namespace Perun.Client.Arsenal;

internal sealed class Loadout
{
    public string Name;
    public readonly Dictionary<EquipmentSlot, FlatItem[]> Slots = new();
}

internal sealed class SavedLoadout
{
    public string Name { get; set; }
    public DateTime SavedAt { get; set; }
    public List<SavedSlot> Slots { get; set; } = new();
}

internal sealed class SavedSlot
{
    public string Slot { get; set; }
    public FlatItem[] Items { get; set; }
}

internal sealed class ContentItem
{
    public ContentItem(Item item, string grid, LocationInGrid at)
    {
        Item = item;
        Grid = grid;
        At = at;
    }

    public Item Item { get; }
    public string Grid { get; }
    public LocationInGrid At { get; }
}

internal static class Loadouts
{
    public static readonly EquipmentSlot[] Covered =
    {
        EquipmentSlot.Headwear, EquipmentSlot.Earpiece, EquipmentSlot.FaceCover, EquipmentSlot.Eyewear, EquipmentSlot.ArmBand,
        EquipmentSlot.ArmorVest, EquipmentSlot.TacticalVest, EquipmentSlot.Backpack, EquipmentSlot.Scabbard,
        EquipmentSlot.Holster, EquipmentSlot.SecondPrimaryWeapon, EquipmentSlot.FirstPrimaryWeapon
    };

    public static readonly EquipmentSlot[] ContentsOnly = { EquipmentSlot.SecuredContainer };

    private const int Limit = 64;
    private static readonly string FilePath = Path.Combine(BepInEx.Paths.ConfigPath, FileLoadouts);
    private static List<SavedLoadout> _saved;

    public static Loadout Clipboard { get; set; }

    public static List<SavedLoadout> Saved
    {
        get
        {
            if (_saved == null)
            {
                Load();
            }
            return _saved;
        }
    }

    public static Loadout Capture(Player unit, string name) => Capture(unit?.InventoryController?.Inventory?.Equipment, name);

    public static Loadout Capture(InventoryEquipment equipment, string name)
    {
        if (equipment == null || !Singleton<ItemFactory>.Instantiated)
        {
            return null;
        }
        Loadout loadout = new() { Name = name };
        foreach (EquipmentSlot slot in Covered.Concat(ContentsOnly))
        {
            Item item = equipment.GetSlot(slot)?.ContainedItem;
            if (item != null)
            {
                loadout.Slots[slot] = Singleton<ItemFactory>.Instance.TreeToFlatItems(item);
            }
        }
        return loadout;
    }

    public static Item Instantiate(FlatItem[] items) => Instantiate(items, null);

    private static Item Instantiate(FlatItem[] items, List<string> skipped)
    {
        if (items == null || items.Length == 0 || !Singleton<ItemFactory>.Instantiated)
        {
            return null;
        }
        ItemFactory factory = Singleton<ItemFactory>.Instance;
        Dictionary<string, MongoID> ids = new();
        foreach (FlatItem item in items)
        {
            if (!factory.ItemTemplates.ContainsKey(item._tpl))
            {
                Plugin.Log.LogWarning($"Loadout: unknown item template {item._tpl} skipped");
                skipped?.Add(item._tpl);
                continue;
            }
            ids[item._id] = MongoID.Generate(false);
        }
        if (!ids.ContainsKey(items[0]._id))
        {
            return null;
        }

        List<FlatItem> fresh = new(items.Length);
        foreach (FlatItem item in items)
        {
            if (!ids.TryGetValue(item._id, out MongoID id))
            {
                continue;
            }
            MongoID? parent = null;
            if (item.parentId.HasValue)
            {
                if (!ids.TryGetValue(item.parentId.Value, out MongoID mapped))
                {
                    continue;
                }
                parent = mapped;
            }
            fresh.Add(new FlatItem
            {
                _id = id,
                _tpl = item._tpl,
                parentId = parent,
                slotId = item.slotId,
                location = item.location,
                upd = item.upd
            });
        }

        var tree = factory.FlatItemsToTree(fresh.ToArray(), true);
        foreach (var error in tree.DeserializationErrors)
        {
            Plugin.Log.LogWarning($"Loadout: {error}");
        }
        return tree.Items.TryGetValue(ids[items[0]._id], out Item root) ? root : null;
    }

    private sealed class Plan
    {
        public readonly Dictionary<EquipmentSlot, Item> Gear = new();
        public readonly Dictionary<EquipmentSlot, Item> WholeContainers = new();
        public readonly Dictionary<EquipmentSlot, List<ContentItem>> Contents = new();
        public readonly List<EquipmentSlot> Unbuildable = new();
        public readonly List<string> Skipped = new();

        public IEnumerable<Item> AllTrees()
        {
            foreach (Item item in Gear.Values)
            {
                yield return item;
            }
            foreach (Item item in WholeContainers.Values)
            {
                yield return item;
            }
            foreach (List<ContentItem> list in Contents.Values)
            {
                foreach (ContentItem content in list)
                {
                    yield return content.Item;
                }
            }
        }
    }

    private static Plan Prepare(Player unit, Loadout loadout)
    {
        Plan plan = new();
        foreach (EquipmentSlot slot in Covered)
        {
            if (!loadout.Slots.TryGetValue(slot, out FlatItem[] items))
            {
                continue;
            }
            Item item = Instantiate(items, plan.Skipped);
            if (item == null)
            {
                plan.Unbuildable.Add(slot);
            }
            else
            {
                plan.Gear[slot] = item;
            }
        }

        foreach (EquipmentSlot slot in ContentsOnly)
        {
            if (!loadout.Slots.TryGetValue(slot, out FlatItem[] items))
            {
                continue;
            }
            if (unit.IsAI || ArsenalOps.InSlot(unit, slot) is not CompoundItem)
            {
                Item whole = Instantiate(items, plan.Skipped);
                if (whole == null)
                {
                    plan.Unbuildable.Add(slot);
                }
                else
                {
                    plan.WholeContainers[slot] = whole;
                }
                continue;
            }
            List<ContentItem> contents = new();
            foreach ((FlatItem[] subtree, string grid, LocationInGrid at) in Contents(items))
            {
                Item item = Instantiate(subtree, plan.Skipped);
                if (item != null)
                {
                    contents.Add(new ContentItem(item, grid, at));
                }
            }
            plan.Contents[slot] = contents.OrderByDescending(c => Area(c.Item)).ToList();
        }
        return plan;
    }

    public static async Task<ArsenalResult> Apply(Player unit, Loadout loadout, Action<string> progress)
    {
        ArsenalOps.HoldDraws(unit);
        try
        {
            return await ApplyHeld(unit, loadout, progress);
        }
        finally
        {
            ArsenalOps.ReleaseDraws(unit);
        }
    }

    private static async Task<ArsenalResult> ApplyHeld(Player unit, Loadout loadout, Action<string> progress)
    {
        if (loadout == null)
        {
            return ArsenalResult.Invalid(LabelNoLoadout);
        }
        string name = unit?.Profile?.Nickname;
        ArsenalResult blocked = ArsenalOps.Blocked(unit);
        if (!blocked.IsOk)
        {
            return blocked;
        }

        ArsenalTrace.Log(unit, $"loadout {loadout.Name} start, slots {string.Join(", ", loadout.Slots.Keys)}");
        Plan plan = Prepare(unit, loadout);
        if (ArsenalTrace.On)
        {
            ArsenalTrace.Note(unit, $"plan: gear {string.Join(", ", plan.Gear.Select(g => $"{g.Key}={g.Value.ShortName.Localized()}"))}; whole containers {string.Join(", ", plan.WholeContainers.Keys)}; contents {string.Join(", ", plan.Contents.Select(c => $"{c.Key}: {c.Value.Count} item(s)"))}; unbuildable {string.Join(", ", plan.Unbuildable)}");
        }
        progress?.Invoke(string.Format(LabelProgressGear, name));
        foreach (Item tree in plan.AllTrees())
        {
            await ArsenalOps.Load(tree);
        }
        blocked = ArsenalOps.Blocked(unit);
        if (!blocked.IsOk)
        {
            return blocked;
        }

        Dictionary<EquipmentSlot, ArsenalOps.Held> held = new();
        List<EquipmentSlot> takeOff = new();
        for (int i = Covered.Length - 1; i >= 0; i--)
        {
            if (!plan.Unbuildable.Contains(Covered[i]) && ArsenalOps.InSlot(unit, Covered[i]) != null)
            {
                takeOff.Add(Covered[i]);
            }
        }
        List<EquipmentSlot> give = Covered.Where(plan.Gear.ContainsKey).ToList();
        LoadoutSteps<EquipmentSlot> steps = new(
            () => ArsenalOps.Blocked(unit),
            async slot =>
            {
                progress?.Invoke(string.Format(LabelProgressTakeOff, name, slot));
                ArsenalTrace.Log(unit, $"step: take off {slot}");
                (ArsenalResult result, ArsenalOps.Held item) = await ArsenalOps.HoldAsync(unit, slot);
                ArsenalTrace.Log(unit, $"step: take off {slot} -> {result.Status} {result.Message}");
                if (result.IsOk && item != null)
                {
                    held[slot] = item;
                }
                return result;
            },
            slot =>
            {
                progress?.Invoke(string.Format(LabelProgressStep, name, slot));
                ArsenalTrace.Note(unit, $"step: give {slot}");
                return ArsenalOps.GiveAsync(unit, plan.Gear[slot], slot, false);
            },
            slot => held.TryGetValue(slot, out ArsenalOps.Held original) ? ArsenalOps.Restore(unit, original) : null);

        LoadoutOutcome gear = await LoadoutRun.Run(takeOff, give, steps);
        if (gear.Aborted is ArsenalResult aborted)
        {
            Plugin.Log.LogWarning($"Loadout {loadout.Name} on {name}: stopped, {aborted.Message}");
            ArsenalOps.ReleaseDraws(unit);
            await ArsenalOps.EquipBest(unit);
            ArsenalTrace.Watch(unit, $"stopped loadout {loadout.Name}");
            return aborted;
        }
        List<string> failures = gear.Failures;
        bool rollbackFailed = gear.RollbackFailed;

        foreach (KeyValuePair<EquipmentSlot, Item> whole in plan.WholeContainers)
        {
            progress?.Invoke(string.Format(LabelProgressStep, name, whole.Key));
            ArsenalResult result = unit.IsAI
                ? await ArsenalOps.ReplaceRaidLockedAsync(unit, whole.Value, whole.Key)
                : await ArsenalOps.GiveAsync(unit, whole.Value, whole.Key, false);
            if (!result.IsOk)
            {
                failures.Add(string.Format(LabelFailure, whole.Key, result.Message));
            }
        }

        foreach (KeyValuePair<EquipmentSlot, List<ContentItem>> contents in plan.Contents)
        {
            progress?.Invoke(string.Format(LabelProgressStep, name, contents.Key));
            ArsenalTrace.Note(unit, $"filling {contents.Key} with {contents.Value.Count} item(s)");
            ArsenalResult result = await FillContainer(unit, contents.Key, contents.Value);
            ArsenalTrace.Note(unit, $"filled {contents.Key}: {result.Status} {result.Message}");
            rollbackFailed |= result.Status == ArsenalStatus.RollbackFailed;
            if (!result.IsOk && (result.Status == ArsenalStatus.Cancelled || !ArsenalOps.Blocked(unit).IsOk))
            {
                failures.Add(string.Format(LabelFailure, contents.Key, result.Message));
                return Summary(loadout, name, failures, rollbackFailed, result.Status);
            }
            if (!result.IsOk)
            {
                failures.Add(string.Format(LabelFailure, contents.Key, result.Message));
            }
        }

        if (unit.IsAI)
        {
            progress?.Invoke(string.Format(LabelProgressAmmo, name));
            await ArsenalOps.TopUpAmmoAsync(unit);
        }

        progress?.Invoke(string.Format(LabelProgressDraw, name));
        ArsenalOps.ReleaseDraws(unit);
        ArsenalOps.RefreshAi(unit);
        await ArsenalOps.EquipBest(unit);
        ArsenalTrace.Log(unit, $"loadout {loadout.Name} done, {failures.Count} problem(s)");
        ArsenalTrace.Watch(unit, $"loadout {loadout.Name}");

        foreach (EquipmentSlot slot in plan.Unbuildable)
        {
            failures.Add(string.Format(LabelItemGone, slot));
        }
        if (plan.Skipped.Count > 0)
        {
            failures.Add(string.Format(LabelSkippedUnknown, string.Join(", ", plan.Skipped.Distinct())));
        }
        return Summary(loadout, name, failures, rollbackFailed, ArsenalStatus.Failed);
    }

    private static ArsenalResult Summary(Loadout loadout, string name, List<string> failures, bool rollbackFailed, ArsenalStatus status)
    {
        foreach (string failure in failures)
        {
            Plugin.Log.LogWarning($"Loadout {loadout.Name} on {name}: {failure}");
        }
        if (failures.Count == 0)
        {
            return ArsenalResult.Ok;
        }
        string summary = failures.Count == 1 ? failures[0] : string.Format(LabelProblems, failures.Count, failures[0]);
        return rollbackFailed ? ArsenalResult.RollbackFailed(summary) : new ArsenalResult(status, summary);
    }

    private static async Task<ArsenalResult> FillContainer(Player unit, EquipmentSlot slot, List<ContentItem> contents)
    {
        if (ArsenalOps.InSlot(unit, slot) is not CompoundItem container)
        {
            return ArsenalResult.Invalid(LabelContainerGone);
        }

        Dictionary<Item, ArsenalOps.HeldContent> held = new();
        SwapSteps<ContentItem> steps = new(
            () => ArsenalOps.Blocked(unit),
            async content =>
            {
                (ArsenalResult result, ArsenalOps.HeldContent old) = await ArsenalOps.HoldContentAsync(unit, content.Item);
                if (result.IsOk)
                {
                    held[content.Item] = old;
                }
                return result;
            },
            content => ArsenalOps.GiveContentAsync(unit, content.Item, container, content.Grid, content.At),
            async content => (await ArsenalOps.HoldContentAsync(unit, content.Item)).result,
            content => ArsenalOps.RestoreContent(unit, held[content.Item], container),
            content => content.Item.ShortName.Localized());
        List<ContentItem> old = ArsenalOps.ContentsOf(container).Select(item => new ContentItem(item, null, null)).ToList();
        return await ContainerSwap.Run(old, contents, steps);
    }

    private static int Area(Item item)
    {
        try
        {
            var size = item.CalculateCellSize();
            return size.X * size.Y;
        }
        catch (Exception)
        {
            return 1;
        }
    }

    private static LocationInGrid Location(FlatItem item)
    {
        try
        {
            if (item.location?.JObject is not JObject json || json[JsonX] == null || json[JsonY] == null)
            {
                return null;
            }
            JToken rotation = json[JsonRotation];
            ItemRotation r = rotation == null ? ItemRotation.Horizontal
                : rotation.Type == JTokenType.Integer ? (ItemRotation)(int)rotation
                : (ItemRotation)Enum.Parse(typeof(ItemRotation), (string)rotation, true);
            return new LocationInGrid((int)json[JsonX], (int)json[JsonY], r);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static List<(FlatItem[] items, string grid, LocationInGrid at)> Contents(FlatItem[] items)
    {
        List<(FlatItem[], string, LocationInGrid)> result = new();
        foreach (List<FlatItem> subtree in ItemTree.ChildSubtrees(items, item => item._id, item => item.parentId.HasValue ? (string)item.parentId.Value : null))
        {
            FlatItem first = subtree[0];
            subtree[0] = new FlatItem { _id = first._id, _tpl = first._tpl, upd = first.upd };
            result.Add((subtree.ToArray(), first.slotId, Location(first)));
        }
        return result;
    }

    public static SavedLoadout Save(Loadout loadout, string name)
    {
        SavedLoadout saved = new() { Name = string.IsNullOrWhiteSpace(name) ? loadout.Name : name.Trim(), SavedAt = DateTime.Now };
        foreach (KeyValuePair<EquipmentSlot, FlatItem[]> entry in loadout.Slots)
        {
            saved.Slots.Add(new SavedSlot { Slot = entry.Key.ToString(), Items = entry.Value });
        }
        Saved.RemoveAll(existing => string.Equals(existing.Name, saved.Name, StringComparison.OrdinalIgnoreCase));
        Saved.Insert(0, saved);
        while (Saved.Count > Limit)
        {
            Saved.RemoveAt(Saved.Count - 1);
        }
        Write();
        return saved;
    }

    public static void Delete(SavedLoadout saved)
    {
        if (Saved.Remove(saved))
        {
            Write();
        }
    }

    public static Loadout From(SavedLoadout saved)
    {
        Loadout loadout = new() { Name = saved.Name };
        foreach (SavedSlot slot in saved.Slots)
        {
            if (Enum.TryParse(slot.Slot, out EquipmentSlot parsed) && slot.Items != null && slot.Items.Length > 0)
            {
                loadout.Slots[parsed] = slot.Items;
            }
        }
        return loadout;
    }

    private static void Load()
    {
        _saved = new List<SavedLoadout>();
        try
        {
            if (File.Exists(FilePath))
            {
                List<SavedLoadout> loaded = JsonConvert.DeserializeObject<List<SavedLoadout>>(File.ReadAllText(FilePath), EFT.EftJsonConverters.Converters);
                if (loaded != null)
                {
                    _saved.AddRange(loaded);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Saved loadouts could not be read: {ex.Message}");
        }
    }

    private static void Write()
    {
        try
        {
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(_saved, Formatting.Indented, EFT.EftJsonConverters.Converters));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Saved loadouts could not be written: {ex.Message}");
        }
    }
}
