using Fika.Core.Networking.LiteNetLib.Utils;
using Perun.Client;
using UnityEngine;

namespace Perun.Fika;

public interface IHostPacket
{
    ulong Token { get; set; }
}

// Fika doesn't catch exceptions thrown while a packet is read; one would escape into its receive loop. So every reader
// here only uses the TryGet calls, marks the packet invalid instead of throwing, and handlers ignore invalid packets.
public static class PerunWire
{
    public const int MaxJobs = 8;
    public const int MaxIds = 128;
    public const int IdLength = 64;
    public const int MessageLength = 200;
    public const int TitleLength = 100;

    public static bool Fits(NetDataReader reader, int count, int max, int minBytesEach) => count <= max && reader.AvailableBytes >= count * minBytesEach;

    public static bool TryString(NetDataReader reader, int maxLength, out string value)
    {
        if (!reader.TryGetString(out value) || value.Length > maxLength)
        {
            value = null;
            return false;
        }
        return true;
    }

    public static bool TryVector(NetDataReader reader, out Vector3 value)
    {
        value = default;
        if (!reader.TryGetFloat(out float x) || !reader.TryGetFloat(out float y) || !reader.TryGetFloat(out float z))
        {
            return false;
        }
        value = new Vector3(x, y, z);
        return true;
    }

    public static void PutToken(NetDataWriter writer, ulong token)
    {
        if (token != 0)
        {
            writer.Put(token);
        }
    }

    public static bool TryToken(NetDataReader reader, out ulong token)
    {
        token = 0;
        return reader.AvailableBytes == 0 || reader.TryGetULong(out token);
    }
}

public struct PerunAuthPacket : INetSerializable
{
    public ulong Token;

    public void Serialize(NetDataWriter writer) => writer.Put(Token);

    public void Deserialize(NetDataReader reader) => Token = reader.TryGetULong(out ulong token) ? token : 0;
}

public struct PerunAuthAckPacket : INetSerializable
{
    public ulong Token;

    public void Serialize(NetDataWriter writer) => writer.Put(Token);

    public void Deserialize(NetDataReader reader) => Token = reader.TryGetULong(out ulong token) ? token : 0;
}

public struct PerunHelloPacket : INetSerializable
{
    public bool Valid;
    public byte Protocol;

    public void Serialize(NetDataWriter writer) => writer.Put(Protocol);

    public void Deserialize(NetDataReader reader) => Valid = reader.TryGetByte(out Protocol);
}

public struct PerunPausePacket : INetSerializable, IHostPacket
{
    public bool Valid;
    public bool Paused;

    public ulong Token { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Paused);
        PerunWire.PutToken(writer, Token);
    }

    public void Deserialize(NetDataReader reader)
    {
        Valid = reader.TryGetBool(out Paused) & PerunWire.TryToken(reader, out ulong token);
        Token = token;
    }
}

public struct PerunWeatherPacket : INetSerializable, IHostPacket
{
    public bool Valid;
    public PerunWeather Weather;

    public ulong Token { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Weather.Enabled);
        if (Weather.Enabled)
        {
            writer.Put(Weather.Clouds);
            writer.Put(Weather.Fog);
            writer.Put(Weather.Rain);
            writer.Put(Weather.Lightning);
            writer.Put(Weather.Wind);
            writer.Put(Weather.Temperature);
            writer.Put(Weather.WindFrom);
            writer.Put(Weather.TopWindX);
            writer.Put(Weather.TopWindY);
        }
        PerunWire.PutToken(writer, Token);
    }

    public void Deserialize(NetDataReader reader)
    {
        Valid = false;
        Weather = default;
        Token = 0;
        if (!reader.TryGetBool(out bool enabled))
        {
            return;
        }
        PerunWeather weather = new() { Enabled = enabled };
        if (enabled && !(reader.TryGetFloat(out weather.Clouds) && reader.TryGetFloat(out weather.Fog) && reader.TryGetFloat(out weather.Rain)
            && reader.TryGetFloat(out weather.Lightning) && reader.TryGetFloat(out weather.Wind) && reader.TryGetFloat(out weather.Temperature)
            && reader.TryGetByte(out weather.WindFrom) && reader.TryGetFloat(out weather.TopWindX) && reader.TryGetFloat(out weather.TopWindY)))
        {
            return;
        }
        if (!PerunWire.TryToken(reader, out ulong token) || !PerunRules.IsSaneWeather(weather))
        {
            return;
        }
        Weather = weather;
        Token = token;
        Valid = true;
    }
}

