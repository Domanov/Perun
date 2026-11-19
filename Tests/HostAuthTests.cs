using System;
using Perun.Fika;

namespace Perun.Tests;

public class HostAuthTests
{
    [Fact]
    public void ANewTokenIsNeverZero()
    {
        for (int i = 0; i < 100; i++)
        {
            Assert.NotEqual(0UL, HostAuth.Create().Token);
        }
    }

    [Fact]
    public void ZeroIsNotAValidToken() => Assert.Throws<ArgumentException>(() => new HostAuth(0));

    [Fact]
    public void BeforeTheTokenIsSentEverythingIsAccepted()
    {
        HostAuth auth = new(42);
        Assert.Equal(HostTrust.Legacy, auth.State);
        Assert.True(auth.Accepts(0));
        Assert.True(auth.Accepts(7));
    }

    [Fact]
    public void WhilePendingOnlyTheTokenIsAccepted()
    {
        HostAuth auth = new(42);
        auth.MarkSent(0f);
        Assert.Equal(HostTrust.Pending, auth.State);
        Assert.False(auth.Admit(0));
        Assert.False(auth.Admit(43));
        Assert.Equal(HostTrust.Pending, auth.State);
    }

    [Fact]
    public void ForgedHelloThenUnsignedPauseBeforeTheAckIsDropped()
    {
        HostAuth auth = new(42);
        Assert.True(auth.ShouldSendToken(HostAuth.Capped(6, 6)));
        auth.MarkSent(0f);
        Assert.False(auth.Admit(0));
        Assert.True(auth.Acknowledge(42));
        Assert.False(auth.Admit(0));
    }

    [Fact]
    public void Protocol5HostEchoingTheTokenVerifiesIt()
    {
        HostAuth auth = new(42);
        auth.MarkSent(0f);
        Assert.True(auth.Admit(42));
        Assert.Equal(HostTrust.Verified, auth.State);
        Assert.False(auth.Admit(0));
        Assert.False(auth.AckOverdue(1000f));
    }

    [Fact]
    public void Protocol4HostWithoutForgeryStaysLegacyAndAccepted()
    {
        HostAuth auth = new(42);
        Assert.False(auth.ShouldSendToken(HostAuth.Capped(4, 6)));
        Assert.True(auth.Admit(0));
        Assert.Equal(HostTrust.Legacy, auth.State);
    }

    [Fact]
    public void ForgedHelloAgainstProtocol4HostFailsClosedWithoutFallback()
    {
        HostAuth auth = new(42);
        auth.MarkSent(0f);
        Assert.True(auth.AckOverdue(HostAuth.AckTimeout));
        Assert.False(auth.Admit(0));
        Assert.Equal(HostTrust.Pending, auth.State);
        Assert.False(auth.Admit(0));
    }

    [Fact]
    public void TokenIsResentOnHellosWhilePending()
    {
        HostAuth auth = new(42);
        auth.MarkSent(0f);
        Assert.True(auth.ShouldSendToken(6));
        Assert.False(auth.ShouldSendToken(4));
        auth.MarkSent(3f);
        Assert.False(auth.AckOverdue(HostAuth.AckTimeout - 0.1f));
    }

    [Fact]
    public void OldHostHelloDoesNotSendTheToken()
    {
        HostAuth auth = new(42);
        Assert.False(auth.ShouldSendToken(4));
        Assert.True(auth.ShouldSendToken(5));
    }

    [Fact]
    public void AckWithTheWrongTokenIsIgnored()
    {
        HostAuth auth = new(42);
        auth.MarkSent(0f);
        Assert.False(auth.Acknowledge(43));
        Assert.Equal(HostTrust.Pending, auth.State);
        Assert.False(auth.Accepts(0));
    }

    [Fact]
    public void AckBeforeSendingIsIgnored()
    {
        HostAuth auth = new(42);
        Assert.False(auth.Acknowledge(42));
        Assert.Equal(HostTrust.Legacy, auth.State);
    }

    [Fact]
    public void AckWithTheRightTokenStartsChecking()
    {
        HostAuth auth = new(42);
        auth.MarkSent(0f);
        Assert.True(auth.Acknowledge(42));
        Assert.Equal(HostTrust.Verified, auth.State);
        Assert.True(auth.Accepts(42));
        Assert.False(auth.Accepts(0));
        Assert.False(auth.Accepts(43));
        Assert.False(auth.AckOverdue(1000f));
    }

    [Fact]
    public void WrongTokenAfterVerificationIsRejected()
    {
        HostAuth auth = new(42);
        auth.MarkSent(0f);
        auth.Acknowledge(42);
        Assert.False(auth.Admit(43));
        Assert.Equal(HostTrust.Verified, auth.State);
    }

    [Fact]
    public void VerifiedNeverGoesBack()
    {
        HostAuth auth = new(42);
        auth.MarkSent(0f);
        auth.Acknowledge(42);
        Assert.False(auth.ShouldSendToken(5));
        auth.MarkSent(5f);
        Assert.False(auth.Acknowledge(43));
        Assert.False(auth.Admit(0));
        Assert.Equal(HostTrust.Verified, auth.State);
    }

    [Fact]
    public void AckIsOverdueOnlyAfterTheTimeout()
    {
        HostAuth auth = new(42);
        auth.MarkSent(10f);
        Assert.False(auth.AckOverdue(10f + HostAuth.AckTimeout - 0.1f));
        Assert.True(auth.AckOverdue(10f + HostAuth.AckTimeout));
    }

    [Fact]
    public void ClaimedProtocolIsCappedToOurs() => Assert.Equal(6, (int)HostAuth.Capped(255, 6));
}
