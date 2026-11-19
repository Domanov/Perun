using System.Collections.Generic;
using EFT;

namespace Perun.Client.Units;

internal static class SpawnNames
{
    private static readonly Dictionary<WildSpawnType, string> Pretty = new();
    private static readonly Dictionary<WildSpawnType, string> Raw = new();

    public static string Of(WildSpawnType role)
    {
        if (!Pretty.TryGetValue(role, out string name))
        {
            name = UI.Theme.PrettyRole(Id(role));
            Pretty[role] = name;
        }
        return name;
    }

    public static string Id(WildSpawnType role)
    {
        if (!Raw.TryGetValue(role, out string name))
        {
            name = role.ToString();
            Raw[role] = name;
        }
        return name;
    }
}
