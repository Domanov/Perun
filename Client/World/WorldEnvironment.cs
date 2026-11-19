using System;
using Comfort.Common;
using EFT;
using EFT.Weather;
using Perun.Client.Mode;
using UnityEngine;

namespace Perun.Client.World;

internal static class WorldEnvironment
{
    private static WeatherController _densityOwner;
    private static float? _savedDensity;

    public static Action<DateTime, float> TimeChanged { get; set; }

    public static bool TimeSetThisRaid { get; private set; }

    private static GameWorld _world;

    public static GameDateTime Clock
    {
        get
        {
            CheckWorld();
            return Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance.GameDateTime : null;
        }
    }

    public static WeatherDebug Weather
    {
        get
        {
            WeatherController controller = WeatherController.Instance;
            return controller != null ? controller.WeatherDebug : null;
        }
    }

    public static void EnableCustom()
    {
        WeatherController controller = WeatherController.Instance;
        WeatherDebug weather = controller != null ? controller.WeatherDebug : null;
        if (weather == null || weather.Enabled)
        {
            return;
        }

        TOD_Scattering scattering = Scattering();
        if (scattering != null)
        {
            _densityOwner = controller;
            _savedDensity = scattering.GlobalDensity;
        }

        weather.CopyParams(controller.WeatherCurve);
        weather.Enabled = true;
    }

    public static void DisableCustom()
    {
        WeatherController controller = WeatherController.Instance;
        WeatherDebug weather = controller != null ? controller.WeatherDebug : null;
        if (weather == null)
        {
            return;
        }

        weather.Enabled = false;
        TOD_Scattering scattering = Scattering();
        if (_savedDensity.HasValue && _densityOwner == controller && scattering != null)
        {
            scattering.GlobalDensity = _savedDensity.Value;
        }
        _savedDensity = null;
        _densityOwner = null;
    }

    public static bool SetTime(float hour)
    {
        GameDateTime clock = Clock;
        if (clock == null)
        {
            return false;
        }

        DateTime now = clock.Calculate();
        int minutes = Mathf.Clamp(Mathf.RoundToInt(hour * 60f), 0, 24 * 60 - 1);
        DateTime target = new(now.Year, now.Month, now.Day, minutes / 60, minutes % 60, 0, now.Kind);
        clock.ResetForce(target);
        Plugin.Log.LogInfo($"Time set to {target:HH:mm}");
        AnnounceTime();
        return true;
    }

    public static bool SetSpeed(float speed)
    {
        GameDateTime clock = Clock;
        if (clock == null)
        {
            return false;
        }

        speed = Mathf.Max(0f, speed);
        if (WorldPause.Paused)
        {
            WorldPause.SetResumeFactor(speed);
        }
        else
        {
            clock.Reset(DateTimeExtensions.UtcNow, clock.Calculate(), speed, true);
        }
        Plugin.Log.LogInfo($"Time speed set to x{speed:0.#}");
        AnnounceTime();
        return true;
    }

    public static float EffectiveSpeed(GameDateTime clock) => WorldPause.Paused && WorldPause.ResumeFactor >= 0f ? WorldPause.ResumeFactor : clock.TimeFactor;

    public static void AnnounceTime()
    {
        GameDateTime clock = Clock;
        if (clock == null)
        {
            return;
        }

        TimeSetThisRaid = true;
        TimeChanged?.Invoke(clock.Calculate(), EffectiveSpeed(clock));
    }

    public static bool ApplyTime(DateTime time, float speed)
    {
        GameDateTime clock = Clock;
        if (clock == null)
        {
            return false;
        }

        clock.ResetForce(time);
        speed = Mathf.Max(0f, speed);
        if (WorldPause.Paused)
        {
            WorldPause.SetResumeFactor(speed);
        }
        else
        {
            clock.Reset(DateTimeExtensions.UtcNow, clock.Calculate(), speed, true);
        }
        return true;
    }

    private static void CheckWorld()
    {
        GameWorld world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
        if (world != _world)
        {
            _world = world;
            TimeSetThisRaid = false;
            _savedDensity = null;
            _densityOwner = null;
        }
    }

    private static TOD_Scattering Scattering()
    {
        Camera camera = EFT.CameraControl.CameraManager.Exist ? EFT.CameraControl.CameraManager.Instance.Camera : null;
        return camera != null ? camera.GetComponent<TOD_Scattering>() : null;
    }
}
