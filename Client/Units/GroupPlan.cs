using System;
using System.Collections.Generic;
using System.Linq;
using EFT;

namespace Perun.Client.Units;

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
    public GroupPlan(string name, IReadOnlyList<GroupMember> members, IReadOnlyList<string> maps)
    {
        Name = name;
        Members = members;
        Maps = maps ?? Array.Empty<string>();
        Min = members.Sum(m => m.Min);
        Max = members.Sum(m => m.Max);
        CountText = Range(Min, Max);
    }

    public string Name { get; }
    public IReadOnlyList<GroupMember> Members { get; }
    public IReadOnlyList<string> Maps { get; }
    public int Min { get; }
    public int Max { get; }
    public string CountText { get; }

    public GroupMember Leader => Members[0];

    internal static string Range(int min, int max) => min == max ? min.ToString() : $"{min}–{max}";

    // The leader always comes first and spawns at least once, even if its counts allow zero.
    public List<(WildSpawnType role, BotDifficulty difficulty, int count)> Roll(Random random, int budget, BotDifficulty? overrideDifficulty)
    {
        List<(WildSpawnType, BotDifficulty, int)> result = new();
        int left = budget;
        for (int i = 0; i < Members.Count && left > 0; i++)
        {
            GroupMember member = Members[i];
            int count = Math.Min(Math.Max(i == 0 ? 1 : 0, member.Roll(random)), left);
            if (count > 0)
            {
                result.Add((member.Role, overrideDifficulty ?? member.Difficulty, count));
                left -= count;
            }
        }
        return result;
    }
}
