using System;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using Perun.Client.Compat;
using UnityEngine;

namespace Perun.Client;

// With Verbose logging on: the setup once per game session and each raid once, so a shared log shows what Perun ran with.
internal static class Diagnostics
{
    private static bool _setupLogged;
    private static GameWorld _raidLogged;

    public static void Tick(ConfigFile config)
    {
        if (!Plugin.Verbose.Value)
        {
            return;
        }

        if (!_setupLogged)
        {
            _setupLogged = true;
            Plugin.Log.LogInfo(Setup(config));
        }

        GameWorld world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
        if (world == null || ReferenceEquals(world, _raidLogged) || world.MainPlayer == null || world.MainPlayer is HideoutPlayer)
        {
            return;
        }
        _raidLogged = world;
        Plugin.Log.LogInfo(Raid(world));
    }

    private static string Setup(ConfigFile config)
    {
        StringBuilder text = new();
        text.AppendLine($"Diagnostics: {Plugin.Name} {Plugin.Version}");
        text.AppendLine($"  Game {Application.version}, Unity {Application.unityVersion}, {SystemInfo.operatingSystem}");
        text.AppendLine($"  Screen {Screen.width}x{Screen.height} {Screen.fullScreenMode}");
        text.AppendLine($"  Fika: {(FikaInfo.Present ? $"installed, {Bridge()}" : "not installed")}");

        text.AppendLine($"  Plugins ({Chainloader.PluginInfos.Count}):");
        foreach (PluginInfo plugin in Chainloader.PluginInfos.Values.OrderBy(p => p.Metadata.GUID, StringComparer.OrdinalIgnoreCase))
        {
            string file = string.IsNullOrEmpty(plugin.Location) ? "?" : Path.GetFileName(plugin.Location);
            text.AppendLine($"    {plugin.Metadata.GUID} {plugin.Metadata.Version} ({plugin.Metadata.Name}, {file})");
        }

        text.AppendLine("  Perun settings:");
        foreach (ConfigEntryBase entry in config.Select(pair => pair.Value).OrderBy(e => e.Definition.Section).ThenBy(e => e.Definition.Key))
        {
            string value = entry.Definition.Key.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0
                ? (string.IsNullOrEmpty(entry.BoxedValue as string) ? "(empty)" : "(set)")
                : entry.GetSerializedValue();
            text.AppendLine($"    [{entry.Definition.Section}] {entry.Definition.Key} = {value}");
        }
        return text.ToString().TrimEnd();
    }

    private static string Raid(GameWorld world)
    {
        Player main = world.MainPlayer;
        StringBuilder text = new();
        text.AppendLine($"Diagnostics: raid on {world.LocationId}");
        text.AppendLine($"  Fika: {FikaRole()}");
        text.AppendLine($"  Your character: {main.Profile?.Info?.Side} level {main.Profile?.Info?.Level}");
        try
        {
            text.AppendLine($"  Game time {world.GameDateTime?.Calculate():HH:mm}");
        }
        catch (Exception ex)
        {
            text.AppendLine($"  Game time unknown ({ex.GetType().Name})");
        }
        text.AppendLine($"  Alive players and bots: {world.AllAlivePlayersList?.Count ?? 0}");
        return text.ToString().TrimEnd();
    }

    private static string FikaRole()
    {
        if (!FikaInfo.Present)
        {
            return "not installed";
        }
        string role = FikaInfo.IsHeadless ? "headless host" : FikaInfo.IsServer ? "host" : "client";
        return $"{role}, {Bridge()}";
    }

    private static string Bridge() =>
        FikaBridgeLoader.Version != null ? $"Perun bridge {FikaBridgeLoader.Version}" : "no Perun bridge";
}
