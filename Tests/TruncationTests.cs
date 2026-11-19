using System;
using System.Collections.Generic;
using EFT;
using Fika.Core.Networking.LiteNetLib.Utils;
using Perun.Client;
using Perun.Fika;
using UnityEngine;

namespace Perun.Tests;

public class TruncationTests
{
    private const ulong Token = 0x1122334455667788UL;

    private static byte[] Bytes<T>(T packet) where T : INetSerializable
    {
        NetDataWriter writer = new();
        packet.Serialize(writer);
        return writer.CopyData();
    }

    private static bool ReadValid<T>(byte[] bytes, int length) where T : struct, INetSerializable
    {
        byte[] cut = new byte[length];
        Array.Copy(bytes, cut, length);
        object packet = new T();
        ((INetSerializable)packet).Deserialize(new NetDataReader(cut));
        return typeof(T).GetField("Valid")?.GetValue(packet) is true;
    }

    private static void ValidOnlyAt<T>(T packet, params int[] validLengths) where T : struct, INetSerializable
    {
        byte[] bytes = Bytes(packet);
        HashSet<int> valid = new(validLengths) { bytes.Length };
        for (int length = 0; length <= bytes.Length; length++)
        {
            bool read;
            try
            {
                read = ReadValid<T>(bytes, length);
            }
            catch (Exception ex)
            {
                Assert.Fail($"{typeof(T).Name} cut to {length}/{bytes.Length} bytes threw {ex.GetType().Name}");
                continue;
            }
            if (read != valid.Contains(length))
            {
                Assert.Fail($"{typeof(T).Name} cut to {length}/{bytes.Length} bytes read as {(read ? "valid" : "invalid")}");
            }
        }
    }

    private static int Legacy<T>(T packet) where T : INetSerializable => Bytes(packet).Length - sizeof(ulong);

    [Fact]
    public void Hello() => ValidOnlyAt(new PerunHelloPacket { Protocol = 6 });

    [Fact]
    public void Pause()
    {
        PerunPausePacket packet = new() { Paused = true, Token = Token };
        ValidOnlyAt(packet, Legacy(packet));
    }

    [Fact]
    public void Weather()
    {
        PerunWeatherPacket packet = new() { Weather = new PerunWeather { Enabled = true, Clouds = 0.5f, Fog = 0.1f, Rain = 0.3f, Temperature = 12f, WindFrom = 3 }, Token = Token };
        ValidOnlyAt(packet, Legacy(packet));
    }

    [Fact]
    public void WeatherOff()
    {
        PerunWeatherPacket packet = new() { Weather = new PerunWeather { Enabled = false }, Token = Token };
        ValidOnlyAt(packet, Legacy(packet));
    }

    [Fact]
    public void Time()
    {
        PerunTimePacket packet = new() { Time = new DateTime(2026, 11, 18, 12, 0, 0).ToBinary(), Speed = 7f, Token = Token };
        ValidOnlyAt(packet, Legacy(packet));
    }

    [Fact]
    public void Claim() => ValidOnlyAt(new PerunClaimPacket { Password = "secret", ProfileId = "profile" });

    [Fact]
    public void ClaimResult()
    {
        PerunClaimResultPacket packet = new() { Granted = true, Message = "you're the game master", Token = Token };
        ValidOnlyAt(packet, Legacy(packet));
    }

    [Fact]
    public void Release() => ValidOnlyAt(new PerunReleasePacket { Released = true });

    [Fact]
    public void Spawn() => ValidOnlyAt(new PerunSpawnPacket { Role = 1, Difficulty = 2, Count = 3, Position = new Vector3(1, 2, 3), Yaw = 90f });

    [Fact]
    public void Watch() => ValidOnlyAt(new PerunWatchPacket { Role = 1, Difficulty = 2, Count = 3 });

    [Fact]
    public void Order() => ValidOnlyAt(new PerunOrderPacket { Kind = (byte)PerunOrderKind.Move, Ids = new[] { "abc", "defg" }, Point = new Vector3(4, 5, 6), Run = true });

    [Fact]
    public void PauseRequest() => ValidOnlyAt(new PerunPauseRequestPacket { Paused = true });

    [Fact]
    public void Status()
    {
        PerunStatus status = new() { LastResult = "spawned", Pending = 2, Alive = 5, Max = 20, WatchRole = 1, ReadyCount = 3, Warming = true };
        status.Jobs.Add(new PerunJobInfo { Title = "Preparing", IsSpawn = true, Stage = 1, Progress = 0.5f, Created = 1, Total = 4, Elapsed = 2f });
        status.OurUnits.Add("unit-a");
        status.Orders.Add(new PerunOrderInfo { ProfileId = "unit-a", Destination = new Vector3(1, 2, 3), Run = true });
        PerunStatusPacket packet = new() { Status = status, Token = Token };
        ValidOnlyAt(packet, Legacy(packet));
    }

    [Fact]
    public void Heal()
    {
        PerunHealPacket packet = new() { ProfileId = "unit-a", Token = Token };
        ValidOnlyAt(packet, Legacy(packet));
    }

    [Fact]
    public void Teleport()
    {
        PerunTeleportPacket packet = new() { ProfileId = "unit-a", Position = new Vector3(1, 2, 3), Token = Token };
        ValidOnlyAt(packet, Legacy(packet));
    }

