using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace Perun.Fika;

// Not a BepInEx plugin: Perun.Client loads this DLL only when Fika is installed, so without Fika it is never loaded
// and can't break other mods that list every type of every loaded DLL.
public static class Plugin
{
    public const string Name = "DMNV-PerunFika";
    public const string Version = Perun.Client.Plugin.Version;

    internal static ManualLogSource Log;

    // Called by Perun.Client through reflection.
    public static void Start(GameObject host, ConfigFile config)
    {
        Log = BepInEx.Logging.Logger.CreateLogSource(Name);
        BridgeHost bridge = host.AddComponent<BridgeHost>();
        bridge.Bind(config);
        Log.LogInfo($"{Name} {Version} loaded");
    }
}
