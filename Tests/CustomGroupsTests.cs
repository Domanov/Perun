using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using Perun.Client.Units;

namespace Perun.Tests;

public class CustomGroupsTests
{
    private static GroupDraft Draft(string name, params (WildSpawnType role, int count)[] members)
    {
        GroupDraft draft = GroupDraft.From(new GroupPlan(GroupSource.Custom, name, new List<GroupMember>(), null, null), name);
        foreach (var (role, count) in members)
        {
            draft.Add(role, BotDifficulty.normal);
            draft.SetCount(draft.Members.Count - 1, count);
        }
        return draft;
    }

    [Fact]
    public void AddingAKnownRoleRaisesItsCountInsteadOfAddingARow()
    {
        GroupDraft draft = Draft("Patrol", (WildSpawnType.assault, 2));
        draft.Add(WildSpawnType.assault, BotDifficulty.hard);

        Assert.Single(draft.Members);
        Assert.Equal(3, draft.Members[0].Count);
        Assert.Equal(BotDifficulty.normal, draft.Members[0].Difficulty);
        Assert.Equal(3, draft.Total);
    }

    [Fact]
    public void CountsStayBetweenOneAndTheMaximum()
    {
        GroupDraft draft = Draft("Patrol", (WildSpawnType.assault, 1));
        draft.SetCount(0, 0);
        Assert.Equal(1, draft.Members[0].Count);
        draft.SetCount(0, 99);
        Assert.Equal(GroupDraft.MaxCount, draft.Members[0].Count);
    }

    [Fact]
    public void AutoLeaderIsTheFirstBossOtherwiseTheFirstMember()
    {
        GroupDraft draft = Draft("Mixed", (WildSpawnType.followerBully, 2), (WildSpawnType.bossBully, 1));
        Assert.Null(draft.Leader);
        Assert.Equal(1, draft.LeaderIndex);

        draft.Remove(1);
        Assert.Equal(0, draft.LeaderIndex);
    }

    [Fact]
    public void RemovingRowsKeepsTheManualLeaderOnTheSameMember()
    {
        GroupDraft draft = Draft("Squad", (WildSpawnType.assault, 1), (WildSpawnType.pmcBot, 1), (WildSpawnType.exUsec, 1));
        draft.SetLeader(2);

        draft.Remove(0);
        Assert.Equal(1, draft.Leader);
        Assert.Equal(WildSpawnType.exUsec, draft.Members[draft.LeaderIndex].Role);

        draft.Remove(1);
        Assert.Null(draft.Leader);
    }

    [Fact]
    public void RollPutsAManualLeaderFirst()
    {
        GroupDraft draft = Draft("Squad", (WildSpawnType.assault, 3), (WildSpawnType.pmcBot, 1));
        draft.SetLeader(1);
        GroupPlan plan = draft.ToPlan();

        var parts = plan.Roll(new Random(1), 10, null);

        Assert.Equal(WildSpawnType.pmcBot, parts[0].role);
        Assert.Equal(WildSpawnType.assault, parts[1].role);
        Assert.Equal(3, parts[1].count);
    }

    [Fact]
    public void SavedGroupsRoundTrip()
    {
        GroupDraft draft = Draft(" Night watch ", (WildSpawnType.assault, 4), (WildSpawnType.pmcBot, 2));
        draft.SetDifficulty(1, BotDifficulty.impossible);
        draft.SetLeader(1);

        List<string> problems = new();
        List<GroupPlan> loaded = CustomGroups.Parse(CustomGroups.Serialize(new[] { draft.ToPlan() }), problems);

        Assert.Empty(problems);
        GroupPlan plan = Assert.Single(loaded);
        Assert.Equal("Night watch", plan.Name);
        Assert.Equal(GroupSource.Custom, plan.Source);
        Assert.Equal(1, plan.LeaderChoice);
        Assert.Equal(new[] { WildSpawnType.assault, WildSpawnType.pmcBot }, plan.Members.Select(m => m.Role));
        Assert.Equal(new[] { 4, 2 }, plan.Members.Select(m => m.Max));
        Assert.Equal(BotDifficulty.impossible, plan.Members[1].Difficulty);
    }

    [Fact]
    public void UnknownRolesAreDroppedAndTheLeaderFollowsItsMember()
    {
        const string json = """
            [
              { "Name": "Old", "Leader": 2, "Members": [
                { "Role": "assault", "Difficulty": "easy", "Count": 2 },
                { "Role": "factionThatIsGone", "Difficulty": "hard", "Count": 1 },
                { "Role": "pmcBot", "Difficulty": "nonsense", "Count": 40 } ] },
              { "Name": "old", "Members": [ { "Role": "assault", "Count": 1 } ] },
              { "Name": "Empty", "Members": [ { "Role": "gone", "Count": 1 } ] }
            ]
            """;

        List<string> problems = new();
        GroupPlan plan = Assert.Single(CustomGroups.Parse(json, problems));

        Assert.Equal(4, problems.Count);
        Assert.Equal(1, plan.LeaderChoice);
        Assert.Equal(WildSpawnType.pmcBot, plan.Leader.Role);
        Assert.Equal(BotDifficulty.normal, plan.Leader.Difficulty);
        Assert.Equal(GroupDraft.MaxCount, plan.Leader.Max);
    }

    [Fact]
    public void NamesMustBeGivenShortAndUnique()
    {
        GroupPlan[] saved = { Draft("Patrol", (WildSpawnType.assault, 1)).ToPlan() };

        Assert.NotNull(CustomGroups.CheckName(saved, "  ", null));
        Assert.NotNull(CustomGroups.CheckName(saved, new string('x', CustomGroups.MaxNameLength + 1), null));
        Assert.NotNull(CustomGroups.CheckName(saved, "patrol ", null));
        Assert.Null(CustomGroups.CheckName(saved, "PATROL", "Patrol"));
        Assert.Null(CustomGroups.CheckName(saved, "Ambush", "Patrol"));
    }
}