    [Fact]
    public void WeatherThatIsNotANumberIsRejected()
    {
        byte[] bytes = Bytes(new PerunWeatherPacket { Weather = new PerunWeather { Enabled = true, Rain = float.NaN } });
        Assert.False(ReadValid<PerunWeatherPacket>(bytes, bytes.Length));
    }

    [Fact]
    public void WeatherFarOutOfRangeIsRejected()
    {
        byte[] bytes = Bytes(new PerunWeatherPacket { Weather = new PerunWeather { Enabled = true, Temperature = 5000f } });
        Assert.False(ReadValid<PerunWeatherPacket>(bytes, bytes.Length));
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(1000f)]
    [InlineData(float.PositiveInfinity)]
    public void ClockSpeedOutOfRangeIsRejected(float speed)
    {
        byte[] bytes = Bytes(new PerunTimePacket { Time = DateTime.Now.ToBinary(), Speed = speed });
        Assert.False(ReadValid<PerunTimePacket>(bytes, bytes.Length));
    }

    [Fact]
    public void TeleportToNowhereIsRejected()
    {
        byte[] bytes = Bytes(new PerunTeleportPacket { ProfileId = "a", Position = new Vector3(float.NaN, 0, 0) });
        Assert.False(ReadValid<PerunTeleportPacket>(bytes, bytes.Length));
    }

    [Fact]
    public void TooLongIdIsRejected()
    {
        NetDataWriter writer = new();
        writer.Put(new string('x', PerunWire.IdLength + 1));
        writer.Put(0f);
        writer.Put(0f);
        writer.Put(0f);
        Assert.False(ReadValid<PerunTeleportPacket>(writer.CopyData(), writer.CopyData().Length));
    }

    [Fact]
    public void AuthAckSurvivesTheTrip()
    {
        NetDataWriter writer = new();
        new PerunAuthAckPacket { Token = Token }.Serialize(writer);
        PerunAuthAckPacket read = new();
        read.Deserialize(new NetDataReader(writer.CopyData()));
        Assert.Equal(Token, read.Token);
    }

    [Fact]
    public void CompleteButMeaninglessSpawnsAreRejected()
    {
        PerunSpawnPacket good = new() { Role = (int)WildSpawnType.assault, Difficulty = (int)BotDifficulty.normal, Count = 1, Position = new Vector3(1, 2, 3), Yaw = 0f };
        Assert.True(ReadAll(good));
        Assert.True(ReadAll(good with { Count = PerunRules.MaxGroup, Yaw = PerunRules.MaxYaw }));
        Assert.False(ReadAll(good with { Role = (int)WildSpawnType.shooterBTR }));
        Assert.False(ReadAll(good with { Role = -5 }));
        Assert.False(ReadAll(good with { Difficulty = 42 }));
        Assert.False(ReadAll(good with { Count = 0 }));
        Assert.False(ReadAll(good with { Count = -3 }));
        Assert.False(ReadAll(good with { Count = 11 }));
        Assert.False(ReadAll(good with { Position = new Vector3(float.NaN, 0, 0) }));
        Assert.False(ReadAll(good with { Position = new Vector3(0, float.PositiveInfinity, 0) }));
        Assert.False(ReadAll(good with { Position = new Vector3(0, 0, 50000f) }));
        Assert.False(ReadAll(good with { Yaw = float.NaN }));
        Assert.False(ReadAll(good with { Yaw = 1000f }));
    }

    [Fact]
    public void CompleteButMeaninglessWatchesAreRejected()
    {
        PerunWatchPacket good = new() { Role = (int)WildSpawnType.pmcUSEC, Difficulty = (int)BotDifficulty.hard, Count = 4 };
        Assert.True(ReadAll(good));
        Assert.False(ReadAll(good with { Role = (int)WildSpawnType.test }));
        Assert.False(ReadAll(good with { Difficulty = -1 }));
        Assert.False(ReadAll(good with { Count = 0 }));
        Assert.False(ReadAll(good with { Count = 100 }));
    }

    [Fact]
    public void CompleteButMeaninglessOrdersAreRejected()
    {
        PerunOrderPacket good = new() { Kind = (byte)PerunOrderKind.Move, Ids = new[] { "a" }, Point = new Vector3(4, 5, 6) };
        Assert.True(ReadAll(good));
        Assert.False(ReadAll(good with { Kind = 99 }));
        Assert.False(ReadAll(good with { Point = new Vector3(float.NaN, 0, 0) }));
        Assert.False(ReadAll(good with { Kind = (byte)PerunOrderKind.Teleport, Point = new Vector3(0, 0, float.NegativeInfinity) }));
        Assert.True(ReadAll(good with { Kind = (byte)PerunOrderKind.Halt, Point = new Vector3(float.NaN, 0, 0) }));
    }

    [Fact]
    public void TeleportOutsideTheWorldIsRejected()
    {
        byte[] bytes = Bytes(new PerunTeleportPacket { ProfileId = "a", Position = new Vector3(0, 0, 20000f) });
        Assert.False(ReadValid<PerunTeleportPacket>(bytes, bytes.Length));
    }

    private static bool ReadAll<T>(T packet) where T : struct, INetSerializable
    {
        byte[] bytes = Bytes(packet);
        return ReadValid<T>(bytes, bytes.Length);
    }
}
