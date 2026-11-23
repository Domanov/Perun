using System;
using System.Collections.Generic;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using Perun.Client;
using UnityEngine;

namespace Perun.Fika;

internal sealed class FikaBridge : IGameMasterLink
{
    private const DeliveryMethod Reliable = DeliveryMethod.ReliableOrdered;
    private const byte Protocol = 7;
    private const byte EnvironmentProtocol = 2;
    private const byte GameMasterProtocol = 3;
    private const byte PlayerActionProtocol = 4;
    private const byte AuthProtocol = HostAuth.AuthProtocol;
    private const byte AckProtocol = 6;
    private const byte GroupProtocol = 7;
    private const int MaxRejectLogs = 5;
    private const float HelloRetrySeconds = 2f;
    private const int MaxHelloAttempts = 10;
    private const float WeatherSendInterval = 0.1f;
    private const float StatusInterval = 0.25f;
    private const int MaxPasswordFailures = 5;
    private const float LockoutSeconds = 60f;

    private readonly Dictionary<NetPeer, byte> _readyPeers = new();
    private readonly Dictionary<NetPeer, ulong> _tokens = new();
    private readonly Dictionary<NetPeer, (int failures, float lockedUntil)> _failures = new();
    private readonly List<NetPeer> _sendList = new();
    private IFikaNetworkManager _manager;
    private bool _isServer;
    private bool _hostReady;
    private byte _hostProtocol;
    private int _helloAttempts;
    private float _nextHelloAt;
    private float _nextWeatherAt;
    private float _nextStatusAt;
    private PerunWeather? _sentWeather;
    private PerunWeather? _pendingWeather;
    private PerunTimePacket? _pendingTime;
    private float _pendingTimeAt;
    private float _now;

    private NetPeer _gameMaster;
    private string _gameMasterId;

    private bool _granted;
    private PerunWeather? _gmSentWeather;
    private HostAuth _auth;
    private bool _ackOverdueLogged;
    private int _rejectLogs;

    public bool CanClaim => _manager != null && !_isServer && _hostReady && _hostProtocol >= GameMasterProtocol;

    public void OnManagerCreated(FikaNetworkManagerCreatedEvent e)
    {
        Reset();
        _manager = e.Manager;
        _isServer = e.Manager is FikaServer;

        _manager.RegisterPacket<PerunHelloPacket, NetPeer>(OnHello);
        _manager.RegisterPacket<PerunPausePacket, NetPeer>(OnPause);
        _manager.RegisterPacket<PerunWeatherPacket, NetPeer>(OnWeather);
        _manager.RegisterPacket<PerunTimePacket, NetPeer>(OnTime);
        _manager.RegisterPacket<PerunClaimPacket, NetPeer>(OnClaim);
        _manager.RegisterPacket<PerunClaimResultPacket, NetPeer>(OnClaimResult);
        _manager.RegisterPacket<PerunReleasePacket, NetPeer>(OnRelease);
        _manager.RegisterPacket<PerunSpawnPacket, NetPeer>(OnSpawn);
        _manager.RegisterPacket<PerunGroupSpawnPacket, NetPeer>(OnSpawnGroup);
        _manager.RegisterPacket<PerunWatchPacket, NetPeer>(OnWatch);
        _manager.RegisterPacket<PerunOrderPacket, NetPeer>(OnOrder);
        _manager.RegisterPacket<PerunPauseRequestPacket, NetPeer>(OnPauseRequest);
        _manager.RegisterPacket<PerunStatusPacket, NetPeer>(OnStatus);
        _manager.RegisterPacket<PerunHealPacket, NetPeer>(OnHeal);
        _manager.RegisterPacket<PerunTeleportPacket, NetPeer>(OnTeleport);
        _manager.RegisterPacket<PerunAuthPacket, NetPeer>(OnAuth);
        _manager.RegisterPacket<PerunAuthAckPacket, NetPeer>(OnAuthAck);

        if (_isServer)
        {
            PerunNetwork.PauseBroadcast = BroadcastPause;
            PerunNetwork.TimeBroadcast = BroadcastTime;
            PerunNetwork.HealRemote = SendHeal;
            PerunNetwork.TeleportRemote = SendTeleport;
        }
        else
        {
            _auth = HostAuth.Create();
            PerunGameMaster.Link = this;
        }

        Plugin.Log.LogInfo($"Perun network ready as {(_isServer ? (PerunHost.IsHeadless ? "headless host" : "host") : "client")}");
        if (_isServer && PerunHost.AcceptsGameMaster)
        {
            Plugin.Log.LogInfo("A player with the game master password can run Perun on this headless host");
        }
    }

