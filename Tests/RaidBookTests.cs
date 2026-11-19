using Perun.Client.Units;

namespace Perun.Tests;

public class RaidBookTests
{
    [Fact]
    public void LateWarmFromAnOldRaidLeavesTheNewRaidAlone()
    {
        RaidBook<string> book = new();
        RaidBook<string>.Lease oldWarm = book.BeginWarm("scav");

        book.Reset();
        RaidBook<string>.Lease newWarm = book.BeginWarm("scav");

        Assert.False(book.End(oldWarm));
        Assert.True(book.IsWarming("scav"));
        Assert.True(book.Busy);
        Assert.Equal(1, book.BusyCount);

        Assert.True(book.End(newWarm));
        Assert.False(book.IsWarming("scav"));
        Assert.False(book.Busy);
    }

    [Fact]
    public void EndingTwiceCountsOnce()
    {
        RaidBook<string> book = new();
        RaidBook<string>.Lease first = book.Begin();
        book.Begin();

        Assert.True(book.End(first));
        Assert.False(book.End(first));
        Assert.Equal(1, book.BusyCount);
    }

    [Fact]
    public void OwnershipEndsWithTheRaid()
    {
        RaidBook<string> book = new();
        RaidBook<string>.Lease lease = book.BeginWarm("pmc");
        Assert.True(book.Owns(lease));

        book.Reset();

        Assert.False(book.Owns(lease));
        Assert.Equal(0, book.WarmingCount);
        Assert.False(book.Busy);
    }

    [Fact]
    public void PlainBusyLeaseDoesNotTouchWarming()
    {
        RaidBook<string> book = new();
        book.BeginWarm("scav");
        RaidBook<string>.Lease spawn = book.Begin();

        book.End(spawn);

        Assert.True(book.IsWarming("scav"));
        Assert.Equal(1, book.BusyCount);
    }
}
