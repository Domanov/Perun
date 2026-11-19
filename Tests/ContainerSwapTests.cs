using System.Collections.Generic;
using System.Threading.Tasks;
using Perun.Client.Arsenal;

namespace Perun.Tests;

public class ContainerSwapTests
{
    private sealed class FakeContainer
    {
        public readonly List<string> Inside = new();
        public readonly List<string> Holding = new();
        public readonly HashSet<string> CantHold = new();
        public readonly HashSet<string> TooBig = new();
        public readonly HashSet<string> CantRestore = new();
        public readonly HashSet<string> CantTakeOut = new();
        public string? CancelAt;

        public SwapSteps<string> Steps() => new(
            () => ArsenalResult.Ok,
            item =>
            {
                if (CantHold.Contains(item))
                {
                    return Task.FromResult(ArsenalResult.Failed($"can't take out {item}"));
                }
                Inside.Remove(item);
                Holding.Add(item);
                return Task.FromResult(ArsenalResult.Ok);
            },
            item =>
            {
                if (item == CancelAt)
                {
                    return Task.FromResult(ArsenalResult.Cancelled());
                }
                if (TooBig.Contains(item))
                {
                    return Task.FromResult(ArsenalResult.NoRoom("no room"));
                }
                Inside.Add(item);
                return Task.FromResult(ArsenalResult.Ok);
            },
            item =>
            {
                if (CantTakeOut.Contains(item))
                {
                    return Task.FromResult(ArsenalResult.Failed($"can't take out {item}"));
                }
                Inside.Remove(item);
                return Task.FromResult(ArsenalResult.Ok);
            },
            item =>
            {
                if (CantRestore.Contains(item))
                {
                    return $"{item} (lost)";
                }
                Holding.Remove(item);
                Inside.Add(item);
                return null;
            },
            item => item);

        public Task<ArsenalResult> Swap(params string[] fresh) => ContainerSwap.Run(new List<string>(Inside), fresh, Steps());
    }

    private static FakeContainer With(params string[] items)
    {
        FakeContainer container = new();
        container.Inside.AddRange(items);
        return container;
    }

    [Fact]
    public async Task ReplacesTheContents()
    {
        FakeContainer container = With("ammo", "med");
        ArsenalResult result = await container.Swap("grenade", "water");

        Assert.True(result.IsOk);
        Assert.Equal(new[] { "grenade", "water" }, container.Inside.ToArray());
    }

    [Fact]
    public async Task SkipsWhatDoesNotFitAndSaysSo()
    {
        FakeContainer container = With("ammo");
        container.TooBig.Add("rifle");
        ArsenalResult result = await container.Swap("grenade", "rifle");

        Assert.Equal(ArsenalStatus.NoRoom, result.Status);
        Assert.Contains("rifle", result.Message);
        Assert.Equal(new[] { "grenade" }, container.Inside.ToArray());
    }

    [Fact]
    public async Task OldItemThatCantBeTakenOutLeavesEverythingAsItWas()
    {
        FakeContainer container = With("ammo", "med", "key");
        container.CantHold.Add("med");
        ArsenalResult result = await container.Swap("grenade");

        Assert.Equal(ArsenalStatus.Failed, result.Status);
        Assert.Equal(new[] { "ammo", "key", "med" }, Sorted(container.Inside));
        Assert.Empty(container.Holding);
    }

    [Fact]
    public async Task CancellingHalfwayPutsTheOldContentsBack()
    {
        FakeContainer container = With("ammo", "med");
        container.CancelAt = "water";
        ArsenalResult result = await container.Swap("grenade", "water", "bandage");

        Assert.Equal(ArsenalStatus.Cancelled, result.Status);
        Assert.Equal(new[] { "ammo", "med" }, Sorted(container.Inside));
    }

    [Fact]
    public async Task UnitBecomingBusyHalfwayPutsTheOldContentsBack()
    {
        FakeContainer container = With("ammo");
        SwapSteps<string> steps = container.Steps();
        int placed = 0;
        SwapSteps<string> busyAfterOne = new(
            () => placed >= 1 ? ArsenalResult.Busy("busy") : ArsenalResult.Ok,
            steps.Hold,
            async item =>
            {
                ArsenalResult result = await steps.Place(item);
                placed++;
                return result;
            },
            steps.TakeOut,
            steps.Restore,
            steps.Name);
        ArsenalResult swapped = await ContainerSwap.Run(new List<string>(container.Inside), new[] { "grenade", "water" }, busyAfterOne);

        Assert.Equal(ArsenalStatus.Busy, swapped.Status);
        Assert.Equal(new[] { "ammo" }, container.Inside.ToArray());
    }

    [Fact]
    public async Task FailedRestoreIsReportedWithTheItemName()
    {
        FakeContainer container = With("ammo", "med");
        container.CancelAt = "grenade";
        container.CantRestore.Add("med");
        ArsenalResult result = await container.Swap("grenade");

        Assert.Equal(ArsenalStatus.RollbackFailed, result.Status);
        Assert.Contains("med (lost)", result.Message);
        Assert.Equal(new[] { "ammo" }, container.Inside.ToArray());
    }

    [Fact]
    public async Task NewItemThatCantBeTakenOutAgainIsAFailedRollback()
    {
        FakeContainer container = With("ammo");
        container.CancelAt = "water";
        container.CantTakeOut.Add("grenade");
        ArsenalResult result = await container.Swap("grenade", "water");

        Assert.Equal(ArsenalStatus.RollbackFailed, result.Status);
        Assert.Contains("grenade (new, stayed in the container)", result.Message);
        Assert.Equal(new[] { "ammo", "grenade" }, Sorted(container.Inside));
    }

    [Fact]
    public async Task StuckNewItemAndLostOldItemAreBothNamed()
    {
        FakeContainer container = With("ammo", "med");
        container.CancelAt = "water";
        container.CantTakeOut.Add("grenade");
        container.CantRestore.Add("med");
        ArsenalResult result = await container.Swap("grenade", "water");

        Assert.Equal(ArsenalStatus.RollbackFailed, result.Status);
        Assert.Contains("grenade (new", result.Message);
        Assert.Contains("med (lost)", result.Message);
    }

    private static string[] Sorted(List<string> items)
    {
        string[] sorted = items.ToArray();
        System.Array.Sort(sorted, string.CompareOrdinal);
        return sorted;
    }
}
