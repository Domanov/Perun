using System.Collections.Generic;
using Fika.Core.Networking.LiteNetLib.Utils;
using Perun.Client;
using Perun.Fika;
using UnityEngine;

namespace Perun.Tests;

public class PacketTests
{
    private static NetDataReader Write<T>(T packet) where T : INetSerializable
    {
        NetDataWriter writer = new();
        packet.Serialize(writer);
        return new NetDataReader(writer.CopyData());
    }

    private static T Read<T>(NetDataReader reader) where T : INetSerializable, new()
    {
        T packet = new();
        packet.Deserialize(reader);
        return packet;
    }

    private static T RoundTrip<T>(T packet) where T : INetSerializable, new() => Read<T>(Write(packet));

    [Fact]
    public void AnOrderSurvivesTheTrip()
    {
        PerunOrderPacket sent = new() { Kind = (byte)PerunOrderKind.Move, Ids = new[] { "a", "b" }, Point = new Vector3(1, 2, 3), Run = true, Forced = false };
        PerunOrderPacket read = RoundTrip(sent);

        Assert.True(read.Valid);
        Assert.Equal(sent.Kind, read.Kind);
        Assert.Equal(sent.Ids, read.Ids);
        Assert.Equal(sent.Point, read.Point);
        Assert.True(read.Run);
        Assert.False(read.Forced);
    }

    [Fact]
    public void AnOrderKeepsTheOldWireFormatForItsIds()
    {
        NetDataWriter legacy = new();
        legacy.Put((byte)PerunOrderKind.Halt);
        legacy.PutArray(new[] { "x", "y" }, PerunWire.IdLength);
        legacy.Put(0f);
        legacy.Put(0f);
        legacy.Put(0f);
        legacy.Put(false);
        legacy.Put(false);

        PerunOrderPacket read = Read<PerunOrderPacket>(new NetDataReader(legacy.CopyData()));

        Assert.True(read.Valid);
        Assert.Equal(new[] { "x", "y" }, read.Ids);
    }

    [Fact]
    public void AnOrderWithTooManyIdsIsRejected()
    {
        NetDataWriter writer = new();
        writer.Put((byte)PerunOrderKind.Kill);
        writer.Put((ushort)(PerunWire.MaxIds + 1));
        for (int i = 0; i <= PerunWire.MaxIds; i++)
        {
            writer.Put("id", PerunWire.IdLength);
        }

        Assert.False(Read<PerunOrderPacket>(new NetDataReader(writer.CopyData())).Valid);
    }

    [Fact]
    public void ATruncatedOrderIsRejectedWithoutThrowing()
    {
        NetDataWriter writer = new();
        writer.Put((byte)PerunOrderKind.Kill);
        writer.Put((ushort)50);
        writer.Put("only-one", PerunWire.IdLength);

        Assert.False(Read<PerunOrderPacket>(new NetDataReader(writer.CopyData())).Valid);
    }

    [Fact]
    public void AnOrderWithoutItsPointIsRejected()
    {
        NetDataWriter writer = new();
        writer.Put((byte)PerunOrderKind.Move);
        writer.PutArray(new[] { "a" }, PerunWire.IdLength);

        Assert.False(Read<PerunOrderPacket>(new NetDataReader(writer.CopyData())).Valid);
    }

    [Fact]
    public void SerialisingCapsTheIdsAtTheLimit()
    {
        string[] ids = new string[PerunWire.MaxIds + 20];
        for (int i = 0; i < ids.Length; i++)
        {
            ids[i] = $"id{i}";
        }

        PerunOrderPacket read = RoundTrip(new PerunOrderPacket { Kind = (byte)PerunOrderKind.Halt, Ids = ids });

        Assert.True(read.Valid);
        Assert.Equal(PerunWire.MaxIds, read.Ids.Length);
    }

