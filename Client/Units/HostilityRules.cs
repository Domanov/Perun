using System;
using System.Collections.Generic;
using EFT;

namespace Perun.Client.Units;

internal enum Attitude
{
    Unknown,
    Ally,
    Neutral,
    Warns,
    Chance,
    Enemy
}

internal readonly struct Stance
{
    public Stance(Attitude kind, int chance = 0)
    {
        Kind = kind;
        Chance = chance;
    }

    public Attitude Kind { get; }
    public int Chance { get; }

    public static readonly Stance Unknown = new(Attitude.Unknown);
    public static readonly Stance Ally = new(Attitude.Ally);
    public static readonly Stance Neutral = new(Attitude.Neutral);
    public static readonly Stance Warns = new(Attitude.Warns);
    public static readonly Stance Enemy = new(Attitude.Enemy);

    public static Stance Worst(Stance a, Stance b)
    {
        if (a.Kind == Attitude.Unknown || b.Kind == Attitude.Unknown)
        {
            return Unknown;
        }
        if (a.Kind != b.Kind)
        {
            return a.Kind > b.Kind ? a : b;
        }
        return a.Chance >= b.Chance ? a : b;
    }
}

internal sealed class SideRule
{
    public EWarnBehaviour Extra;
    public int ExtraChance;
    public EWarnBehaviour Default;
}

// Mirrors BotSettings.GetFriendly/Warn/EnemyBotTypes, BotSettings.IsPlayerEnemy and BotsGroup.IsPlayerEnemy
// for one viewing role, so attitudes can be shown before a bot of that role exists.
internal sealed class RoleMind
{
    private readonly HashSet<WildSpawnType> _friendly = new();
    private readonly HashSet<WildSpawnType> _warn = new();
    private readonly HashSet<WildSpawnType> _enemy = new();
    private readonly Dictionary<WildSpawnType, int> _chanced = new();
    private readonly SideRule _usec = new();
    private readonly SideRule _bear = new();
    private readonly SideRule _savage = new();

    public RoleMind(
        WildSpawnType[] friendly, WildSpawnType[] warn, WildSpawnType[] enemy,
        EWarnBehaviour usec, EWarnBehaviour bear, EWarnBehaviour savage,
        AdditionalHostilitySettings extra)
    {
        Add(_friendly, friendly);
        Add(_warn, warn);
        Add(_enemy, enemy);
        _usec.Default = usec;
        _bear.Default = bear;
        _savage.Default = savage;

        if (extra == null)
        {
            return;
        }

        Add(_friendly, extra.AlwaysFriends);
        Add(_friendly, extra.Neutral);
        Remove(_friendly, extra.AlwaysEnemies);

        Add(_warn, extra.Warn);
        Remove(_warn, extra.AlwaysFriends);
        Remove(_warn, extra.Neutral);
        Remove(_warn, extra.AlwaysEnemies);

        Add(_enemy, extra.AlwaysEnemies);
        Remove(_enemy, extra.AlwaysFriends);
        Remove(_enemy, extra.Neutral);
        Remove(_enemy, extra.Warn);
        if (extra.ChancedEnemies != null)
        {
            foreach (AdditionalHostilitySettings.ChancedEnemy chanced in extra.ChancedEnemies)
            {
                if (chanced == null)
                {
                    continue;
                }
                _enemy.Remove(chanced.Role);
                _chanced[chanced.Role] = chanced.EnemyChance;
            }
        }

        _usec.Extra = extra.UsecPlayerBehaviour;
        _usec.ExtraChance = extra.UsecEnemyChance;
        _bear.Extra = extra.BearPlayerBehaviour;
        _bear.ExtraChance = extra.BearEnemyChance;
        _savage.Extra = extra.SavagePlayerBehaviour;
        _savage.ExtraChance = extra.SavageEnemyChance;
    }

    public Stance TowardBot(WildSpawnType role, EPlayerSide side)
    {
        if (_friendly.Contains(role))
        {
            return Stance.Ally;
        }
        if (_warn.Contains(role))
        {
            return Stance.Neutral;
        }
        if (_enemy.Contains(role))
        {
            return Stance.Enemy;
        }
        if (_chanced.TryGetValue(role, out int chance))
        {
            return Chance(chance);
        }
        return TowardSide(side, false);
    }

