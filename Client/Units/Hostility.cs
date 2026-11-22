using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;

namespace Perun.Client.Units;

internal static class Hostility
{
    private static readonly Dictionary<WildSpawnType, RoleMind> Minds = new();
    private static readonly HashSet<WildSpawnType> Failed = new();
    private static object _source;

    public static int Version { get; private set; }

    public static Stance Between(WildSpawnType a, EPlayerSide aSide, WildSpawnType b, EPlayerSide bSide)
    {
        Sync();
        return HostilityRules.Between(a, Mind(a), aSide, b, Mind(b), bSide, SameFamily(a, b));
    }

    public static Stance TowardPlayer(WildSpawnType bot, EPlayerSide playerSide)
    {
        Sync();
        RoleMind mind = Mind(bot);
        return mind == null ? Stance.Unknown : mind.TowardSide(playerSide, true);
    }

    private static void Sync()
    {
        object source = Singleton<IBotGame>.Instantiated ? Singleton<IBotGame>.Instance.BotsController : null;
        if (ReferenceEquals(source, _source))
        {
            return;
        }
        _source = source;
        Minds.Clear();
        Failed.Clear();
        Version++;
    }

    private static bool SameFamily(WildSpawnType a, WildSpawnType b)
    {
        try
        {
            return WildSpawnTypeExtension.IsSuitable(a, b) || WildSpawnTypeExtension.IsSuitable(b, a);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static RoleMind Mind(WildSpawnType role)
    {
        if (Minds.TryGetValue(role, out RoleMind mind))
        {
            return mind;
        }
        if (Failed.Contains(role))
        {
            return null;
        }

        try
        {
            BotsController controller = _source as BotsController;
            bool pve = controller?.IsPvE ?? true;
            BotGlobalsMindSettings settings = BotInternalSettingsController.GetSettings(BotDifficulty.normal, role, pve)?.Mind;
            if (settings == null)
            {
                Failed.Add(role);
                return null;
            }
            mind = new RoleMind(
                settings.FRIENDLY_BOT_TYPES, settings.WARN_BOT_TYPES, settings.ENEMY_BOT_TYPES,
                settings.DEFAULT_USEC_BEHAVIOUR, settings.DEFAULT_BEAR_BEHAVIOUR, settings.DEFAULT_SAVAGE_BEHAVIOUR,
                Extra(controller, role));
            Minds[role] = mind;
            return mind;
        }
        catch (Exception ex)
        {
            Failed.Add(role);
            Plugin.Log.LogWarning($"Hostility: no settings for {role}: {ex.Message}");
            return null;
        }
    }

    private static AdditionalHostilitySettings Extra(BotsController controller, WildSpawnType role)
    {
        AdditionalHostilitySettings[] all = controller?.BotLocationModifier?.AdditionalHostilitySettings;
        if (all == null)
        {
            return null;
        }
        foreach (AdditionalHostilitySettings settings in all)
        {
            if (settings != null && settings.BotRole == role)
            {
                return settings;
            }
        }
        return null;
    }
}
