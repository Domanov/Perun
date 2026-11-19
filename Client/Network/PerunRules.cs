#nullable enable
using System;
using System.Collections.Generic;
using EFT;
using UnityEngine;

namespace Perun.Client;

public static class PerunRules
{
    public const int MaxGroup = 10;
    public const float WorldLimit = 10000f;
    public const float MaxYaw = 720f;

    // Roles the spawn panel hides because spawning them breaks the game: test bots, and the BTR gunner, which belongs to
    // the BTR vehicle.
    private static readonly HashSet<WildSpawnType> HiddenRoles = new()
    {
        WildSpawnType.test, WildSpawnType.bossTest, WildSpawnType.followerTest, WildSpawnType.shooterBTR,
        WildSpawnType.assaultGroup, WildSpawnType.gifter
    };

    public static bool IsKnownOrder(byte kind) => Enum.IsDefined(typeof(PerunOrderKind), kind);

    public static bool IsHiddenRole(WildSpawnType role) => HiddenRoles.Contains(role);

    public static bool IsSpawnableRole(int role) => Enum.IsDefined(typeof(WildSpawnType), role) && !IsHiddenRole((WildSpawnType)role);

    public static bool IsKnownDifficulty(int difficulty) => Enum.IsDefined(typeof(BotDifficulty), difficulty);

    public static bool IsGroupSize(int count) => count >= 1 && count <= MaxGroup;

    public static bool IsInWorld(Vector3 point) => InWorld(point.x) && InWorld(point.y) && InWorld(point.z);

    public static bool IsSaneYaw(float yaw) => IsFinite(yaw) && yaw >= -MaxYaw && yaw <= MaxYaw;

    public static bool IsSaneWatch(int role, int difficulty, int count) => IsSpawnableRole(role) && IsKnownDifficulty(difficulty) && IsGroupSize(count);

    public static bool IsSaneSpawn(int role, int difficulty, int count, Vector3 position, float yaw) =>
        IsSaneWatch(role, difficulty, count) && IsInWorld(position) && IsSaneYaw(yaw);

    public static bool IsSaneOrder(byte kind, Vector3 point)
    {
        if (!IsKnownOrder(kind))
        {
            return false;
        }
        PerunOrderKind order = (PerunOrderKind)kind;
        return (order != PerunOrderKind.Move && order != PerunOrderKind.Teleport) || IsInWorld(point);
    }

    private static bool InWorld(float value) => IsFinite(value) && value >= -WorldLimit && value <= WorldLimit;

    public static bool IsFinite(Vector3 point) => IsFinite(point.x) && IsFinite(point.y) && IsFinite(point.z);

    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public const float MaxTimeSpeed = 100f;

    public static bool IsSaneTime(long binary, float speed)
    {
        if (!IsFinite(speed) || speed < 0f || speed > MaxTimeSpeed)
        {
            return false;
        }
        try
        {
            DateTime.FromBinary(binary);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static bool IsSaneWeather(PerunWeather weather)
    {
        if (!weather.Enabled)
        {
            return true;
        }
        return Within(weather.Clouds, -2f, 2f) && Within(weather.Fog, 0f, 2f) && Within(weather.Rain, 0f, 2f) && Within(weather.Lightning, 0f, 2f)
            && Within(weather.Wind, 0f, 2f) && Within(weather.Temperature, -100f, 100f) && Within(weather.TopWindX, -100f, 100f) && Within(weather.TopWindY, -100f, 100f);
    }

    private static bool Within(float value, float min, float max) => IsFinite(value) && value >= min && value <= max;
}
