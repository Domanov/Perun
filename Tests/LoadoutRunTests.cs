using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Perun.Client.Arsenal;

namespace Perun.Tests;

public class LoadoutRunTests
{
    private sealed class FakeUnit
    {
        public readonly Dictionary<string, string> Worn = new();
        public readonly Dictionary<string, string> Holding = new();
        public readonly Dictionary<string, string> New = new();
        public readonly HashSet<string> CantRestore = new();
        public readonly HashSet<string> NoRoom = new();
        public string? CancelAt;
        public string? ThrowAt;
        public int BusyAfterGives = int.MaxValue;
        private int _gives;

        public LoadoutSteps<string> Steps() => new(
            () => _gives >= BusyAfterGives ? ArsenalResult.Busy("busy") : ArsenalResult.Ok,
            slot =>
            {
                Holding[slot] = Worn[slot];
                Worn.Remove(slot);
                return Task.FromResult(ArsenalResult.Ok);
            },
            slot =>
            {
                if (slot == ThrowAt)
                {
                    throw new InvalidOperationException("boom");
                }
                if (slot == CancelAt)
                {
                    return Task.FromResult(ArsenalResult.Cancelled());
                }
                if (NoRoom.Contains(slot))
                {
                    return Task.FromResult(ArsenalResult.Incompatible("doesn't fit"));
                }
                Worn[slot] = New[slot];
                _gives++;
                return Task.FromResult(ArsenalResult.Ok);
            },
            slot =>
            {
                if (CantRestore.Contains(slot))
                {
                    return $"{Holding[slot]} (lost)";
                }
                Worn[slot] = Holding[slot];
                Holding.Remove(slot);
                return null;
            });

        public Task<LoadoutOutcome> Apply()
        {
            List<string> slots = new() { "head", "vest", "rig", "back" };
            return LoadoutRun.Run(slots.Where(Worn.ContainsKey).Reverse().ToList(), slots.Where(New.ContainsKey).ToList(), Steps());
        }
    }

    private static ArsenalResult Stopped(LoadoutOutcome outcome)
    {
        Assert.True(outcome.Aborted.HasValue, "expected the run to stop");
        return outcome.Aborted.GetValueOrDefault();
    }

    private static FakeUnit Geared()
    {
        FakeUnit unit = new();
        unit.Worn["head"] = "old helmet";
        unit.Worn["vest"] = "old vest";
        unit.Worn["rig"] = "old rig";
        unit.Worn["back"] = "old pack";
        unit.New["head"] = "new helmet";
        unit.New["vest"] = "new vest";
        unit.New["rig"] = "new rig";
        unit.New["back"] = "new pack";
        return unit;
    }

    [Fact]
    public async Task ChangesEverySlot()
    {
        FakeUnit unit = Geared();
        LoadoutOutcome outcome = await unit.Apply();

        Assert.False(outcome.Aborted.HasValue);
        Assert.Empty(outcome.Failures);
        Assert.Equal("new pack", unit.Worn["back"]);
        Assert.Equal(4, unit.Holding.Count);
    }

    [Fact]
    public async Task UnitBusyAfterSomeSlotsKeepsThoseAndRestoresTheRest()
    {
        FakeUnit unit = Geared();
        unit.BusyAfterGives = 2;
        LoadoutOutcome outcome = await unit.Apply();

        Assert.Equal(ArsenalStatus.Busy, Stopped(outcome).Status);
        Assert.Equal("new helmet", unit.Worn["head"]);
        Assert.Equal("new vest", unit.Worn["vest"]);
        Assert.Equal("old rig", unit.Worn["rig"]);
        Assert.Equal("old pack", unit.Worn["back"]);
        Assert.Equal(new[] { "head", "vest" }, unit.Holding.Keys.OrderBy(k => k).ToArray());
    }

    [Fact]
    public async Task CancelledDuringALaterGiveRestoresEverySlotStillWaiting()
    {
        FakeUnit unit = Geared();
        unit.CancelAt = "rig";
        LoadoutOutcome outcome = await unit.Apply();

        Assert.Equal(ArsenalStatus.Cancelled, Stopped(outcome).Status);
        Assert.Equal("old rig", unit.Worn["rig"]);
        Assert.Equal("old pack", unit.Worn["back"]);
        Assert.False(unit.Holding.ContainsKey("rig"));
        Assert.False(unit.Holding.ContainsKey("back"));
    }

    [Fact]
    public async Task ExceptionInAGiveStillRestores()
    {
        FakeUnit unit = Geared();
        unit.ThrowAt = "vest";
        LoadoutOutcome outcome = await unit.Apply();

        Assert.Equal(ArsenalStatus.Failed, Stopped(outcome).Status);
        Assert.Equal("new helmet", unit.Worn["head"]);
        Assert.Equal("old vest", unit.Worn["vest"]);
        Assert.Equal("old pack", unit.Worn["back"]);
        Assert.Equal(new[] { "head" }, unit.Holding.Keys.ToArray());
    }

    [Fact]
    public async Task TwoFailedRestoresAreReportedTogether()
    {
        FakeUnit unit = Geared();
        unit.CancelAt = "vest";
        unit.CantRestore.Add("rig");
        unit.CantRestore.Add("back");
        LoadoutOutcome outcome = await unit.Apply();

        Assert.Equal(ArsenalStatus.RollbackFailed, Stopped(outcome).Status);
        Assert.Contains("old rig (lost)", Stopped(outcome).Message);
        Assert.Contains("old pack (lost)", Stopped(outcome).Message);
        Assert.Equal("old vest", unit.Worn["vest"]);
    }

    [Fact]
    public async Task ItemThatDoesNotFitPutsThatSlotBackAndCarriesOn()
    {
        FakeUnit unit = Geared();
        unit.NoRoom.Add("vest");
        LoadoutOutcome outcome = await unit.Apply();

        Assert.False(outcome.Aborted.HasValue);
        Assert.Single(outcome.Failures);
        Assert.Equal("old vest", unit.Worn["vest"]);
        Assert.Equal("new pack", unit.Worn["back"]);
    }

    [Fact]
    public async Task SlotEmptiedByTheLoadoutIsRestoredOnAbort()
    {
        FakeUnit unit = Geared();
        unit.New.Remove("back");
        unit.CancelAt = "rig";
        LoadoutOutcome outcome = await unit.Apply();

        Assert.Equal(ArsenalStatus.Cancelled, Stopped(outcome).Status);
        Assert.Equal("old pack", unit.Worn["back"]);
    }
}