    public void OnManagerDestroyed(FikaNetworkManagerDestroyedEvent e)
    {
        if (_manager == null || !ReferenceEquals(e.Manager, _manager))
        {
            return;
        }

        Reset();
        PerunNetwork.ResetPause();
    }

    public void OnPeerDisconnected(PeerDisconnectedEvent e)
    {
        if (!_isServer || !ReferenceEquals(e.NetworkManager, _manager))
        {
            return;
        }

        _readyPeers.Remove(e.Peer);
        _tokens.Remove(e.Peer);
        _failures.Remove(e.Peer);
        if (e.Peer == _gameMaster)
        {
            ReleaseGameMaster("disconnected");
        }
    }

    public void OnRaidStarted(FikaRaidStartedEvent e)
    {
        if (_manager == null || _isServer || !_hostReady)
        {
            return;
        }

        SendHello();
    }

    public void Tick(float now)
    {
        _now = now;
        if (_manager == null)
        {
            return;
        }

        if (_isServer)
        {
            PollWeather(now);
            SendStatus(now);
            return;
        }

        ApplyPending();
        PollGameMasterWeather(now);
        if (!_ackOverdueLogged && _auth != null && _auth.AckOverdue(now))
        {
            _ackOverdueLogged = true;
            Plugin.Log.LogWarning("The host hasn't confirmed this client's token; host messages are ignored until it does. If the host runs DMNV-Perun older than 0.11.2, update it");
        }

        if (_hostReady || _helloAttempts >= MaxHelloAttempts || now < _nextHelloAt)
        {
            return;
        }

        NetPeer connection = (_manager as FikaClient)?.ServerConnection;
        if (connection == null || connection.ConnectionState != ConnectionState.Connected)
        {
            return;
        }

        _nextHelloAt = now + HelloRetrySeconds;
        _helloAttempts++;
        SendHello();

        if (_helloAttempts == MaxHelloAttempts)
        {
            Plugin.Log.LogWarning("The host never answered. Is DMNV-Perun installed on the host?");
        }
    }

    public void Claim(string password, string profileId)
    {
        if (!CanClaim)
        {
            return;
        }

        PerunClaimPacket packet = new() { Password = password, ProfileId = profileId };
        _manager.SendData(ref packet, Reliable, false);
    }

    public void Release()
    {
        if (!_granted || _manager == null || _isServer)
        {
            return;
        }

        _granted = false;
        PerunNetwork.TimeBroadcast = null;
        PerunReleasePacket packet = new() { Released = true };
        _manager.SendData(ref packet, Reliable, false);
    }

    public void Spawn(int role, int difficulty, int count, Vector3 position, float yaw)
    {
        if (!_granted)
        {
            return;
        }

        PerunSpawnPacket packet = new() { Role = role, Difficulty = difficulty, Count = count, Position = position, Yaw = yaw };
        _manager.SendData(ref packet, Reliable, false);
    }

    public bool SpawnGroup(PerunGroupSpawn group)
    {
        if (!_granted || _hostProtocol < GroupProtocol)
        {
            return false;
        }

        PerunGroupSpawnPacket packet = new() { Group = group };
        _manager.SendData(ref packet, Reliable, false);
        return true;
    }

    public void Watch(int role, int difficulty, int count)
    {
        if (!_granted)
        {
            return;
        }

        PerunWatchPacket packet = new() { Role = role, Difficulty = difficulty, Count = count };
        _manager.SendData(ref packet, Reliable, false);
    }

    public void Order(PerunOrderKind kind, string[] profileIds, Vector3 point, bool run, bool forced)
    {
        if (!_granted)
        {
            return;
        }

        PerunOrderPacket packet = new() { Kind = (byte)kind, Ids = profileIds, Point = point, Run = run, Forced = forced };
        _manager.SendData(ref packet, Reliable, false);
    }

