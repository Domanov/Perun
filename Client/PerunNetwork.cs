using System;
using EFT;
using EFT.Weather;
using Perun.Client.Mode;
using Perun.Client.World;
using UnityEngine;

namespace Perun.Client;

public struct PerunWeather
{
    private const float Epsilon = 0.0005f;

    public bool Enabled;
    public float Clouds;
    public float Fog;
    public float Rain;
    public float Lightning;
    public float Wind;
    public float Temperature;
    public byte WindFrom;
    public float TopWindX;
    public float TopWindY;

    public bool SameAs(PerunWeather other)
    {
        if (Enabled != other.Enabled)
        {
            return false;
        }

        if (!Enabled)
        {
            return true;
        }

        return WindFrom == other.WindFrom
            && Near(Clouds, other.Clouds) && Near(Fog, other.Fog) && Near(Rain, other.Rain)
            && Near(Lightning, other.Lightning) && Near(Wind, other.Wind) && Near(Temperature, other.Temperature)
            && Near(TopWindX, other.TopWindX) && Near(TopWindY, other.TopWindY);
    }

    private static bool Near(float a, float b) => Mathf.Abs(a - b) < Epsilon;
}

public static class PerunNetwork
{
    public static bool Paused => WorldPause.Paused;

    public static Action<bool> PauseBroadcast
    {
        get => WorldPause.Broadcast;
        set => WorldPause.Broadcast = value;
    }

    public static Action<DateTime, float> TimeBroadcast
    {
        get => WorldEnvironment.TimeChanged;
        set => WorldEnvironment.TimeChanged = value;
    }

    public static bool TimeSetThisRaid => WorldEnvironment.TimeSetThisRaid;

    public static Func<Player, bool> ObservedBot
    {
        get => Units.Bots.ObservedBot;
        set => Units.Bots.ObservedBot = value;
    }

    public static Action<string> HealRemote { get; set; }

    public static Action<string, Vector3> TeleportRemote { get; set; }

    public static bool TeleportFromHost(string profileId, Vector3 position)
    {
        if (string.IsNullOrEmpty(profileId) || !Comfort.Common.Singleton<GameWorld>.Instantiated)
        {
            return false;
        }
        Player me = Comfort.Common.Singleton<GameWorld>.Instance.MainPlayer;
        if (me == null || me.ProfileId != profileId || me.HealthController == null || !me.HealthController.IsAlive)
        {
            return false;
        }
        me.Teleport(position);
        Plugin.Log.LogInfo("Teleported by the game master");
        return true;
    }

    public static bool HealFromHost(string profileId)
    {
        if (string.IsNullOrEmpty(profileId) || !Comfort.Common.Singleton<GameWorld>.Instantiated)
        {
            return false;
        }
        Player me = Comfort.Common.Singleton<GameWorld>.Instance.MainPlayer;
        if (me == null || me.ProfileId != profileId)
        {
            return false;
        }
        bool healed = Units.Healing.HealLocal(me);
        if (healed)
        {
            Plugin.Log.LogInfo("Healed by the game master");
        }
        return healed;
    }

    public static void ApplyPauseFromHost(bool paused) => WorldPause.Set(paused, false);

    public static void ResetPause() => WorldPause.ResetSilently();

    public static bool TryReadWeather(out PerunWeather weather)
    {
        weather = default;
        WeatherDebug debug = WorldEnvironment.Weather;
        if (debug == null)
        {
            return false;
        }

        weather.Enabled = debug.Enabled;
        if (!weather.Enabled)
        {
            return true;
        }

        weather.Clouds = debug.CloudDensity;
        weather.Fog = debug.Fog;
        weather.Rain = debug.Rain;
        weather.Lightning = debug.LightningThunderProbability;
        weather.Wind = debug.WindMagnitude;
        weather.Temperature = debug.Temperature;
        weather.WindFrom = (byte)debug.WindDirection;
        weather.TopWindX = debug.TopWindDirection.x;
        weather.TopWindY = debug.TopWindDirection.y;
        return true;
    }

    public static bool ApplyWeatherFromHost(PerunWeather weather)
    {
        WeatherDebug debug = WorldEnvironment.Weather;
        if (debug == null)
        {
            return false;
        }

        if (!weather.Enabled)
        {
            WorldEnvironment.DisableCustom();
            return true;
        }

        WorldEnvironment.EnableCustom();
        debug.CloudDensity = weather.Clouds;
        debug.Fog = weather.Fog;
        debug.Rain = weather.Rain;
        debug.LightningThunderProbability = weather.Lightning;
        debug.WindMagnitude = weather.Wind;
        debug.Temperature = weather.Temperature;
        debug.WindDirection = Enum.IsDefined(typeof(WeatherDebug.Direction), (int)weather.WindFrom)
            ? (WeatherDebug.Direction)weather.WindFrom
            : WeatherDebug.Direction.North;
        debug.TopWindDirection = new Vector2(weather.TopWindX, weather.TopWindY);
        return true;
    }

    public static bool TryReadTime(out DateTime time, out float speed)
    {
        GameDateTime clock = WorldEnvironment.Clock;
        if (clock == null)
        {
            time = default;
            speed = 0f;
            return false;
        }

        time = clock.Calculate();
        speed = WorldEnvironment.EffectiveSpeed(clock);
        return true;
    }

    public static bool ApplyTimeFromHost(DateTime time, float speed) => WorldEnvironment.ApplyTime(time, speed);
}
