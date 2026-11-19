using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace Perun.Client.Compat;

internal static class FikaInfo
{
    private static bool _resolved;
    private static PropertyInfo _isServer;
    private static PropertyInfo _isHeadless;

    public static bool Present => Chainloader.PluginInfos.ContainsKey("com.fika.core");

    public static bool IsServer => Present && Read(_isServer, false);

    public static bool IsHeadless => Present && Read(_isHeadless, false);

    private static bool Read(PropertyInfo property, bool fallback)
    {
        Resolve();
        if (property == null)
        {
            return fallback;
        }

        try
        {
            return (bool)property.GetValue(null);
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    private static void Resolve()
    {
        if (_resolved)
        {
            return;
        }

        _resolved = true;
        Type utils = Type.GetType("Fika.Core.Main.Utils.FikaBackendUtils, Fika.Core");
        if (utils == null)
        {
            Plugin.Log.LogWarning("Fika found but FikaBackendUtils is missing; Perun stays off because the host can't be confirmed");
            return;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        _isServer = utils.GetProperty("IsServer", flags);
        _isHeadless = utils.GetProperty("IsHeadless", flags);
        if (_isServer == null)
        {
            Plugin.Log.LogWarning("Fika found but FikaBackendUtils.IsServer is missing; Perun stays off because the host can't be confirmed");
        }
    }
}
