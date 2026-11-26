using System;
using System.Reflection;
using BepInEx.Bootstrap;
using static Perun.Client.Compat.Constants.ConfigManagerCompat;

namespace Perun.Client.Compat;

internal static class ConfigManagerCompat
{
    private static bool _resolved;
    private static Func<bool> _displaying;

    public static bool WindowOpen
    {
        get
        {
            if (!_resolved)
            {
                Resolve();
            }

            try
            {
                return _displaying != null && _displaying();
            }
            catch (Exception)
            {
                _displaying = null;
                return false;
            }
        }
    }

    private static void Resolve()
    {
        _resolved = true;
        if (!Chainloader.PluginInfos.TryGetValue(GuidConfigManager, out var info) || info.Instance == null)
        {
            return;
        }

        MethodInfo getter = info.Instance.GetType().GetProperty(PropertyDisplayingWindow, BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod();
        if (getter == null || getter.ReturnType != typeof(bool))
        {
            return;
        }

        _displaying = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), info.Instance, getter);
    }
}
