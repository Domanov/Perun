using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using Perun.Client.Units;

namespace Perun.Tests;

public class BossPresetsTests
{
    private static BossLocationSpawn Wave(string boss, string escort, string amount, params WildSpawnSupports[] supports) => new()
    {
        BossName = boss,
        BossDifficult = "normal",
        BossEscortType = escort,
        BossEscortDifficult = "hard",
        BossEscortAmount = amount,
        Supports = supports
    };

    private static WildSpawnSupports Support(WildSpawnType role, int amount) => new()
    {
        BossEscortType = role,
        BossEscortAmount = amount,
        BossEscortDifficult = new[] { "normal" }
    };

    [Fact]
    public void PmcAndGifterWavesAreNotPresets()
    {
        List<GroupPlan> plans = BossPresets.Build(new[]
        {
            ("bigmap", Wave("pmcUSEC", "pmcUSEC", "0,1,2")),
            ("bigmap", Wave("gifter", "gifter", "0")),
            ("bigmap", Wave("bossBully", "followerBully", "4"))
        });

        Assert.Single(plans);
        Assert.Equal("Reshala", plans[0].Name);
        Assert.Equal(new[] { WildSpawnType.bossBully, WildSpawnType.followerBully }, plans[0].Members.Select(m => m.Role));
        Assert.Equal(5, plans[0].Max);
        Assert.Equal(BotDifficulty.hard, plans[0].Members[1].Difficulty);
    }

    [Fact]
    public void SupportsReplaceThePlainEscort()
    {
        List<GroupPlan> plans = BossPresets.Build(new[]
        {
            ("rezervbase", Wave("bossGluhar", "followerGluharSecurity", "2",
                Support(WildSpawnType.followerGluharAssault, 2),
                Support(WildSpawnType.followerGluharSecurity, 2),
                Support(WildSpawnType.followerGluharScout, 2)))
        });

        Assert.Equal(new[] { WildSpawnType.bossGluhar, WildSpawnType.followerGluharAssault, WildSpawnType.followerGluharSecurity, WildSpawnType.followerGluharScout },
            plans[0].Members.Select(m => m.Role));
        Assert.Equal(7, plans[0].Max);
    }

    [Fact]
    public void ZeroSupportsAndZeroEscortsAreDropped()
    {
        List<GroupPlan> plans = BossPresets.Build(new[]
        {
            ("bigmap", Wave("bossKnight", "exUsec", "2",
                Support(WildSpawnType.followerBigPipe, 1),
                Support(WildSpawnType.followerBirdEye, 1),
                Support(WildSpawnType.followerGluharScout, 0))),
            ("interchange", Wave("bossKilla", "followerTagilla", "0"))
        });

        GroupPlan goons = plans.Single(p => p.Name == "Goons");
        Assert.Equal(3, goons.Max);
        GroupPlan killa = plans.Single(p => p.Name == "Killa");
        Assert.Single(killa.Members);
    }

    [Fact]
    public void SameGroupOnSeveralMapsMergesMapsAndCountOptions()
    {
        List<GroupPlan> plans = BossPresets.Build(new[]
        {
            ("lighthouse", Wave("exUsec", "exUsec", "1")),
            ("lighthouse", Wave("exUsec", "exUsec", "1,2")),
            ("woods", Wave("sectantPriest", "sectantWarrior", "4")),
            ("shoreline", Wave("sectantPriest", "sectantWarrior", "3,4"))
        });

        GroupPlan rogues = plans.Single(p => p.Name == "Rogues");
        Assert.Equal("2–3", rogues.CountText);
        GroupPlan cultists = plans.Single(p => p.Name == "Cultists");
        Assert.Equal(new[] { "Woods", "Shoreline" }, cultists.Maps);
        Assert.Equal("4–5", cultists.CountText);
    }

    [Fact]
    public void DifferentGroupsOfOneBossGetTheMapInTheName()
    {
        List<GroupPlan> plans = BossPresets.Build(new[]
        {
            ("tarkovstreets", Wave("bossKolontay", "followerKolontaySecurity", "2", Support(WildSpawnType.followerKolontayAssault, 2))),
            ("sandbox_high", Wave("bossKolontay", "followerKolontaySecurity", "2"))
        });

        Assert.Equal(new[] { "Kollontay · Ground Zero", "Kollontay · Streets" }, plans.Select(p => p.Name));
    }

    [Fact]
    public void RollKeepsTheLeaderAndRespectsTheBudget()
    {
        GroupPlan plan = new("Test", new[]
        {
            new GroupMember(WildSpawnType.bossBully, BotDifficulty.normal, new[] { 1 }),
            new GroupMember(WildSpawnType.followerBully, BotDifficulty.normal, new[] { 4 })
        }, null);

        var parts = plan.Roll(new Random(1), 3, BotDifficulty.hard);

        Assert.Equal(WildSpawnType.bossBully, parts[0].role);
        Assert.Equal(1, parts[0].count);
        Assert.Equal(2, parts[1].count);
        Assert.All(parts, p => Assert.Equal(BotDifficulty.hard, p.difficulty));
    }

    [Fact]
    public void RollSkipsMembersThatRolledZero()
    {
        GroupPlan plan = new("Test", new[]
        {
            new GroupMember(WildSpawnType.exUsec, BotDifficulty.normal, new[] { 1 }),
            new GroupMember(WildSpawnType.exUsec, BotDifficulty.normal, new[] { 0 })
        }, null);

        Assert.Single(plan.Roll(new Random(1), 10, null));
    }
}
