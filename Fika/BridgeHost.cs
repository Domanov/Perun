using BepInEx.Configuration;
using Fika.Core.Main.Players;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Perun.Client;
using UnityEngine;
using static Perun.Fika.Constants.BridgeHost;

namespace Perun.Fika;

internal sealed class BridgeHost : MonoBehaviour
{
    private FikaBridge _bridge;
#if DEBUG
    private ConfigEntry<bool> _forge;
    private ConfigEntry<bool> _malformed;
#endif

    public void Bind(ConfigFile config)
    {
        _bridge = new FikaBridge();
#if DEBUG
        _forge = config.Bind(ConfigSectionDebug, ConfigKeyForgeHost, false, ConfigDescriptionForgeHost);
        _malformed = config.Bind(ConfigSectionDebug, ConfigKeyMalformedRequests, false, ConfigDescriptionMalformedRequests);
#endif
        PerunNetwork.ObservedBot = player => player is FikaPlayer fikaPlayer && fikaPlayer.IsObservedAI;
        string missing = DeathCamera.Missing;
        if (missing == null)
        {
            DeathCamera deathCamera = new();
            PerunNetwork.SuspendDeathCamera = deathCamera.Suspend;
            PerunNetwork.ResumeDeathCamera = deathCamera.Resume;
        }
        else
        {
            Plugin.Log.LogWarning($"Fika's {missing} wasn't found; Perun won't open after you die");
        }
        FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerCreatedEvent>(_bridge.OnManagerCreated);
        FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerDestroyedEvent>(_bridge.OnManagerDestroyed);
        FikaEventDispatcher.SubscribeEvent<PeerDisconnectedEvent>(_bridge.OnPeerDisconnected);
        FikaEventDispatcher.SubscribeEvent<FikaRaidStartedEvent>(_bridge.OnRaidStarted);
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
