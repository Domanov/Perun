using BepInEx;
using BepInEx.Bootstrap;
#if DEBUG
using BepInEx.Configuration;
#endif
using BepInEx.Logging;
using UnityEngine;

namespace Perun.Fika;

// Fika is optional so one zip serves single player too: without it the bridge logs one line and stays off.
// Nothing here may name a Fika type; those live in FikaHooks, which is only loaded when Fika is.
[BepInPlugin(Guid, Name, Version)]
[BepInDependency("com.SPT.core", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency(FikaGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(Perun.Client.Plugin.Guid, BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "com.dmnv.perunfika";
    public const string Name = "DMNV-PerunFika";
    public const string Version = Perun.Client.Plugin.Version;
    private const string FikaGuid = "com.fika.core";

    internal static ManualLogSource Log;

    private bool _running;
#if DEBUG
    private ConfigEntry<bool> _forge;
    private ConfigEntry<bool> _malformed;
#endif

    private void Awake()
    {
        Log = Logger;
        if (!Chainloader.PluginInfos.ContainsKey(FikaGuid))
        {
            Log.LogInfo($"{Name} {Version}: Fika isn't installed, so the Fika bridge stays off");
            return;
        }

#if DEBUG
        _forge = Config.Bind("Debug build", "Send forged host messages", false, "Sends a Hello claiming protocol 6, an unsigned pause and a pause with a wrong token to every other player, the way a modified client would. Debug builds only.");
        _malformed = Config.Bind("Debug build", "Send malformed game master requests", false, "Sends a NaN spawn, a BTR gunner spawn, a 500-bot spawn and an unknown order to the host. Debug builds only.");
#endif
        FikaHooks.Start();
        _running = true;
        Log.LogInfo($"{Name} {Version} loaded");
    }

    private void Update()
    {
        if (!_running)
        {
            return;
        }
        FikaHooks.Tick(Time.unscaledTime);
#if DEBUG
        if (_forge.Value)
        {
            _forge.Value = false;
            FikaHooks.SendForged();
        }
        if (_malformed.Value)
        {
            _malformed.Value = false;
            FikaHooks.SendMalformed();
        }
#endif
    }

    private void OnDestroy()
    {
        if (_running)
        {
            FikaHooks.Stop();
        }
    }
}