public struct PerunTimePacket : INetSerializable, IHostPacket
{
    public bool Valid;
    public long Time;
    public float Speed;

    public ulong Token { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Time);
        writer.Put(Speed);
        PerunWire.PutToken(writer, Token);
    }

    public void Deserialize(NetDataReader reader)
    {
        ulong token = 0;
        Valid = reader.TryGetLong(out Time) & reader.TryGetFloat(out Speed) && PerunWire.TryToken(reader, out token) && PerunRules.IsSaneTime(Time, Speed);
        Token = Valid ? token : 0;
    }
}

public struct PerunClaimPacket : INetSerializable
{
    public bool Valid;
    public string Password;
    public string ProfileId;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Password, 128);
        writer.Put(ProfileId, PerunWire.IdLength);
    }

    public void Deserialize(NetDataReader reader) => Valid = PerunWire.TryString(reader, 128, out Password) & PerunWire.TryString(reader, PerunWire.IdLength, out ProfileId);
}

public struct PerunClaimResultPacket : INetSerializable, IHostPacket
{
    public bool Valid;
    public bool Granted;
    public string Message;

    public ulong Token { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Granted);
        writer.Put(Message, PerunWire.MessageLength);
        PerunWire.PutToken(writer, Token);
    }

    public void Deserialize(NetDataReader reader)
    {
        ulong token = 0;
        Valid = reader.TryGetBool(out Granted) && PerunWire.TryString(reader, PerunWire.MessageLength, out Message) && PerunWire.TryToken(reader, out token);
        Token = Valid ? token : 0;
    }
}

public struct PerunReleasePacket : INetSerializable
{
    public bool Valid;
    public bool Released;

    public void Serialize(NetDataWriter writer) => writer.Put(Released);

    public void Deserialize(NetDataReader reader) => Valid = reader.TryGetBool(out Released);
}

public struct PerunSpawnPacket : INetSerializable
{
    public bool Valid;
    public int Role;
    public int Difficulty;
    public int Count;
    public Vector3 Position;
    public float Yaw;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Role);
        writer.Put(Difficulty);
        writer.Put(Count);
        writer.Put(Position.x);
        writer.Put(Position.y);
        writer.Put(Position.z);
        writer.Put(Yaw);
    }

    public void Deserialize(NetDataReader reader)
    {
        Valid = reader.TryGetInt(out Role) && reader.TryGetInt(out Difficulty) && reader.TryGetInt(out Count) && PerunWire.TryVector(reader, out Position) && reader.TryGetFloat(out Yaw)
            && PerunRules.IsSaneSpawn(Role, Difficulty, Count, Position, Yaw);
    }
}

public struct PerunGroupSpawnPacket : INetSerializable
{
    public bool Valid;
    public PerunGroupSpawn Group;

    public void Serialize(NetDataWriter writer)
    {
        PerunGroupSpawn group = Group ?? new PerunGroupSpawn();
        writer.Put(group.Name ?? "", PerunWire.TitleLength);
        int members = System.Math.Min(group.Members.Length, PerunRules.MaxGroupMembers);
        writer.Put((byte)members);
        for (int i = 0; i < members; i++)
        {
            PerunGroupMember member = group.Members[i];
            writer.Put(member.Role);
            writer.Put(member.Difficulty);
            int counts = System.Math.Min(member.Counts.Length, PerunRules.MaxCountOptions);
            writer.Put((byte)counts);
            for (int c = 0; c < counts; c++)
            {
                writer.Put((byte)member.Counts[c]);
            }
        }
        writer.Put(group.Leader);
        writer.Put(group.Difficulty);
        writer.Put(group.Position.x);
        writer.Put(group.Position.y);
        writer.Put(group.Position.z);
        writer.Put(group.Yaw);
        writer.Put(group.Spacing);
    }

