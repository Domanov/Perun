using System;
using System.Collections.Generic;
using EFT;
using Perun.Client.Compat;
using UnityEngine;

namespace Perun.Client.UI;

internal enum BotKind
{
    Player,
    Usec,
    Bear,
    Scav,
    Raider,
    Boss,
    Follower,
    Cultist,
    Infected,
    Custom,
    Other
}

internal static class BotKinds
{
    private const string MatchInfected = "infected";
    private const string MatchSectant = "sectant";
    private const string MatchSectantPriestEvent = "sectactPriestEvent";
    private const string MatchFollower = "follower";
    private const string MatchTagillaHelperAgro = "tagillaHelperAgro";
    private const string MatchBoss = "boss";

    private static readonly Dictionary<int, BotKind> Cache = new();
    private static readonly string[] HexCache = new string[Enum.GetValues(typeof(BotKind)).Length];

    public static int ColorVersion { get; private set; }

    public static void InvalidateColors()
    {
        Array.Clear(HexCache, 0, HexCache.Length);
        ColorVersion++;
    }

    public static BotKind Classify(Player player)
    {
        if (player == null || player.Profile == null)
        {
            return BotKind.Other;
        }

        if (!Units.Bots.IsBot(player))
        {
            return BotKind.Player;
        }

        return Classify(player.Profile.Info.Settings.Role, player.Side);
    }

    public static BotKind Classify(WildSpawnType role, EPlayerSide side)
    {
        int key = ((int)role << 3) | ((int)side & 7);
        if (!Cache.TryGetValue(key, out BotKind kind))
        {
            kind = Resolve(role, side);
            Cache[key] = kind;
        }
        return kind;
    }

    private static BotKind Resolve(WildSpawnType role, EPlayerSide side)
    {
        if (MoreBotsCompat.TryGetFaction((int)role, out _))
        {
            return BotKind.Custom;
        }

        switch (role)
        {
            case WildSpawnType.pmcUSEC:
                return BotKind.Usec;
            case WildSpawnType.pmcBEAR:
                return BotKind.Bear;
            case WildSpawnType.assault:
            case WildSpawnType.assaultGroup:
            case WildSpawnType.marksman:
            case WildSpawnType.cursedAssault:
            case WildSpawnType.crazyAssaultEvent:
                return BotKind.Scav;
            case WildSpawnType.pmcBot:
            case WildSpawnType.exUsec:
            case WildSpawnType.arenaFighter:
            case WildSpawnType.arenaFighterEvent:
                return BotKind.Raider;
        }

        string name = Units.SpawnNames.Id(role);
        if (name.StartsWith(MatchInfected, StringComparison.Ordinal))
        {
            return BotKind.Infected;
        }
        if (name.StartsWith(MatchSectant, StringComparison.Ordinal) || name == MatchSectantPriestEvent)
        {
            return BotKind.Cultist;
        }
        if (name.StartsWith(MatchFollower, StringComparison.Ordinal) || name == MatchTagillaHelperAgro)
        {
            return BotKind.Follower;
        }
        if (name.StartsWith(MatchBoss, StringComparison.Ordinal))
        {
            return BotKind.Boss;
        }

        return side switch
        {
            EPlayerSide.Usec => BotKind.Usec,
            EPlayerSide.Bear => BotKind.Bear,
            _ => BotKind.Other
        };
    }

    public static string Hex(BotKind kind)
    {
        int index = (int)kind;
        string hex = HexCache[index];
        if (hex == null)
        {
            Color color = Plugin.KindColors.TryGetValue(kind, out var entry) ? entry.Value : Color.white;
            hex = "#" + ColorUtility.ToHtmlStringRGB(color);
            HexCache[index] = hex;
        }
        return hex;
    }
}
