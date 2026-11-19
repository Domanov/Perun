using BepInEx;
#if DEBUG
using BepInEx.Configuration;
#endif
using BepInEx.Logging;
using Fika.Core.Modding;
using Fika.Core.Main.Players;
using Fika.Core.Modding.Events;
using Perun.Client;
using UnityEngine;

namespace Perun.Fika;

[BepInPlugin(Guid, Name, Version)]
[BepInDependency("com.SPT.core", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("com.fika.core", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency(Perun.Client.Plugin.Guid, BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "com.dmnv.perunfika";
    public const string Name = "DMNV-PerunFika";
    public const string Version = Perun.Client.Plugin.Version;

    internal static ManualLogSource Log;

    private FikaBridge _bridge;
#if DEBUG
    private ConfigEntry<bool> _forge;
    private ConfigEntry<bool> _malformed;
#endif

    private void Awake()
    {
        Log = Logger;
        _bridge = new FikaBridge();
#if DEBUG
        _forge = Config.Bind("Debug build", "Send forged host messages", false, "Sends a Hello claiming protocol 6, an unsigned pause and a pause with a wrong token to every other player, the way a modified client would. Debug builds only.");
        _malformed = Config.Bind("Debug build", "Send malformed game master requests", false, "Sends a NaN spawn, a BTR gunner spawn, a 500-bot spawn and an unknown order to the host. Debug builds only.");
#endif
        PerunNetwork.ObservedBot = player => player is FikaPlayer fikaPlayer && fikaPlayer.IsObservedAI;
        FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerCreatedEvent>(_bridge.OnManagerCreated);
        FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerDestroyedEvent>(_bridge.OnManagerDestroyed);
        FikaEventDispatcher.SubscribeEvent<PeerDisconnectedEvent>(_bridge.OnPeerDisconnected);
        FikaEventDispatcher.SubscribeEvent<FikaRaidStartedEvent>(_bridge.OnRaidStarted);
        Log.LogInfo($"{Name} {Version} loaded");
    }

    private void Update()
    {
        _bridge.Tick(Time.unscaledTime);
#if DEBUG
        if (_forge.Value)
        {
            _forge.Value = false;
            _bridge.SendForged();
        }
        if (_malformed.Value)
        {
            _malformed.Value = false;
            _bridge.SendMalformed();
        }
#endif
    }

    private void OnDestroy()
    {
        FikaEventDispatcher.UnsubscribeEvent<FikaNetworkManagerCreatedEvent>(_bridge.OnManagerCreated);
        FikaEventDispatcher.UnsubscribeEvent<FikaNetworkManagerDestroyedEvent>(_bridge.OnManagerDestroyed);
        FikaEventDispatcher.UnsubscribeEvent<PeerDisconnectedEvent>(_bridge.OnPeerDisconnected);
        FikaEventDispatcher.UnsubscribeEvent<FikaRaidStartedEvent>(_bridge.OnRaidStarted);
    }
}