    public void Pause(bool paused)
    {
        if (!_granted)
        {
            return;
        }

        PerunPauseRequestPacket packet = new() { Paused = paused };
        _manager.SendData(ref packet, Reliable, false);
    }

#if DEBUG
    // Debug builds only: what a modified client could send, to test the host trust checks and packet validation in a
    // real Fika session.
    public void SendForged()
    {
        if (_manager == null || _isServer)
        {
            Plugin.Log.LogWarning("Forged messages can only be sent from a client");
            return;
        }
        PerunHelloPacket hello = new() { Protocol = AckProtocol };
        _manager.SendData(ref hello, Reliable, true);
        PerunPausePacket unsigned = new() { Paused = !PerunNetwork.Paused };
        _manager.SendData(ref unsigned, Reliable, true);
        PerunPausePacket wrong = new() { Paused = !PerunNetwork.Paused, Token = 0x5EED5EED5EED5EEDUL };
        _manager.SendData(ref wrong, Reliable, true);
        Plugin.Log.LogWarning("Sent a forged Hello (protocol 6), an unsigned pause and a pause with a wrong token to every other player");
    }

    public void SendMalformed()
    {
        if (_manager == null || _isServer)
        {
            Plugin.Log.LogWarning("Malformed requests can only be sent from a client");
            return;
        }
        PerunSpawnPacket nowhere = new() { Role = (int)EFT.WildSpawnType.assault, Difficulty = (int)BotDifficulty.normal, Count = 1, Position = new Vector3(float.NaN, 0f, 0f) };
        _manager.SendData(ref nowhere, Reliable, false);
        PerunSpawnPacket gunner = new() { Role = (int)EFT.WildSpawnType.shooterBTR, Difficulty = (int)BotDifficulty.normal, Count = 1, Position = Vector3.zero };
        _manager.SendData(ref gunner, Reliable, false);
        PerunSpawnPacket crowd = new() { Role = (int)EFT.WildSpawnType.assault, Difficulty = (int)BotDifficulty.normal, Count = 500, Position = Vector3.zero };
        _manager.SendData(ref crowd, Reliable, false);
        PerunOrderPacket unknown = new() { Kind = 99, Ids = new string[0], Point = Vector3.zero };
        _manager.SendData(ref unknown, Reliable, false);
        Plugin.Log.LogWarning("Sent four malformed game master requests to the host (NaN spawn, BTR gunner, 500 bots, order kind 99)");
    }
#endif

    private void SendHello()
    {
        PerunHelloPacket hello = new() { Protocol = Protocol };
        _manager.SendData(ref hello, Reliable, false);
    }

    private void Invalid(NetPeer peer)
    {
        if (_rejectLogs < MaxRejectLogs)
        {
            _rejectLogs++;
            Plugin.Log.LogWarning($"Ignored a malformed Perun message from peer {peer?.Id}");
        }
    }

    private void OnHello(PerunHelloPacket packet, NetPeer peer)
    {
        if (!packet.Valid)
        {
            return;
        }
        byte protocol = HostAuth.Capped(packet.Protocol, Protocol);
        if (!_isServer)
        {
            _hostProtocol = Math.Max(_hostProtocol, protocol);
            if (_auth != null && _auth.ShouldSendToken(protocol))
            {
                PerunAuthPacket auth = new() { Token = _auth.Token };
                _manager.SendData(ref auth, Reliable, false);
                _auth.MarkSent(_now);
            }
            return;
        }

        if (!_readyPeers.ContainsKey(peer))
        {
            Plugin.Log.LogInfo($"Peer {peer.Id} joined Perun ({_readyPeers.Count + 1} connected)");
            if (protocol < AuthProtocol)
            {
                Plugin.Log.LogWarning($"Peer {peer.Id} has an older DMNV-Perun (protocol {protocol}); its host messages can't be verified");
            }
        }
        _readyPeers[peer] = protocol;

        if (protocol >= GameMasterProtocol)
        {
            PerunHelloPacket reply = new() { Protocol = Protocol };
            _manager.SendDataToPeer(ref reply, Reliable, peer);
        }

        // A peer that checks host messages would drop state sent before its token arrives, so it gets it from OnAuth.
        if (protocol < AuthProtocol || _tokens.ContainsKey(peer))
        {
            SendState(peer, protocol);
        }
    }