    public void Deserialize(NetDataReader reader)
    {
        Valid = false;
        Group = null;
        if (!PerunWire.TryString(reader, PerunWire.TitleLength, out string name) || !reader.TryGetByte(out byte count)
            || !PerunWire.Fits(reader, count, PerunRules.MaxGroupMembers, 9))
        {
            return;
        }
        PerunGroupMember[] members = new PerunGroupMember[count];
        for (int i = 0; i < count; i++)
        {
            if (!reader.TryGetInt(out int role) || !reader.TryGetInt(out int difficulty) || !reader.TryGetByte(out byte options)
                || !PerunWire.Fits(reader, options, PerunRules.MaxCountOptions, 1))
            {
                return;
            }
            int[] counts = new int[options];
            for (int c = 0; c < options; c++)
            {
                if (!reader.TryGetByte(out byte value))
                {
                    return;
                }
                counts[c] = value;
            }
            members[i] = new PerunGroupMember { Role = role, Difficulty = difficulty, Counts = counts };
        }
        PerunGroupSpawn group = new() { Name = name, Members = members };
        if (!reader.TryGetInt(out group.Leader) || !reader.TryGetInt(out group.Difficulty) || !PerunWire.TryVector(reader, out group.Position)
            || !reader.TryGetFloat(out group.Yaw) || !reader.TryGetFloat(out group.Spacing) || !PerunRules.IsSaneGroup(group))
        {
            return;
        }
        Group = group;
        Valid = true;
    }
}

public struct PerunWatchPacket : INetSerializable
{
    public bool Valid;
    public int Role;
    public int Difficulty;
    public int Count;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Role);
        writer.Put(Difficulty);
        writer.Put(Count);
    }

    public void Deserialize(NetDataReader reader) =>
        Valid = reader.TryGetInt(out Role) && reader.TryGetInt(out Difficulty) && reader.TryGetInt(out Count) && PerunRules.IsSaneWatch(Role, Difficulty, Count);
}

public struct PerunOrderPacket : INetSerializable
{
    public bool Valid;
    public byte Kind;
    public string[] Ids;
    public Vector3 Point;
    public bool Run;
    public bool Forced;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Kind);
        string[] ids = Ids ?? new string[0];
        int count = System.Math.Min(ids.Length, PerunWire.MaxIds);
        writer.Put((ushort)count);
        for (int i = 0; i < count; i++)
        {
            writer.Put(ids[i], PerunWire.IdLength);
        }
        writer.Put(Point.x);
        writer.Put(Point.y);
        writer.Put(Point.z);
        writer.Put(Run);
        writer.Put(Forced);
    }

    public void Deserialize(NetDataReader reader)
    {
        Valid = false;
        Ids = new string[0];
        if (!reader.TryGetByte(out Kind) || !reader.TryGetUShort(out ushort count) || !PerunWire.Fits(reader, count, PerunWire.MaxIds, 2))
        {
            return;
        }
        string[] ids = new string[count];
        for (int i = 0; i < count; i++)
        {
            if (!PerunWire.TryString(reader, PerunWire.IdLength, out ids[i]))
            {
                return;
            }
        }
        if (!PerunWire.TryVector(reader, out Point) || !reader.TryGetBool(out Run) || !reader.TryGetBool(out Forced) || !PerunRules.IsSaneOrder(Kind, Point))
        {
            return;
        }
        Ids = ids;
        Valid = true;
    }
}

public struct PerunPauseRequestPacket : INetSerializable
{
    public bool Valid;
    public bool Paused;

    public void Serialize(NetDataWriter writer) => writer.Put(Paused);

    public void Deserialize(NetDataReader reader) => Valid = reader.TryGetBool(out Paused);
}

public struct PerunStatusPacket : INetSerializable, IHostPacket
{
    public bool Valid;
    public PerunStatus Status;

    public ulong Token { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        PerunStatus s = Status;
        writer.Put(s.LastResult, PerunWire.MessageLength);
        writer.Put((ushort)s.Pending);
        writer.Put((ushort)s.Alive);
        writer.Put((ushort)s.Max);
        writer.Put(s.WatchRole);
        writer.Put(s.WatchDifficulty);
        writer.Put((ushort)s.ReadyCount);
        writer.Put(s.Warming);

        int jobs = System.Math.Min(s.Jobs.Count, PerunWire.MaxJobs);
        writer.Put((byte)jobs);
        for (int i = 0; i < jobs; i++)
        {
            PerunJobInfo job = s.Jobs[i];
            writer.Put(job.Title, PerunWire.TitleLength);
            writer.Put(job.IsSpawn);
            writer.Put(job.Stage);
            writer.Put(job.Progress);
            writer.Put((ushort)job.Created);
            writer.Put((ushort)job.Total);
            writer.Put(job.Elapsed);
        }

        int units = System.Math.Min(s.OurUnits.Count, PerunWire.MaxIds);
        writer.Put((ushort)units);
        for (int i = 0; i < units; i++)
        {
            writer.Put(s.OurUnits[i], PerunWire.IdLength);
        }

        int orders = System.Math.Min(s.Orders.Count, PerunWire.MaxIds);
        writer.Put((ushort)orders);
        for (int i = 0; i < orders; i++)
        {
            PerunOrderInfo order = s.Orders[i];
            writer.Put(order.ProfileId, PerunWire.IdLength);
            writer.Put(order.Destination.x);
            writer.Put(order.Destination.y);
            writer.Put(order.Destination.z);
            writer.Put((byte)((order.Run ? 1 : 0) | (order.Forced ? 2 : 0)));
        }
        PerunWire.PutToken(writer, Token);
    }

