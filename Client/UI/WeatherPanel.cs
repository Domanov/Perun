using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Weather;
using Perun.Client.World;
using UnityEngine;
using static Perun.Client.UI.Constants.WeatherPanel;

namespace Perun.Client.UI;

internal sealed class WeatherPanel
{
    private static readonly (string name, float hour)[] TimePresets =
    {
        (LabelDawn, 5.5f), (LabelMorning, 9f), (LabelNoon, 12f), (LabelDusk, 19.5f), (LabelNight, 23f), (LabelDeepNight, 2f)
    };

    private static readonly string[] WindNames = { LabelWindE, LabelWindN, LabelWindW, LabelWindS, LabelWindSe, LabelWindSw, LabelWindNw, LabelWindNe };

    private float _hour = 12f;
    private float _speed = 7f;
    private bool _hourEdited;
    private bool _speedEdited;
    private int _nowKey = -1;
    private float _nowFactor = -1f;
    private string _nowText = "";
    private WeatherSnapshot _open;
    private WeatherSnapshot _pendingOpen;
    private bool _pendingToggle;
    private WeatherSnapshot _pendingRemove;

    public void DrawContent()
    {
        GameWorld world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
        GameDateTime clock = world?.GameDateTime;
        WeatherController weather = WeatherController.Instance;

        if (Event.current.type == EventType.Layout)
        {
            ApplyPending();
        }

        DrawTime(clock);
        GUILayout.Space(10f);
        DrawWeather(weather);
        GUILayout.Space(10f);
        DrawSaved(world, clock, weather);
    }

    private void ApplyPending()
    {
        if (_pendingRemove != null)
        {
            if (_open == _pendingRemove)
            {
                _open = null;
            }
            WeatherSnapshots.Remove(_pendingRemove);
            _pendingRemove = null;
        }

        if (_pendingToggle)
        {
            _open = _open == _pendingOpen ? null : _pendingOpen;
            _pendingToggle = false;
            _pendingOpen = null;
        }
    }

    private void DrawSaved(GameWorld world, GameDateTime clock, WeatherController controller)
    {
        GUILayout.Label(LabelSavedWeather, Theme.Header);
        WeatherDebug weather = controller != null ? controller.WeatherDebug : null;

        GUI.enabled = weather != null;
        if (GUILayout.Button(weather != null && weather.Enabled ? ChipSaveCustom : ChipSaveCurrent, Theme.Button))
        {
            Capture(world, clock, controller, weather);
        }
        GUI.enabled = true;

        List<WeatherSnapshot> items = WeatherSnapshots.Items;
        if (items.Count == 0)
        {
            GUILayout.Label(LabelNothingSaved, Theme.Small);
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            WeatherSnapshot item = items[i];
            bool open = item == _open;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button((open ? "▾ " : "▸ ") + item.Name, open ? Theme.RowSelected : Theme.Row))
            {
                _pendingOpen = item;
                _pendingToggle = true;
            }
            if (GUILayout.Button("✕", Theme.Button, GUILayout.Width(26f)))
            {
                _pendingRemove = item;
            }
            GUILayout.EndHorizontal();

            if (!open)
            {
                continue;
            }

            GUILayout.Label(WeatherSnapshots.Describe(item), Theme.Small);
            GUILayout.BeginHorizontal();
            GUI.enabled = weather != null;
            if (GUILayout.Button(ChipApply, Theme.Button))
            {
                Apply(item, controller, weather, null);
            }
            GUI.enabled = weather != null && clock != null && item.Hour >= 0f;
            if (GUILayout.Button(ChipApplyTime, Theme.Button))
            {
                Apply(item, controller, weather, clock);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }
    }

    private static void Capture(GameWorld world, GameDateTime clock, WeatherController controller, WeatherDebug weather)
    {
        if (controller == null || weather == null)
        {
            return;
        }

        WeatherSnapshot snapshot = weather.Enabled || controller.WeatherCurve == null
            ? WeatherSnapshots.FromDebug(weather)
            : WeatherSnapshots.FromCurve(controller.WeatherCurve);

        string time = "";
        if (clock != null)
        {
            DateTime now = clock.Calculate();
            snapshot.Hour = now.Hour + now.Minute / 60f;
            time = $" {now:HH:mm}";
        }

        snapshot.Map = world != null ? world.LocationId : "";
        snapshot.SavedAt = DateTime.Now;
        snapshot.Name = $"{Kind(snapshot)} · {MapName(snapshot.Map)}{time}";
        WeatherSnapshots.Add(snapshot);
        Plugin.Log.LogInfo($"Weather saved: {snapshot.Name} ({WeatherSnapshots.Describe(snapshot).Replace('\n', ' ')})");
    }

