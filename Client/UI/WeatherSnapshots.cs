using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using EFT.Weather;
using Newtonsoft.Json;
using UnityEngine;

namespace Perun.Client.UI;

internal sealed class WeatherSnapshot
{
    public string Name { get; set; }
    public string Map { get; set; }
    public DateTime SavedAt { get; set; }
    public float Hour { get; set; } = -1f;
    public float Clouds { get; set; }
    public float Fog { get; set; }
    public float Rain { get; set; }
    public float Lightning { get; set; }
    public float Wind { get; set; }
    public int WindFrom { get; set; } = (int)WeatherDebug.Direction.North;
    public float TopWindX { get; set; }
    public float TopWindY { get; set; }
    public float Temperature { get; set; }

    [JsonIgnore]
    public string Summary { get; set; }
}

internal static class WeatherSnapshots
{
    private const int Limit = 16;

    private static readonly string FilePath = Path.Combine(BepInEx.Paths.ConfigPath, "com.dmnv.perun.weather.json");
    private static List<WeatherSnapshot> _items;

    public static List<WeatherSnapshot> Items
    {
        get
        {
            if (_items == null)
            {
                Load();
            }
            return _items;
        }
    }

    public static WeatherSnapshot FromCurve(IWeatherCurve curve)
    {
        return new WeatherSnapshot
        {
            Clouds = curve.Cloudiness,
            Fog = curve.Fog,
            Rain = curve.Rain,
            Lightning = curve.LightningThunderProbability,
            Wind = curve.Wind.magnitude,
            WindFrom = (int)ToDirection(curve.Wind),
            TopWindX = curve.TopWind.x,
            TopWindY = curve.TopWind.y,
            Temperature = curve.Temperature
        };
    }

    public static WeatherSnapshot FromDebug(WeatherDebug weather)
    {
        return new WeatherSnapshot
        {
            Clouds = weather.CloudDensity,
            Fog = weather.Fog,
            Rain = weather.Rain,
            Lightning = weather.LightningThunderProbability,
            Wind = weather.WindMagnitude,
            WindFrom = (int)weather.WindDirection,
            TopWindX = weather.TopWindDirection.x,
            TopWindY = weather.TopWindDirection.y,
            Temperature = weather.Temperature
        };
    }

    public static void ApplyTo(WeatherSnapshot snapshot, WeatherDebug weather)
    {
        weather.CloudDensity = snapshot.Clouds;
        weather.Fog = snapshot.Fog;
        weather.Rain = snapshot.Rain;
        weather.LightningThunderProbability = snapshot.Lightning;
        weather.WindMagnitude = snapshot.Wind;
        weather.WindDirection = Enum.IsDefined(typeof(WeatherDebug.Direction), snapshot.WindFrom)
            ? (WeatherDebug.Direction)snapshot.WindFrom
            : WeatherDebug.Direction.North;
        weather.TopWindDirection = new Vector2(snapshot.TopWindX, snapshot.TopWindY);
        weather.Temperature = snapshot.Temperature;
    }

    public static void Add(WeatherSnapshot snapshot)
    {
        Items.Insert(0, snapshot);
        while (Items.Count > Limit)
        {
            Items.RemoveAt(Items.Count - 1);
        }
        Save();
    }

    public static void Remove(WeatherSnapshot snapshot)
    {
        if (Items.Remove(snapshot))
        {
            Save();
        }
    }

    public static string Describe(WeatherSnapshot s)
    {
        if (s.Summary != null)
        {
            return s.Summary;
        }

        string from = Enum.IsDefined(typeof(WeatherDebug.Direction), s.WindFrom) ? ((WeatherDebug.Direction)s.WindFrom).ToString() : "?";
        string time = s.Hour >= 0f ? $"{(int)s.Hour:00}:{(int)(s.Hour % 1f * 60f):00}" : "-";
        s.Summary =
            $"Clouds {(s.Clouds + 1f) * 50f:0}% · Fog {FogPercent(s.Fog):0}% · Rain {s.Rain * 100f:0}% · Lightning {s.Lightning * 100f:0}%\n" +
            $"Wind {s.Wind * 100f:0}% from {from} · {s.Temperature:0} °C · time {time}";
        return s.Summary;
    }

    public static float FogPercent(float fog) => Mathf.Sqrt(Mathf.Clamp01((fog - 0.001f) / 0.254f)) * 100f;

    private static WeatherDebug.Direction ToDirection(Vector2 wind)
    {
        if (wind.sqrMagnitude < 0.0001f)
        {
            return WeatherDebug.Direction.North;
        }

        float angle = Mathf.Repeat(Mathf.Atan2(wind.y, wind.x) * Mathf.Rad2Deg, 360f);
        int sector = Mathf.RoundToInt(angle / 45f) % 8;
        return sector switch
        {
            0 => WeatherDebug.Direction.East,
            1 => WeatherDebug.Direction.NE,
            2 => WeatherDebug.Direction.North,
            3 => WeatherDebug.Direction.NW,
            4 => WeatherDebug.Direction.West,
            5 => WeatherDebug.Direction.SW,
            6 => WeatherDebug.Direction.South,
            _ => WeatherDebug.Direction.SE
        };
    }

    private static void Load()
    {
        _items = new List<WeatherSnapshot>();
        try
        {
            if (File.Exists(FilePath))
            {
                List<WeatherSnapshot> loaded = JsonConvert.DeserializeObject<List<WeatherSnapshot>>(File.ReadAllText(FilePath));
                if (loaded != null)
                {
                    _items.AddRange(loaded);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Saved weather could not be read: {ex.Message}");
        }
    }

    private static void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(_items, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Saved weather could not be written: {ex.Message}");
        }
    }
}
