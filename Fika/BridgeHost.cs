using BepInEx.Configuration;
using Fika.Core.Main.Players;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Perun.Client;
using UnityEngine;

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
        _forge = config.Bind("Debug build", "Send forged host messages", false, "Sends a Hello claiming protocol 6, an unsigned pause and a pause with a wrong token to every other player, the way a modified client would. Debug builds only.");
        _malformed = config.Bind("Debug build", "Send malformed game master requests", false, "Sends a NaN spawn, a BTR gunner spawn, a 500-bot spawn and an unknown order to the host. Debug builds only.");
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