    private void Apply(WeatherSnapshot snapshot, WeatherController controller, WeatherDebug weather, GameDateTime clock)
    {
        WorldEnvironment.EnableCustom();
        WeatherSnapshots.ApplyTo(snapshot, weather);
        if (clock != null && snapshot.Hour >= 0f)
        {
            SetTime(snapshot.Hour);
        }
        Plugin.Log.LogInfo($"Weather applied: {snapshot.Name}");
    }

    private static string Kind(WeatherSnapshot s)
    {
        if (s.Rain > 0.6f && s.Lightning > 0.3f) return LabelStorm;
        if (s.Rain > 0.05f) return LabelRain;
        if (WeatherSnapshots.FogPercent(s.Fog) > 40f) return LabelFog;
        if (s.Clouds > 0.2f) return LabelCloudy;
        return LabelClear;
    }

    private static string MapName(string id)
    {
        switch ((id ?? "").ToLowerInvariant())
        {
            case LocationCustoms: return NameMapCustoms;
            case LocationFactoryDay: return NameMapFactory;
            case LocationFactoryNight: return NameMapNightFactory;
            case LocationInterchange: return NameMapInterchange;
            case LocationLabs: return NameMapLabs;
            case LocationLighthouse: return NameMapLighthouse;
            case LocationReserve: return NameMapReserve;
            case LocationShoreline: return NameMapShoreline;
            case LocationStreets: return NameMapStreets;
            case LocationWoods: return NameMapWoods;
            case LocationGroundZero: return NameMapGroundZero;
            case LocationGroundZeroHigh: return NameMapGroundZero;
            case LocationLabyrinth: return NameMapLabyrinth;
            case "": return NameMapRaid;
            default: return id;
        }
    }

