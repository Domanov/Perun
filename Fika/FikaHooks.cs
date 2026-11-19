using System.Runtime.CompilerServices;
using Fika.Core.Main.Players;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Perun.Client;

namespace Perun.Fika;

internal static class FikaHooks
{
    private static FikaBridge _bridge;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Start()
    {
        _bridge = new FikaBridge();
        PerunNetwork.ObservedBot = player => player is FikaPlayer fikaPlayer && fikaPlayer.IsObservedAI;
        FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerCreatedEvent>(_bridge.OnManagerCreated);
        FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerDestroyedEvent>(_bridge.OnManagerDestroyed);
        FikaEventDispatcher.SubscribeEvent<PeerDisconnectedEvent>(_bridge.OnPeerDisconnected);
        FikaEventDispatcher.SubscribeEvent<FikaRaidStartedEvent>(_bridge.OnRaidStarted);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Tick(float now) => _bridge.Tick(now);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Stop()
    {
        FikaEventDispatcher.UnsubscribeEvent<FikaNetworkManagerCreatedEvent>(_bridge.OnManagerCreated);
        FikaEventDispatcher.UnsubscribeEvent<FikaNetworkManagerDestroyedEvent>(_bridge.OnManagerDestroyed);
        FikaEventDispatcher.UnsubscribeEvent<PeerDisconnectedEvent>(_bridge.OnPeerDisconnected);
        FikaEventDispatcher.UnsubscribeEvent<FikaRaidStartedEvent>(_bridge.OnRaidStarted);
    }

#if DEBUG
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void SendForged() => _bridge.SendForged();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void SendMalformed() => _bridge.SendMalformed();
#endif
}