    private void OnAuth(PerunAuthPacket packet, NetPeer peer)
    {
        if (!_isServer || packet.Token == 0 || !_readyPeers.TryGetValue(peer, out byte protocol) || protocol < AuthProtocol)
        {
            return;
        }
        bool first = !_tokens.ContainsKey(peer);
        _tokens[peer] = packet.Token;
        if (protocol >= AckProtocol)
        {
            PerunAuthAckPacket ack = new() { Token = packet.Token };
            _manager.SendDataToPeer(ref ack, Reliable, peer);
        }
        if (first)
        {
            SendState(peer, protocol);
        }
    }

    private void OnAuthAck(PerunAuthAckPacket packet, NetPeer peer)
    {
        if (!_isServer && _auth != null && _auth.Acknowledge(packet.Token))
        {
            Plugin.Log.LogInfo("The host confirmed this client's token; host messages are now checked");
        }
    }

    private void SendState(NetPeer peer, byte protocol)
    {
        PerunPausePacket pause = new() { Paused = PerunNetwork.Paused };
        SendTo(peer, ref pause);

        if (protocol < EnvironmentProtocol)
        {
            return;
        }

        if (PerunNetwork.TryReadWeather(out PerunWeather weather) && (weather.Enabled || _sentWeather.HasValue))
        {
            PerunWeatherPacket state = new() { Weather = weather };
            SendTo(peer, ref state);
        }

        if (PerunNetwork.TimeSetThisRaid && PerunNetwork.TryReadTime(out DateTime time, out float speed))
        {
            PerunTimePacket clock = new() { Time = time.ToBinary(), Speed = speed };
            SendTo(peer, ref clock);
        }
    }

    private void SendTo<T>(NetPeer peer, ref T packet) where T : struct, INetSerializable, IHostPacket
    {
        packet.Token = _tokens.TryGetValue(peer, out ulong token) ? token : 0;
        _manager.SendDataToPeer(ref packet, Reliable, peer);
    }

    private bool FromHost(ulong token, string what)
    {
        if (_auth == null)
        {
            return true;
        }
        bool pending = _auth.State == HostTrust.Pending;
        if (_auth.Admit(token))
        {
            if (pending && _auth.State == HostTrust.Verified)
            {
                Plugin.Log.LogInfo("The host signed a message with this client's token; host messages are now checked");
            }
            return true;
        }
        if (_rejectLogs < MaxRejectLogs)
        {
            _rejectLogs++;
            Plugin.Log.LogWarning($"Ignored a {what} message that didn't come from the host");
        }
        return false;
    }

    private void OnPause(PerunPausePacket packet, NetPeer peer)
    {
        if (!packet.Valid)
        {
            Invalid(peer);
            return;
        }
        if (_isServer || !FromHost(packet.Token, "pause"))
        {
            return;
        }

        if (!_hostReady)
        {
            _hostReady = true;
            Plugin.Log.LogInfo("Host confirmed Perun");
        }

        PerunNetwork.ApplyPauseFromHost(packet.Paused);
    }

    private void OnWeather(PerunWeatherPacket packet, NetPeer peer)
    {
        if (!packet.Valid)
        {
            Invalid(peer);
            return;
        }
        if (_isServer)
        {
            if (peer != _gameMaster)
            {
                return;
            }

            PerunHost.ApplyWeather(packet.Weather);
            _sentWeather = packet.Weather;
            foreach (NetPeer other in Peers(EnvironmentProtocol))
            {
                if (other != peer)
                {
                    SendTo(other, ref packet);
                }
            }
            return;
        }

        if (!FromHost(packet.Token, "weather"))
        {
            return;
        }

        if (_granted)
        {
            _gmSentWeather = packet.Weather;
        }

        if (!PerunNetwork.ApplyWeatherFromHost(packet.Weather))
        {
            _pendingWeather = packet.Weather;
        }
        else
        {
            _pendingWeather = null;
        }
    }