    private void DrawTime(GameDateTime clock)
    {
        GUILayout.Label(LabelTime, Theme.Header);
        if (clock == null)
        {
            GUILayout.Label(LabelNoRaidClock, Theme.Small);
            return;
        }

        DateTime now = clock.Calculate();
        if (!_hourEdited)
        {
            _hour = now.Hour + now.Minute / 60f;
        }
        if (!_speedEdited)
        {
            _speed = WorldEnvironment.EffectiveSpeed(clock);
        }

        int nowKey = now.Hour * 60 + now.Minute;
        float factor = WorldEnvironment.EffectiveSpeed(clock);
        if (nowKey != _nowKey || !Mathf.Approximately(factor, _nowFactor))
        {
            _nowKey = nowKey;
            _nowFactor = factor;
            _nowText = string.Format(LabelClockNow, Theme.Time(now), Theme.Factor(_nowFactor));
        }
        GUILayout.Label(_nowText, Theme.Label);

        GUILayout.BeginHorizontal();
        GUILayout.Label(Theme.Time(_hour), Theme.Label, GUILayout.Width(44f));
        float hour = GUILayout.HorizontalSlider(_hour, 0f, 23.99f);
        if (!Mathf.Approximately(hour, _hour))
        {
            _hour = hour;
            _hourEdited = true;
        }
        if (GUILayout.Button(ChipSet, Theme.Button, GUILayout.Width(48f)))
        {
            SetTime(_hour);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        for (int i = 0; i < TimePresets.Length; i++)
        {
            if (i == 3)
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
            }
            if (GUILayout.Button(TimePresets[i].name, Theme.Button))
            {
                SetTime(TimePresets[i].hour);
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label(Theme.Factor(_speed), Theme.Label, GUILayout.Width(44f));
        float speed = Mathf.Round(GUILayout.HorizontalSlider(_speed, 0f, 60f));
        if (!Mathf.Approximately(speed, _speed))
        {
            _speed = speed;
            _speedEdited = true;
        }
        if (GUILayout.Button(ChipSet, Theme.Button, GUILayout.Width(48f)))
        {
            SetSpeed(_speed);
        }
        GUILayout.EndHorizontal();
        GUILayout.Label(LabelClockHint, Theme.Small);
    }

    private void DrawWeather(WeatherController controller)
    {
        GUILayout.Label(LabelWeather, Theme.Header);
        WeatherDebug weather = controller != null ? controller.WeatherDebug : null;
        if (weather == null)
        {
            GUILayout.Label(LabelNoWeatherOn, Theme.Small);
            return;
        }

        bool overridden = weather.Enabled;
        bool wantOverride = GUILayout.Toggle(overridden, overridden ? LabelCustomWeatherOn : LabelCustomWeatherOff, Theme.Tab);
        if (wantOverride != overridden)
        {
            if (wantOverride)
            {
                WorldEnvironment.EnableCustom();
            }
            else
            {
                WorldEnvironment.DisableCustom();
            }
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(LabelClear, Theme.Button)) Preset(controller, weather, -1f, 0f, 0f, 0f, 0.1f);
        if (GUILayout.Button(LabelCloudy, Theme.Button)) Preset(controller, weather, 0.4f, 0.01f, 0f, 0f, 0.3f);
        if (GUILayout.Button(LabelRain, Theme.Button)) Preset(controller, weather, 0.8f, 0.02f, 0.6f, 0.1f, 0.5f);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(LabelStorm, Theme.Button)) Preset(controller, weather, 1f, 0.03f, 1f, 1f, 0.9f);
        if (GUILayout.Button(LabelFog, Theme.Button)) Preset(controller, weather, 0.3f, 0.2f, 0f, 0f, 0.05f);
        if (GUILayout.Button(LabelNatural, Theme.Button)) WorldEnvironment.DisableCustom();
        GUILayout.EndHorizontal();

        GUI.enabled = weather.Enabled;
        weather.CloudDensity = Slider(LabelClouds, weather.CloudDensity, -1f, 1f, Theme.Percent(Mathf.RoundToInt((weather.CloudDensity + 1f) * 50f)));
        weather.Fog = FogSlider(weather.Fog);
        weather.Rain = Slider(LabelRain, weather.Rain, 0f, 1f, Theme.Percent(Mathf.RoundToInt(weather.Rain * 100f)));
        weather.LightningThunderProbability = Slider(LabelLightning, weather.LightningThunderProbability, 0f, 1f, Theme.Percent(Mathf.RoundToInt(weather.LightningThunderProbability * 100f)));
        weather.WindMagnitude = Slider(LabelWind, weather.WindMagnitude, 0f, 1f, Theme.Percent(Mathf.RoundToInt(weather.WindMagnitude * 100f)));

        GUILayout.BeginHorizontal();
        GUILayout.Label(LabelFrom, Theme.Label, GUILayout.Width(66f));
        int current = Mathf.Clamp((int)weather.WindDirection - 1, 0, WindNames.Length - 1);
        int picked = GUILayout.SelectionGrid(current, WindNames, 4, Theme.Tab);
        if (picked != current)
        {
            weather.WindDirection = (WeatherDebug.Direction)(picked + 1);
        }
        GUILayout.EndHorizontal();

        weather.Temperature = Mathf.Round(Slider(LabelTemp, weather.Temperature, -30f, 40f, Theme.Celsius(weather.Temperature)));
        GUI.enabled = true;
    }

    private static float Slider(string label, float value, float min, float max, string shown)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, Theme.Label, GUILayout.Width(66f));
        float result = GUILayout.HorizontalSlider(value, min, max);
        GUILayout.Label(shown, Theme.Label, GUILayout.Width(52f));
        GUILayout.EndHorizontal();
        return result;
    }

    private static float FogSlider(float fog)
    {
        float amount = Mathf.Sqrt(Mathf.Clamp01((fog - 0.001f) / 0.254f));
        amount = Slider(LabelFog, amount, 0f, 1f, Theme.Percent(Mathf.RoundToInt(amount * 100f)));
        return 0.001f + amount * amount * 0.254f;
    }

    private static void Preset(WeatherController controller, WeatherDebug weather, float clouds, float fog, float rain, float lightning, float wind)
    {
        WorldEnvironment.EnableCustom();

        weather.CloudDensity = clouds;
        weather.Fog = fog;
        weather.Rain = rain;
        weather.LightningThunderProbability = lightning;
        weather.WindMagnitude = wind;
    }

    private void SetTime(float hour)
    {
        if (WorldEnvironment.SetTime(hour))
        {
            _hourEdited = false;
        }
    }

    private void SetSpeed(float speed)
    {
        if (WorldEnvironment.SetSpeed(speed))
        {
            _speedEdited = false;
        }
    }
}
