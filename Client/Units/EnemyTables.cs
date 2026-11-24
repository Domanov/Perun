using System.Collections.Generic;
using EFT;

namespace Perun.Client.Units;

// Enemy tables are keyed by IPlayer, and with Fika the key for a player isn't always its Player object: matching by
// reference left an entry SAIN kept copying back, so Shturman shot a PMC that had joined his group.
internal static class EnemyTables
{
    private static readonly List<IPlayer> NoKeys = new();

    public static List<IPlayer> KeysOf<T>(Dictionary<IPlayer, T> table, string profileId)
    {
        List<IPlayer> keys = null;
        foreach (IPlayer key in table.Keys)
        {
            if (key != null && key.ProfileId == profileId)
            {
                (keys ??= new List<IPlayer>()).Add(key);
            }
        }
        return keys ?? NoKeys;
    }

    public static bool Lists<T>(Dictionary<IPlayer, T> table, string profileId)
    {
        foreach (IPlayer key in table.Keys)
        {
            if (key != null && key.ProfileId == profileId)
            {
                return true;
            }
        }
        return false;
    }
}
