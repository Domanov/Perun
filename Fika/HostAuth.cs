#nullable enable
using System;
using System.Security.Cryptography;

namespace Perun.Fika;

internal enum HostTrust
{
    Legacy,
    Pending,
    Verified
}

// Fika relays client packets so that they look like they came from the server, so a Hello can be forged and must not
// decide whether host messages are checked. Only the host's acknowledgement of our token can, since only the host saw it.
internal sealed class HostAuth
{
    public const byte AuthProtocol = 5;
    public const float AckTimeout = 10f;

    private float _sentAt;

    public HostAuth(ulong token)
    {
        if (token == 0)
        {
            throw new ArgumentException("The token can't be zero", nameof(token));
        }
        Token = token;
    }

    public ulong Token { get; }

    public HostTrust State { get; private set; }

    // Sent again on every Hello until verified: the first copy can arrive before the host's bridge knows this peer.
    public bool ShouldSendToken(byte hostProtocol) => State != HostTrust.Verified && hostProtocol >= AuthProtocol;

    public void MarkSent(float now)
    {
        if (State == HostTrust.Legacy)
        {
            State = HostTrust.Pending;
            _sentAt = now;
        }
    }

    public bool Acknowledge(ulong token)
    {
        if (State == HostTrust.Legacy || token != Token)
        {
            return false;
        }
        State = HostTrust.Verified;
        return true;
    }

    public bool Accepts(ulong token) => State == HostTrust.Legacy || token == Token;

    // Only the host has seen the token, so a message carrying it verifies the host even without an ack (protocol 5).
    public bool Admit(ulong token)
    {
        if (!Accepts(token))
        {
            return false;
        }
        if (State == HostTrust.Pending)
        {
            State = HostTrust.Verified;
        }
        return true;
    }

    public bool AckOverdue(float now) => State == HostTrust.Pending && now - _sentAt >= AckTimeout;

    public static byte Capped(byte claimed, byte ours) => Math.Min(claimed, ours);

    public static HostAuth Create()
    {
        byte[] bytes = new byte[sizeof(ulong)];
        using RandomNumberGenerator random = RandomNumberGenerator.Create();
        ulong token;
        do
        {
            random.GetBytes(bytes);
            token = BitConverter.ToUInt64(bytes, 0);
        }
        while (token == 0);
        return new HostAuth(token);
    }
}
