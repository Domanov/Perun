using System;
using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;
using static Perun.Client.Compat.Constants.FikaBridgeLoader;

namespace Perun.Client.Compat;

// The bridge is an ordinary DLL next to Perun.Client.dll, loaded only with Fika: loaded without Fika, its Fika types
// can't resolve and every mod that lists all types (HarmonyX's TypeByName among them) logs errors on it.
internal static class FikaBridgeLoader
{
    public static string Version { get; private set; }

    public static void Load(string pluginFolder, GameObject host, ConfigFile config)
    {
        if (!FikaInfo.Present)
        {
            return;
        }

        string path = Path.Combine(pluginFolder, FileName);
        if (!File.Exists(path))
        {
            Plugin.Log.LogWarning($"Fika is installed but {FileName} isn't next to Perun.Client.dll; pause, weather and time won't reach other players");
            return;
        }

        try
        {
            Type entry = Assembly.LoadFrom(path).GetType(TypeBridgePlugin, true);
            entry.GetMethod(MethodStart, BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { host, config });
            Version = (string)entry.GetField(FieldVersion).GetValue(null);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Couldn't start the Fika bridge: {(ex as TargetInvocationException)?.InnerException ?? ex}");
        }
    }
}