    private void OnTime(PerunTimePacket packet, NetPeer peer)
    {
        if (!packet.Valid)
        {
            Invalid(peer);
            return;
        }
        if (_isServer)
        {
            if (peer == _gameMaster)
            {
                PerunHost.ApplyTime(DateTime.FromBinary(packet.Time), packet.Speed);
            }
            return;
        }

        if (!FromHost(packet.Token, "time"))
        {
            return;
        }

        if (!PerunNetwork.ApplyTimeFromHost(DateTime.FromBinary(packet.Time), packet.Speed))
        {
            _pendingTime = packet;
            _pendingTimeAt = _now;
        }
        else
        {
            _pendingTime = null;
        }
    }

    private void OnClaim(PerunClaimPacket packet, NetPeer peer)
    {
        if (!packet.Valid)
        {
            Invalid(peer);
            return;
        }
        if (!_isServer)
        {
            return;
        }

        if (!_readyPeers.ContainsKey(peer))
        {
            Answer(peer, false, "Perun hasn't finished connecting yet, try again");
            return;
        }

        if (!PerunHost.AcceptsGameMaster)
        {
            Answer(peer, false, PerunHost.IsHeadless ? "The headless host has no game master password set" : "Only the host can use Perun");
            return;
        }

        if (_failures.TryGetValue(peer, out var record) && _now < record.lockedUntil)
        {
            Answer(peer, false, $"Too many wrong passwords, wait {Mathf.CeilToInt(record.lockedUntil - _now)} s");
            return;
        }

        if (!PerunHost.CheckPassword(packet.Password))
        {
            int failures = record.failures + 1;
            float lockedUntil = failures >= MaxPasswordFailures ? _now + LockoutSeconds : 0f;
            _failures[peer] = (failures >= MaxPasswordFailures ? 0 : failures, lockedUntil);
            Plugin.Log.LogWarning($"Peer {peer.Id}: wrong game master password");
            Answer(peer, false, "Wrong game master password");
            return;
        }

        if (_gameMaster != null && _gameMaster != peer)
        {
            Answer(peer, false, $"{PerunHost.NameOf(_gameMasterId)} is already the game master");
            return;
        }

        _failures.Remove(peer);
        _gameMaster = peer;
        _gameMasterId = packet.ProfileId;
        _nextStatusAt = 0f;
        PerunHost.Protect(_gameMasterId);
        Plugin.Log.LogInfo($"{PerunHost.NameOf(_gameMasterId)} (peer {peer.Id}) is now the game master");
        Answer(peer, true, "");
    }

    private void Answer(NetPeer peer, bool granted, string message)
    {
        PerunClaimResultPacket result = new() { Granted = granted, Message = message };
        SendTo(peer, ref result);
    }

    private void OnClaimResult(PerunClaimResultPacket packet, NetPeer peer)
    {
        if (!packet.Valid)
        {
            Invalid(peer);
            return;
        }
        if (_isServer || !FromHost(packet.Token, "game master answer"))
        {
            return;
        }

        _granted = packet.Granted;
        if (_granted)
        {
            PerunNetwork.TimeBroadcast = SendTimeRequest;
            _gmSentWeather = PerunNetwork.TryReadWeather(out PerunWeather weather) ? weather : null;
            _nextWeatherAt = 0f;
        }

        PerunGameMaster.OnClaimResult(packet.Granted, packet.Message);
    }

    private void OnRelease(PerunReleasePacket packet, NetPeer peer)
    {
        if (!packet.Valid)
        {
            Invalid(peer);
            return;
        }
        if (_isServer && peer == _gameMaster)
        {
            ReleaseGameMaster("left RTS mode");
        }
    }

    private void OnSpawn(PerunSpawnPacket packet, NetPeer peer)
    {
        if (!packet.Valid)
        {
            Invalid(peer);
            return;
        }
        if (IsGameMaster(peer))
        {
            PerunHost.Spawn(packet.Role, packet.Difficulty, packet.Count, packet.Position, packet.Yaw);
            _nextStatusAt = 0f;
        }
    }

    private void OnSpawnGroup(PerunGroupSpawnPacket packet, NetPeer peer)
    {
        if (!packet.Valid)
        {
            Invalid(peer);
            return;
        }
        if (IsGameMaster(peer))
        {
            PerunHost.SpawnGroup(packet.Group);
            _nextStatusAt = 0f;
        }
    }