    // Toward a bot, "warn" means only "not an enemy"; toward a human the bot warns before shooting.
    public Stance TowardSide(EPlayerSide side, bool human)
    {
        Stance warned = human ? Stance.Warns : Stance.Neutral;
        SideRule rule = side switch
        {
            EPlayerSide.Usec => _usec,
            EPlayerSide.Bear => _bear,
            _ => _savage
        };

        bool defaultEnemy = Has(rule.Default, EWarnBehaviour.AlwaysEnemies);
        EWarnBehaviour extra = rule.Extra;
        if (Has(extra, EWarnBehaviour.AlwaysFriends))
        {
            return Stance.Ally;
        }
        if (Has(extra, EWarnBehaviour.Neutral))
        {
            return Stance.Neutral;
        }
        if (Has(extra, EWarnBehaviour.Warn))
        {
            return warned;
        }
        if (Has(extra, EWarnBehaviour.AlwaysEnemies))
        {
            return Stance.Enemy;
        }
        if (Has(extra, EWarnBehaviour.ChancedEnemies) && !defaultEnemy)
        {
            return Chance(rule.ExtraChance);
        }

        if (defaultEnemy)
        {
            return Stance.Enemy;
        }
        if (Has(rule.Default, EWarnBehaviour.Warn))
        {
            return warned;
        }
        return Has(rule.Default, EWarnBehaviour.Neutral) ? Stance.Neutral : Stance.Ally;
    }

    private static Stance Chance(int chance)
    {
        if (chance >= 100)
        {
            return Stance.Enemy;
        }
        return chance <= 0 ? Stance.Neutral : new Stance(Attitude.Chance, chance);
    }

    private static bool Has(EWarnBehaviour value, EWarnBehaviour flag) => (value & flag) != 0;

    private static void Add(HashSet<WildSpawnType> set, WildSpawnType[] roles)
    {
        if (roles != null)
        {
            set.UnionWith(roles);
        }
    }

    private static void Remove(HashSet<WildSpawnType> set, WildSpawnType[] roles)
    {
        if (roles != null)
        {
            set.ExceptWith(roles);
        }
    }
}

internal static class HostilityRules
{
    private const string ColorAlly = "#8be38b";
    private const string ColorNeutral = "#9fb4c8";
    private const string ColorWarns = "#ffd36f";
    private const string ColorChance = "#ffa04d";
    private const string ColorEnemy = "#ff8080";

    private const string LabelAlly = "ally";
    private const string LabelNeutral = "neutral";
    private const string LabelWarns = "warns";
    private const string LabelHostileChance = "hostile ";
    private const string LabelHostile = "hostile";

    private const string MarkupColorOpen = "<color=";
    private const string MarkupTagEnd = ">";
    private const string MarkupColorClose = "</color>";

    public static Stance Between(WildSpawnType a, RoleMind aMind, EPlayerSide aSide, WildSpawnType b, RoleMind bMind, EPlayerSide bSide, bool sameFamily)
    {
        if (a == b || sameFamily)
        {
            return Stance.Ally;
        }
        if (aMind == null || bMind == null)
        {
            return Stance.Unknown;
        }
        return Stance.Worst(aMind.TowardBot(b, bSide), bMind.TowardBot(a, aSide));
    }

    public static EPlayerSide SideOf(WildSpawnType role) => role switch
    {
        WildSpawnType.pmcUSEC => EPlayerSide.Usec,
        WildSpawnType.pmcBEAR => EPlayerSide.Bear,
        _ => EPlayerSide.Savage
    };

    public static string Hex(Attitude kind) => kind switch
    {
        Attitude.Ally => ColorAlly,
        Attitude.Neutral => ColorNeutral,
        Attitude.Warns => ColorWarns,
        Attitude.Chance => ColorChance,
        Attitude.Enemy => ColorEnemy,
        _ => null
    };

    public static string Badge(Stance stance) => stance.Kind switch
    {
        Attitude.Ally => Tint(stance.Kind, LabelAlly),
        Attitude.Neutral => Tint(stance.Kind, LabelNeutral),
        Attitude.Warns => Tint(stance.Kind, LabelWarns),
        Attitude.Chance => Tint(stance.Kind, LabelHostileChance + stance.Chance + "%"),
        Attitude.Enemy => Tint(stance.Kind, LabelHostile),
        _ => ""
    };

    private static string Tint(Attitude kind, string text) => MarkupColorOpen + Hex(kind) + MarkupTagEnd + text + MarkupColorClose;
}
