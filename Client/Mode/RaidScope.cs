using System.Threading;
using Comfort.Common;
using EFT;

namespace Perun.Client.Mode;

internal static class RaidScope
{
    private static GameWorld _world;
    private static CancellationTokenSource _source = new();

    public static CancellationToken Token => _source.Token;

    public static void Tick()
    {
        GameWorld world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
        if (ReferenceEquals(world, _world))
        {
            return;
        }
        _world = world;
        _source.Cancel();
        _source.Dispose();
        _source = new CancellationTokenSource();
    }
}
