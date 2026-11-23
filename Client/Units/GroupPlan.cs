using System;
using System.Collections.Generic;
using System.Linq;
using EFT;

namespace Perun.Client.Units;

internal enum GroupSource
{
    Spt,
    Custom
}

internal sealed class GroupMember
{
    private readonly int[] _counts;

    public GroupMember(WildSpawnType role, BotDifficulty difficulty, int[] counts)
    {
        Role = role;
        Difficulty = difficulty;
        _counts = counts is { Length: > 0 } ? counts : new[] { 1 };
        Min = _counts.Min();
        Max = _counts.Max();
        CountText = GroupPlan.Range(Min, Max);
    }

    public WildSpawnType Role { get; }
    public BotDifficulty Difficulty { get; }
    public int Min { get; }
    public int Max { get; }
    public string CountText { get; }

    public int Roll(Random random) => _counts[random.Next(_counts.Length)];
}

internal sealed class GroupPlan
{
    public GroupPlan(GroupSource source, string name, IReadOnlyList<GroupMember> members, IReadOnlyList<string> maps, int? leaderChoice, string faction = null)
    {
        Source = source;
        Faction = faction;
        Name = name;
        Members = members;
        Maps = maps ?? Array.Empty<string>();
        LeaderChoice = leaderChoice is { } index && index >= 0 && index < members.Count ? index : null;
        LeaderIndex = LeaderChoice ?? AutoLeader(members.Select(m => m.Role).ToList());
        Min = members.Sum(m => m.Min);
        Max = members.Sum(m => m.Max);
        CountText = Range(Min, Max);
    }

    public GroupSource Source { get; }
    public string Faction { get; }
    public string Name { get; }
    public IReadOnlyList<GroupMember> Members { get; }
    public IReadOnlyList<string> Maps { get; }
    public int? LeaderChoice { get; }
    public int LeaderIndex { get; }
    public int Min { get; }
    public int Max { get; }
    public string CountText { get; }

    public GroupMember Leader => Members[LeaderIndex];

    internal static string Range(int min, int max) => min == max ? min.ToString() : $"{min}–{max}";

    // Same pick as the game's own boss spawns and SAIN: a boss role leads, otherwise whoever is listed first.
    internal static int AutoLeader(IReadOnlyList<WildSpawnType> roles)
    {
        for (int i = 0; i < roles.Count; i++)
        {
            if (IsBossRole(roles[i]))
            {
                return i;
            }
        }
        return 0;
    }

    internal static bool IsBossRole(WildSpawnType role) => SpawnNames.Id(role).StartsWith("boss", StringComparison.Ordinal);

    // The n-th member in spawn order: the leader first, the others as listed.
    public GroupMember InSpawnOrder(int n) => Members[n == 0 ? LeaderIndex : n <= LeaderIndex ? n - 1 : n];

    // The leader comes first in the result and spawns at least once, even if its counts allow zero.
    public List<(WildSpawnType role, BotDifficulty difficulty, int count)> Roll(Random random, int budget, BotDifficulty? overrideDifficulty)
    {
        List<(WildSpawnType, BotDifficulty, int)> result = new();
        int left = budget;
        for (int n = 0; n < Members.Count && left > 0; n++)
        {
            GroupMember member = InSpawnOrder(n);
            int count = Math.Min(Math.Max(n == 0 ? 1 : 0, member.Roll(random)), left);
            if (count > 0)
            {
                result.Add((member.Role, overrideDifficulty ?? member.Difficulty, count));
                left -= count;
            }
        }
        return result;
    }
}