    private static PerunStatus SampleStatus()
    {
        PerunStatus status = new() { LastResult = "ok", Pending = 2, Alive = 5, Max = 30, WatchRole = 4, WatchDifficulty = 1, ReadyCount = 3, Warming = true };
        status.Jobs.Add(new PerunJobInfo { Title = "Spawning", IsSpawn = true, Stage = 1, Progress = 0.5f, Created = 1, Total = 4, Elapsed = 2.5f });
        status.OurUnits.Add("unit1");
        status.Orders.Add(new PerunOrderInfo { ProfileId = "unit1", Destination = new Vector3(4, 5, 6), Run = true, Forced = true });
        return status;
    }

    [Fact]
    public void AStatusSurvivesTheTripWithItsToken()
    {
        PerunStatusPacket read = RoundTrip(new PerunStatusPacket { Status = SampleStatus(), Token = 99 });

        Assert.True(read.Valid);
        Assert.Equal(99UL, read.Token);
        Assert.Equal("ok", read.Status.LastResult);
        Assert.Single(read.Status.Jobs);
        Assert.Equal(4, read.Status.Jobs[0].Total);
        Assert.Equal(new List<string> { "unit1" }, read.Status.OurUnits);
        Assert.Equal(new Vector3(4, 5, 6), read.Status.Orders[0].Destination);
        Assert.True(read.Status.Orders[0].Forced);
    }

    [Fact]
    public void AStatusWithTooManyJobsIsRejected()
    {
        NetDataWriter writer = new();
        writer.Put("x", 200);
        writer.Put((ushort)0);
        writer.Put((ushort)0);
        writer.Put((ushort)0);
        writer.Put(0);
        writer.Put(0);
        writer.Put((ushort)0);
        writer.Put(false);
        writer.Put((byte)(PerunWire.MaxJobs + 1));

        PerunStatusPacket read = Read<PerunStatusPacket>(new NetDataReader(writer.CopyData()));

        Assert.False(read.Valid);
        Assert.Null(read.Status);
    }

    [Fact]
    public void APauseWithoutATokenKeepsTheOldOneByteFormat()
    {
        NetDataWriter writer = new();
        new PerunPausePacket { Paused = true }.Serialize(writer);

        Assert.Equal(1, writer.Length);
    }

    [Fact]
    public void AnOldPauseReadsAsNoToken()
    {
        NetDataWriter writer = new();
        writer.Put(true);

        PerunPausePacket read = Read<PerunPausePacket>(new NetDataReader(writer.CopyData()));

        Assert.True(read.Paused);
        Assert.Equal(0UL, read.Token);
    }

    [Fact]
    public void HostMessagesCarryTheirToken()
    {
        Assert.Equal(7UL, RoundTrip(new PerunPausePacket { Paused = true, Token = 7 }).Token);
        Assert.Equal(7UL, RoundTrip(new PerunTimePacket { Time = 123, Speed = 2f, Token = 7 }).Token);
        Assert.Equal(7UL, RoundTrip(new PerunHealPacket { ProfileId = "me", Token = 7 }).Token);
        Assert.Equal(7UL, RoundTrip(new PerunTeleportPacket { ProfileId = "me", Position = Vector3.one, Token = 7 }).Token);
        Assert.Equal(7UL, RoundTrip(new PerunClaimResultPacket { Granted = true, Message = "yes", Token = 7 }).Token);
    }

    [Fact]
    public void WeatherCarriesItsTokenWhetherOnOrOff()
    {
        Assert.Equal(5UL, RoundTrip(new PerunWeatherPacket { Weather = new PerunWeather { Enabled = false }, Token = 5 }).Token);

        PerunWeatherPacket on = RoundTrip(new PerunWeatherPacket { Weather = new PerunWeather { Enabled = true, Fog = 0.25f, WindFrom = 3 }, Token = 5 });
        Assert.Equal(5UL, on.Token);
        Assert.True(on.Weather.Enabled);
        Assert.Equal(0.25f, on.Weather.Fog);
        Assert.Equal(3, on.Weather.WindFrom);
    }

    [Fact]
    public void TheAuthPacketCarriesTheToken() => Assert.Equal(123456789UL, RoundTrip(new PerunAuthPacket { Token = 123456789 }).Token);
}
