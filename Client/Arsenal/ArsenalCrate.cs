using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;

namespace Perun.Client.Arsenal;

internal static class ArsenalCrate
{
    private static Stash _stash;
    private static GameWorld _world;
    private static Grid _grid;

    public static int Count
    {
        get
        {
            if (_grid == null || !ReferenceEquals(_world, Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null))
            {
                return 0;
            }
            int count = 0;
            foreach (Item _ in _grid.Items)
            {
                count++;
            }
            return count;
        }
    }

    public static bool IsOpen { get; private set; }

    private static bool Ensure()
    {
        GameWorld world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
        if (!ReferenceEquals(world, _world))
        {
            Forget();
            _world = world;
        }
        if (_stash != null)
        {
            return true;
        }
        if (!Singleton<ItemFactory>.Instantiated)
        {
            return false;
        }
        _stash = Singleton<ItemFactory>.Instance.CreateFakeStash();
        _grid = new Grid("perunArsenalCrate", 10, 1, true, Array.Empty<ItemFilter>(), _stash);
        _stash.Grids[0] = _grid;
        return true;
    }

    public static async Task<ArsenalResult> Add(Item tree)
    {
        if (tree == null)
        {
            return ArsenalResult.Invalid("unknown item");
        }
        if (!Ensure())
        {
            return ArsenalResult.Busy("no item factory yet");
        }
        Grid grid = _grid;
        await ArsenalOps.Load(tree);
        if (!ReferenceEquals(grid, _grid) || !ReferenceEquals(_world, Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null))
        {
            return ArsenalResult.Cancelled();
        }
        var added = grid.AddAnywhere(tree, EErrorHandlingType.Ignore);
        if (added.Failed)
        {
            return ArsenalResult.NoRoom($"couldn't add it: {added.Error}");
        }
        Plugin.Log.LogInfo($"Arsenal crate: added {tree.ShortName.Localized()} ({tree.TemplateId})");
        return ArsenalResult.Ok;
    }

    public static void Snapshot(List<Item> into)
    {
        into.Clear();
        if (_grid == null || !ReferenceEquals(_world, Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null))
        {
            return;
        }
        into.AddRange(_grid.Items);
    }

    public static void Remove(Item item)
    {
        if (_grid == null || item == null)
        {
            return;
        }
        var removed = _grid.Remove(item, false);
        if (removed.Failed)
        {
            Plugin.Log.LogWarning($"Arsenal crate: couldn't remove {item.ShortName.Localized()}: {removed.Error}");
        }
    }

    public static void Empty()
    {
        if (_grid == null)
        {
            return;
        }
        List<Item> items = new(_grid.Items);
        foreach (Item item in items)
        {
            _grid.Remove(item, false);
        }
        Plugin.Log.LogInfo($"Arsenal crate: emptied ({items.Count} items)");
    }

    public static void Forget()
    {
        _stash = null;
        _grid = null;
        IsOpen = false;
    }

    public static string Open(GamePlayerOwner owner, Player unit)
    {
        if (owner == null || unit?.InventoryController == null)
        {
            return "no controlled unit";
        }
        if (IsOpen)
        {
            return null;
        }
        if (!Ensure())
        {
            return "no item factory yet";
        }
        new ItemController(_stash, unit.InventoryController.ID, "Perun arsenal crate", false, EOwnerType.Profile);
        IsOpen = true;
        try
        {
            owner.ShowInventoryScreenLoot(_stash, () => IsOpen = false, false);
        }
        catch
        {
            IsOpen = false;
            throw;
        }
        Plugin.Log.LogInfo($"Arsenal crate: opened next to {unit.Profile?.Nickname}'s inventory ({Count} items)");
        return null;
    }
}