    private void OnWatch(PerunWatchPacket packet, NetPeer peer)
    {
        if (!packet.Valid)
        {
            Invalid(peer);
            return;
        }
        if (IsGameMaster(peer))
        {
            PerunHost.Watch(packet.Role, packet.Difficulty, packet.Count);
        }
    }

    private void OnOrder(PerunOrderPacket packet, NetPeer peer)
    {
        if (!packet.Valid)
        {
            Invalid(peer);
            return;
        }
        if (IsGameMaster(peer))
        {
            PerunHost.Order((PerunOrderKind)packet.Kind, packet.Ids, packet.Point, packet.Run, packet.Forced);
            _nextStatusAt = 0f;
        }
    }

    private void OnPauseRequest(PerunPauseRequestPacket packet, NetPeer peer)
    {
        if (!packet.Valid)
        {
            Invalid(peer);
            return;
        }
        if (IsGameMaster(peer))
        {
            PerunHost.Pause(packet.Paused);
        }
    }

    private void OnStatus(PerunStatusPacket packet, NetPeer peer)
    {
        if (!_isServer && _granted && packet.Valid && FromHost(packet.Token, "status"))
        {
            PerunGameMaster.OnStatus(packet.Status);
        }
    }

    private bool IsGameMaster(NetPeer peer) => _isServer && peer != null && peer == _gameMaster;

    private void ReleaseGameMaster(string reason)
    {
        if (_gameMaster == null)
        {
            return;
        }

        Plugin.Log.LogInfo($"{PerunHost.NameOf(_gameMasterId)} is no longer the game master ({reason})");
        _gameMaster = null;
        _gameMasterId = null;
        PerunHost.Unprotect();
    }

    private void SendStatus(float now)
    {
        if (_gameMaster == null || now < _nextStatusAt)
        {
            return;
        }

        _nextStatusAt = now + StatusInterval;
        PerunStatusPacket packet = new() { Status = PerunHost.BuildStatus() };
        SendTo(_gameMaster, ref packet);
    }

    private void SendTimeRequest(DateTime time, float speed)
    {
        if (!_granted || _manager == null)
        {
            return;
        }

        PerunTimePacket packet = new() { Time = time.ToBinary(), Speed = speed };
        _manager.SendData(ref packet, Reliable, false);
    }

    private void PollGameMasterWeather(float now)
    {
        if (!_granted || now < _nextWeatherAt)
        {
            return;
        }

        _nextWeatherAt = now + WeatherSendInterval;
        if (!PerunNetwork.TryReadWeather(out PerunWeather weather))
        {
            return;
        }

        bool changed = _gmSentWeather.HasValue ? !weather.SameAs(_gmSentWeather.Value) : weather.Enabled;
        if (!changed)
        {
            return;
        }

        _gmSentWeather = weather;
        PerunWeatherPacket packet = new() { Weather = weather };
        _manager.SendData(ref packet, Reliable, false);
    }

    private void ApplyPending()
    {
        if (_pendingWeather.HasValue && PerunNetwork.ApplyWeatherFromHost(_pendingWeather.Value))
        {
            _pendingWeather = null;
        }

        if (_pendingTime.HasValue)
        {
            PerunTimePacket packet = _pendingTime.Value;
            float waited = Math.Max(0f, _now - _pendingTimeAt);
            DateTime time = DateTime.FromBinary(packet.Time).AddSeconds(waited * packet.Speed);
            if (PerunNetwork.ApplyTimeFromHost(time, packet.Speed))
            {
                _pendingTime = null;
            }
        }
    }

    private void PollWeather(float now)
    {
        if (_readyPeers.Count == 0 || now < _nextWeatherAt)
        {
            return;
        }

        _nextWeatherAt = now + WeatherSendInterval;
        if (!PerunNetwork.TryReadWeather(out PerunWeather weather))
        {
            return;
        }

        bool changed = _sentWeather.HasValue ? !weather.SameAs(_sentWeather.Value) : weather.Enabled;
        if (!changed)
        {
            return;
        }

        _sentWeather = weather;
        PerunWeatherPacket packet = new() { Weather = weather };
        foreach (NetPeer peer in Peers(EnvironmentProtocol))
        {
            SendTo(peer, ref packet);
        }
    }

