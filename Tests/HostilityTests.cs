using EFT;
using Perun.Client.Units;

namespace Perun.Tests;

public class HostilityTests
{
    private static RoleMind Bully() => new(
        new WildSpawnType[0],
        new[] { WildSpawnType.assault },
        new[] { WildSpawnType.pmcBEAR, WildSpawnType.pmcUSEC },
        EWarnBehaviour.AlwaysEnemies, EWarnBehaviour.AlwaysEnemies, EWarnBehaviour.Warn,
        null);

    private static RoleMind Scav() => new(
        new WildSpawnType[0], new WildSpawnType[0], new WildSpawnType[0],
        EWarnBehaviour.AlwaysEnemies, EWarnBehaviour.AlwaysEnemies, EWarnBehaviour.Default,
        null);

    private static RoleMind Usec(AdditionalHostilitySettings extra) => new(
        new WildSpawnType[0], new WildSpawnType[0], new WildSpawnType[0],
        EWarnBehaviour.Default, EWarnBehaviour.AlwaysEnemies, EWarnBehaviour.AlwaysEnemies,
        extra);

    [Fact]
    public void ListsWinOverSideDefaults()
    {
        RoleMind bully = Bully();

        Assert.Equal(Attitude.Neutral, bully.TowardBot(WildSpawnType.assault, EPlayerSide.Savage).Kind);
        Assert.Equal(Attitude.Enemy, bully.TowardBot(WildSpawnType.pmcUSEC, EPlayerSide.Usec).Kind);
        Assert.Equal(Attitude.Neutral, bully.TowardBot(WildSpawnType.marksman, EPlayerSide.Savage).Kind);
    }

    [Fact]
    public void BadgeIsTheWorseOfBothDirections()
    {
        Stance stance = HostilityRules.Between(
            WildSpawnType.bossBully, Bully(), EPlayerSide.Savage,
            WildSpawnType.assault, Scav(), EPlayerSide.Savage,
            false);

        Assert.Equal(Attitude.Neutral, stance.Kind);
    }

    [Fact]
    public void SameFamilyIsAllyWhateverTheLists()
    {
        Stance stance = HostilityRules.Between(
            WildSpawnType.bossBully, Bully(), EPlayerSide.Savage,
            WildSpawnType.pmcUSEC, Scav(), EPlayerSide.Usec,
            true);

        Assert.Equal(Attitude.Ally, stance.Kind);
    }

    [Fact]
    public void ChancedEnemiesShowTheirChance()
    {
        AdditionalHostilitySettings extra = new()
        {
            BotRole = WildSpawnType.pmcUSEC,
            ChancedEnemies = new[] { new AdditionalHostilitySettings.ChancedEnemy { Role = WildSpawnType.pmcUSEC, EnemyChance = 85 } },
            SavagePlayerBehaviour = EWarnBehaviour.ChancedEnemies,
            SavageEnemyChance = 95
        };
        RoleMind usec = Usec(extra);

        Stance toUsec = usec.TowardBot(WildSpawnType.pmcUSEC, EPlayerSide.Usec);
        Assert.Equal(Attitude.Chance, toUsec.Kind);
        Assert.Equal(85, toUsec.Chance);
    }

    [Fact]
    public void ChanceDoesNotSoftenAnEnemyDefault()
    {
        AdditionalHostilitySettings extra = new()
        {
            BotRole = WildSpawnType.pmcUSEC,
            SavagePlayerBehaviour = EWarnBehaviour.ChancedEnemies,
            SavageEnemyChance = 95
        };

        Assert.Equal(Attitude.Enemy, Usec(extra).TowardSide(EPlayerSide.Savage, false).Kind);
    }

    [Fact]
    public void ExtraFriendsRemoveRolesFromTheEnemyList()
    {
        AdditionalHostilitySettings extra = new()
        {
            BotRole = WildSpawnType.bossBully,
            AlwaysFriends = new[] { WildSpawnType.pmcUSEC }
        };
        RoleMind bully = new(
            new WildSpawnType[0], new WildSpawnType[0], new[] { WildSpawnType.pmcUSEC },
            EWarnBehaviour.AlwaysEnemies, EWarnBehaviour.AlwaysEnemies, EWarnBehaviour.Warn,
            extra);

        Assert.Equal(Attitude.Ally, bully.TowardBot(WildSpawnType.pmcUSEC, EPlayerSide.Usec).Kind);
    }

    [Fact]
    public void WarnBehaviourIsNeutralToBotsAndWarnsHumans()
    {
        RoleMind bully = Bully();

        Assert.Equal(Attitude.Neutral, bully.TowardSide(EPlayerSide.Savage, false).Kind);
        Assert.Equal(Attitude.Warns, bully.TowardSide(EPlayerSide.Savage, true).Kind);
        Assert.Equal(Attitude.Enemy, bully.TowardSide(EPlayerSide.Usec, true).Kind);
    }

    [Fact]
    public void BadgeWordsMatchTheAttitude()
    {
        Assert.Contains(">neutral<", HostilityRules.Badge(Stance.Neutral));
        Assert.Contains(">warns<", HostilityRules.Badge(Stance.Warns));
        Assert.Contains(">hostile 85%<", HostilityRules.Badge(new Stance(Attitude.Chance, 85)));
        Assert.Contains(">hostile<", HostilityRules.Badge(Stance.Enemy));
    }

    [Fact]
    public void UnknownMindGivesNoBadge()
    {
        Stance stance = HostilityRules.Between(
            WildSpawnType.bossBully, null, EPlayerSide.Savage,
            WildSpawnType.assault, Scav(), EPlayerSide.Savage,
            false);

        Assert.Equal("", HostilityRules.Badge(stance));
    }
}
