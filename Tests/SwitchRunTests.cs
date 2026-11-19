using Perun.Client.Mode;

namespace Perun.Tests;

public class SwitchRunTests
{
    [Fact]
    public void CallbackAfterCancelIsStale()
    {
        SwitchRun runs = new();
        int run = runs.Start();

        runs.Cancel();

        Assert.False(runs.IsCurrent(run));
    }

    [Fact]
    public void CallbackFromAReplacedRunIsStaleAndTheNewRunIsNot()
    {
        SwitchRun runs = new();
        int first = runs.Start();
        int second = runs.Start();

        Assert.False(runs.IsCurrent(first));
        Assert.True(runs.IsCurrent(second));
    }

    [Fact]
    public void RunStaysCurrentUntilSomethingReplacesIt()
    {
        SwitchRun runs = new();
        int run = runs.Start();

        Assert.True(runs.IsCurrent(run));
    }
}
