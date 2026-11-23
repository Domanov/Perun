using System.Linq;
using EFT;
using Perun.Client;
using UnityEngine;

namespace Perun.Tests;

public class RulesTests
{
    [Theory]
    [InlineData((byte)PerunOrderKind.Move)]
    [InlineData((byte)PerunOrderKind.Halt)]
    [InlineData((byte)PerunOrderKind.Remove)]
    [InlineData((byte)PerunOrderKind.Kill)]
    [InlineData((byte)PerunOrderKind.Heal)]
    [InlineData((byte)PerunOrderKind.Teleport)]
    public void KnownOrderKindsAreAccepted(byte kind) => Assert.True(PerunRules.IsKnownOrder(kind));

    [Theory]
    [InlineData((byte)6)]
    [InlineData((byte)100)]
    [InlineData((byte)255)]
    public void UnknownOrderKindsAreRejected(byte kind) => Assert.False(PerunRules.IsKnownOrder(kind));

    [Theory]
    [InlineData(float.NaN, 0f, 0f)]
    [InlineData(0f, float.PositiveInfinity, 0f)]
    [InlineData(0f, 0f, float.NegativeInfinity)]
    public void NonFinitePointsAreRejected(float x, float y, float z) => Assert.False(PerunRules.IsFinite(new Vector3(x, y, z)));

    [Fact]
    public void FinitePointsAreAccepted() => Assert.True(PerunRules.IsFinite(new Vector3(-120.5f, 3f, 999f)));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    [InlineData(int.MinValue)]
    public void GroupSizesOutsideOneToTenAreRejected(int count) => Assert.False(PerunRules.IsGroupSize(count));

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void GroupSizeBoundsAreAccepted(int count) => Assert.True(PerunRules.IsGroupSize(count));

    // Ints, not the enum: xUnit reads attribute arguments while finding tests, before the game assembly can be resolved.
    [Theory]
    [InlineData((int)WildSpawnType.shooterBTR)]
    [InlineData((int)WildSpawnType.test)]
    [InlineData((int)WildSpawnType.bossTest)]
    [InlineData((int)WildSpawnType.followerTest)]
    [InlineData((int)WildSpawnType.assaultGroup)]
    [InlineData((int)WildSpawnType.gifter)]
    public void HiddenRolesCannotBeSpawned(int role) => Assert.False(PerunRules.IsSpawnableRole(role));

    [Fact]
    public void UndefinedRolesAndDifficultiesAreRejected()
    {
        Assert.False(PerunRules.IsSpawnableRole(-1));
        Assert.False(PerunRules.IsSpawnableRole(int.MaxValue));
        Assert.False(PerunRules.IsKnownDifficulty(-1));
        Assert.False(PerunRules.IsKnownDifficulty(99));
    }

    [Fact]
    public void OrdinaryRolesAndDifficultiesAreAccepted()
    {
        Assert.True(PerunRules.IsSpawnableRole((int)WildSpawnType.assault));
        Assert.True(PerunRules.IsSpawnableRole((int)WildSpawnType.pmcBEAR));
        Assert.True(PerunRules.IsKnownDifficulty((int)BotDifficulty.impossible));
    }

    [Theory]
    [InlineData(10000f, -10000f, 10000f, true)]
    [InlineData(10001f, 0f, 0f, false)]
    [InlineData(0f, -10001f, 0f, false)]
    [InlineData(0f, 0f, float.NaN, false)]
    [InlineData(float.PositiveInfinity, 0f, 0f, false)]
    public void PointsMustBeInsideTheWorld(float x, float y, float z, bool expected) => Assert.Equal(expected, PerunRules.IsInWorld(new Vector3(x, y, z)));

    [Theory]
    [InlineData(0f, true)]
    [InlineData(-720f, true)]
    [InlineData(720f, true)]
    [InlineData(721f, false)]
    [InlineData(float.NaN, false)]
    [InlineData(float.NegativeInfinity, false)]
    public void YawMustBeFiniteAndBounded(float yaw, bool expected) => Assert.Equal(expected, PerunRules.IsSaneYaw(yaw));

    [Fact]
    public void OrdersWithoutADestinationIgnoreThePoint()
    {
        Vector3 nowhere = new(float.NaN, 0f, 0f);
        Assert.True(PerunRules.IsSaneOrder((byte)PerunOrderKind.Halt, nowhere));
        Assert.True(PerunRules.IsSaneOrder((byte)PerunOrderKind.Kill, nowhere));
        Assert.False(PerunRules.IsSaneOrder((byte)PerunOrderKind.Move, nowhere));
        Assert.False(PerunRules.IsSaneOrder((byte)PerunOrderKind.Teleport, new Vector3(0f, 20000f, 0f)));
        Assert.False(PerunRules.IsSaneOrder(99, Vector3.zero));
    }

    internal static PerunGroupSpawn Group() => new()
    {
        Name = "Reshala",
        Members = new[]
        {
            new PerunGroupMember { Role = (int)WildSpawnType.bossBully, Difficulty = (int)BotDifficulty.normal, Counts = new[] { 1 } },
            new PerunGroupMember { Role = (int)WildSpawnType.followerBully, Difficulty = (int)BotDifficulty.hard, Counts = new[] { 2, 3, 4 } }
        },
        Leader = -1,
        Difficulty = -1,
        Position = new Vector3(10f, 2f, -30f),
        Yaw = 90f,
        Spacing = 1.3f
    };

    [Fact]
    public void AGroupFromTheEditorIsSane()
    {
        Assert.True(PerunRules.IsSaneGroup(Group()));
        PerunGroupSpawn chosen = Group();
        chosen.Leader = 1;
        chosen.Difficulty = (int)BotDifficulty.impossible;
        Assert.True(PerunRules.IsSaneGroup(chosen));
    }

    [Fact]
    public void BrokenGroupsAreRejected()
    {
        void Rejected(System.Action<PerunGroupSpawn> change)
        {
            PerunGroupSpawn group = Group();
            change(group);
            Assert.False(PerunRules.IsSaneGroup(group));
        }

        Rejected(g => g.Members = new PerunGroupMember[0]);
        Rejected(g => g.Members = new PerunGroupMember[PerunRules.MaxGroupMembers + 1].Select(_ => g.Members[0]).ToArray());
        Rejected(g => g.Leader = 2);
        Rejected(g => g.Leader = -2);
        Rejected(g => g.Difficulty = 99);
        Rejected(g => g.Members[0].Role = (int)WildSpawnType.shooterBTR);
        Rejected(g => g.Members[0].Difficulty = -1);
        Rejected(g => g.Members[1].Counts = new int[0]);
        Rejected(g => g.Members[1].Counts = new int[PerunRules.MaxCountOptions + 1]);
        Rejected(g => g.Members[1].Counts = new[] { PerunRules.MaxGroup + 1 });
        Rejected(g => g.Position = new Vector3(0f, float.NaN, 0f));
        Rejected(g => g.Yaw = float.PositiveInfinity);
        Rejected(g => g.Spacing = 0.1f);
        Rejected(g => g.Spacing = 50f);
    }
}
