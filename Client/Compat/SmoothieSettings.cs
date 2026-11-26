using System.Collections.Generic;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using static Perun.Client.Compat.Constants.SmoothieSettings;

namespace Perun.Client.Compat;

internal sealed class SmoothieSettings
{
    private static readonly ConfigDefinition HideCovered = new(ConfigSectionSmoothieOtherFixes, ConfigKeySmoothieHideCovered);

    private readonly List<(ConfigEntryBase entry, object value)> _saved = new();
    private ConfigFile _config;
    private bool _warned;

    public void Apply()
    {
        Restore();
        if (!Chainloader.PluginInfos.TryGetValue(GuidSmoothie, out var info) || info.Instance == null)
        {
            return;
        }

        _config = info.Instance.Config;
        Override(HideCovered, false);
    }

    public void Restore()
    {
        if (_config == null || _saved.Count == 0)
        {
            _saved.Clear();
            return;
        }

        bool save = _config.SaveOnConfigSet;
        _config.SaveOnConfigSet = false;
        for (int i = _saved.Count - 1; i >= 0; i--)
        {
            _saved[i].entry.BoxedValue = _saved[i].value;
        }
        _config.SaveOnConfigSet = save;

        if (Plugin.Verbose.Value)
        {
            Plugin.Log.LogInfo($"Smoothie: restored {_saved.Count} setting(s)");
        }
        _saved.Clear();
    }

    private void Override(ConfigDefinition definition, object value)
    {
        if (!_config.ContainsKey(definition))
        {
            if (!_warned)
            {
                _warned = true;
                Plugin.Log.LogWarning($"Smoothie: setting \"{definition.Section} / {definition.Key}\" not found, leaving Smoothie alone");
            }
            return;
        }

        ConfigEntryBase entry = _config[definition];
        if (Equals(entry.BoxedValue, value))
        {
            return;
        }

        _saved.Add((entry, entry.BoxedValue));
        bool save = _config.SaveOnConfigSet;
        _config.SaveOnConfigSet = false;
        entry.BoxedValue = value;
        _config.SaveOnConfigSet = save;

        if (Plugin.Verbose.Value)
        {
            Plugin.Log.LogInfo($"Smoothie: \"{definition.Key}\" set to {value} for RTS mode");
        }
    }
}