    private void BroadcastPause(bool paused)
    {
        if (_manager == null || !_isServer)
        {
            return;
        }

        PerunPausePacket packet = new() { Paused = paused };
        foreach (NetPeer peer in Peers(0))
        {
            SendTo(peer, ref packet);
        }
    }

    private void BroadcastTime(DateTime time, float speed)
    {
        if (_manager == null || !_isServer)
        {
            return;
        }

        PerunTimePacket packet = new() { Time = time.ToBinary(), Speed = speed };
        foreach (NetPeer peer in Peers(EnvironmentProtocol))
        {
            SendTo(peer, ref packet);
        }
    }

    private void SendHeal(string profileId)
    {
        if (_manager == null || !_isServer || string.IsNullOrEmpty(profileId))
        {
            return;
        }

        PerunHealPacket packet = new() { ProfileId = profileId };
        List<NetPeer> peers = Peers(PlayerActionProtocol);
        if (peers.Count == 0)
        {
            Plugin.Log.LogWarning($"Can't heal {PerunHost.NameOf(profileId)}: no player has a Perun bridge that supports healing");
            return;
        }
        foreach (NetPeer peer in peers)
        {
            SendTo(peer, ref packet);
        }
    }

    private void SendTeleport(string profileId, Vector3 position)
    {
        if (_manager == null || !_isServer || string.IsNullOrEmpty(profileId))
        {
            return;
        }

        PerunTeleportPacket packet = new() { ProfileId = profileId, Position = position };
        List<NetPeer> peers = Peers(PlayerActionProtocol);
        if (peers.Count == 0)
        {
            Plugin.Log.LogWarning($"Can't teleport {PerunHost.NameOf(profileId)}: no player has a Perun bridge that supports it");
            return;
        }
        foreach (NetPeer peer in peers)
        {
            SendTo(peer, ref packet);
        }
    }

    private void OnTeleport(PerunTeleportPacket packet, NetPeer peer)
    {
        if (!packet.Valid)
        {
            Invalid(peer);
            return;
        }
        if (!_isServer && FromHost(packet.Token, "teleport"))
        {
            PerunNetwork.TeleportFromHost(packet.ProfileId, packet.Position);
        }
    }

    private void OnHeal(PerunHealPacket packet, NetPeer peer)
    {
        if (!packet.Valid)
        {
            Invalid(peer);
            return;
        }
        if (!_isServer && FromHost(packet.Token, "heal"))
        {
            PerunNetwork.HealFromHost(packet.ProfileId);
        }
    }

    private List<NetPeer> Peers(byte minimumProtocol)
    {
        _sendList.Clear();
        foreach (KeyValuePair<NetPeer, byte> entry in _readyPeers)
        {
            if (entry.Value >= minimumProtocol && (entry.Value < AuthProtocol || _tokens.ContainsKey(entry.Key)))
            {
                _sendList.Add(entry.Key);
            }
        }
        return _sendList;
    }

    private void Reset()
    {
        if (_isServer)
        {
            PerunNetwork.PauseBroadcast = null;
            PerunNetwork.TimeBroadcast = null;
            PerunNetwork.HealRemote = null;
            PerunNetwork.TeleportRemote = null;
            ReleaseGameMaster("raid ended");
        }
        else if (_manager != null)
        {
            bool wasGranted = _granted;
            _granted = false;
            PerunNetwork.TimeBroadcast = null;
            if (ReferenceEquals(PerunGameMaster.Link, this))
            {
                PerunGameMaster.Link = null;
            }
            if (wasGranted)
            {
                PerunGameMaster.OnLost("the connection to the host ended");
            }
        }

        _manager = null;
        _isServer = false;
        _hostReady = false;
        _hostProtocol = 0;
        _helloAttempts = 0;
        _nextHelloAt = 0f;
        _nextWeatherAt = 0f;
        _nextStatusAt = 0f;
        _sentWeather = null;
        _pendingWeather = null;
        _pendingTime = null;
        _gmSentWeather = null;
        _granted = false;
        _gameMaster = null;
        _gameMasterId = null;
        _readyPeers.Clear();
        _tokens.Clear();
        _auth = null;
        _ackOverdueLogged = false;
        _rejectLogs = 0;
        _failures.Clear();
        _sendList.Clear();
    }
}