    public void Deserialize(NetDataReader reader)
    {
        Valid = false;
        Status = null;
        Token = 0;
        PerunStatus s = new();
        if (!PerunWire.TryString(reader, PerunWire.MessageLength, out s.LastResult)
            || !reader.TryGetUShort(out ushort pending) || !reader.TryGetUShort(out ushort alive) || !reader.TryGetUShort(out ushort max)
            || !reader.TryGetInt(out s.WatchRole) || !reader.TryGetInt(out s.WatchDifficulty)
            || !reader.TryGetUShort(out ushort ready) || !reader.TryGetBool(out s.Warming))
        {
            return;
        }
        s.Pending = pending;
        s.Alive = alive;
        s.Max = max;
        s.ReadyCount = ready;

        if (!reader.TryGetByte(out byte jobs) || !PerunWire.Fits(reader, jobs, PerunWire.MaxJobs, 16))
        {
            return;
        }
        for (int i = 0; i < jobs; i++)
        {
            PerunJobInfo job = new();
            if (!PerunWire.TryString(reader, PerunWire.TitleLength, out job.Title) || !reader.TryGetBool(out job.IsSpawn) || !reader.TryGetByte(out job.Stage)
                || !reader.TryGetFloat(out job.Progress) || !reader.TryGetUShort(out ushort created) || !reader.TryGetUShort(out ushort total) || !reader.TryGetFloat(out job.Elapsed))
            {
                return;
            }
            job.Created = created;
            job.Total = total;
            s.Jobs.Add(job);
        }

        if (!reader.TryGetUShort(out ushort units) || !PerunWire.Fits(reader, units, PerunWire.MaxIds, 2))
        {
            return;
        }
        for (int i = 0; i < units; i++)
        {
            if (!PerunWire.TryString(reader, PerunWire.IdLength, out string id))
            {
                return;
            }
            s.OurUnits.Add(id);
        }

        if (!reader.TryGetUShort(out ushort orders) || !PerunWire.Fits(reader, orders, PerunWire.MaxIds, 15))
        {
            return;
        }
        for (int i = 0; i < orders; i++)
        {
            if (!PerunWire.TryString(reader, PerunWire.IdLength, out string id) || !PerunWire.TryVector(reader, out Vector3 destination) || !reader.TryGetByte(out byte flags))
            {
                return;
            }
            s.Orders.Add(new PerunOrderInfo { ProfileId = id, Destination = destination, Run = (flags & 1) != 0, Forced = (flags & 2) != 0 });
        }

        if (!PerunWire.TryToken(reader, out ulong token))
        {
            return;
        }
        Status = s;
        Token = token;
        Valid = true;
    }
}

public struct PerunHealPacket : INetSerializable, IHostPacket
{
    public bool Valid;
    public string ProfileId;

    public ulong Token { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(ProfileId ?? "");
        PerunWire.PutToken(writer, Token);
    }

    public void Deserialize(NetDataReader reader)
    {
        ulong token = 0;
        Valid = PerunWire.TryString(reader, PerunWire.IdLength, out ProfileId) && PerunWire.TryToken(reader, out token);
        Token = Valid ? token : 0;
    }
}

public struct PerunTeleportPacket : INetSerializable, IHostPacket
{
    public bool Valid;
    public string ProfileId;
    public Vector3 Position;

    public ulong Token { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(ProfileId ?? "");
        writer.Put(Position.x);
        writer.Put(Position.y);
        writer.Put(Position.z);
        PerunWire.PutToken(writer, Token);
    }

    public void Deserialize(NetDataReader reader)
    {
        ulong token = 0;
        Valid = PerunWire.TryString(reader, PerunWire.IdLength, out ProfileId) && PerunWire.TryVector(reader, out Position) && PerunWire.TryToken(reader, out token) && PerunRules.IsInWorld(Position);
        Token = Valid ? token : 0;
    }
}
