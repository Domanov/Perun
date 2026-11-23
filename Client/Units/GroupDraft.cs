using System.Collections.Generic;
using EFT;

namespace Perun.Client.Units;

internal sealed class GroupDraft
{
    public const int MaxCount = 10;

    internal sealed class Member
    {
        public WildSpawnType Role;
        public BotDifficulty Difficulty;
        public int Count;
    }

    private readonly List<Member> _members = new();
    private readonly List<WildSpawnType> _roles = new();
    private string _name = "";

    public string Name
    {
        get => _name;
        set
        {
            value ??= "";
            if (value != _name)
            {
                _name = value;
                Version++;
            }
        }
    }

    public IReadOnlyList<Member> Members => _members;
    public int? Leader { get; private set; }
    public int LeaderIndex { get; private set; }
    public int Total { get; private set; }
    public int Version { get; private set; }

    public static GroupDraft From(GroupPlan plan, string name)
    {
        GroupDraft draft = new() { _name = name };
        foreach (GroupMember member in plan.Members)
        {
            draft._members.Add(new Member { Role = member.Role, Difficulty = member.Difficulty, Count = Clamp(member.Max) });
        }
        draft.Leader = plan.LeaderChoice;
        draft.Changed();
        return draft;
    }

    public void Add(WildSpawnType role, BotDifficulty difficulty)
    {
        int index = _members.FindIndex(m => m.Role == role);
        if (index >= 0)
        {
            SetCount(index, _members[index].Count + 1);
            return;
        }
        _members.Add(new Member { Role = role, Difficulty = difficulty, Count = 1 });
        Changed();
    }

    public void Remove(int index)
    {
        _members.RemoveAt(index);
        if (Leader == index)
        {
            Leader = null;
        }
        else if (Leader > index)
        {
            Leader--;
        }
        Changed();
    }

    public void SetCount(int index, int count)
    {
        count = Clamp(count);
        if (_members[index].Count != count)
        {
            _members[index].Count = count;
            Changed();
        }
    }

    public void SetDifficulty(int index, BotDifficulty difficulty)
    {
        if (_members[index].Difficulty != difficulty)
        {
            _members[index].Difficulty = difficulty;
            Changed();
        }
    }

    public void SetLeader(int? index)
    {
        if (index is { } i && (i < 0 || i >= _members.Count))
        {
            index = null;
        }
        if (Leader != index)
        {
            Leader = index;
            Changed();
        }
    }

    public GroupPlan ToPlan()
    {
        List<GroupMember> members = _members.ConvertAll(m => new GroupMember(m.Role, m.Difficulty, new[] { m.Count }));
        return new GroupPlan(GroupSource.Custom, _name.Trim(), members, null, Leader);
    }

    private static int Clamp(int count) => count < 1 ? 1 : count > MaxCount ? MaxCount : count;

    private void Changed()
    {
        _roles.Clear();
        int total = 0;
        foreach (Member member in _members)
        {
            _roles.Add(member.Role);
            total += member.Count;
        }
        Total = total;
        LeaderIndex = Leader ?? GroupPlan.AutoLeader(_roles);
        Version++;
    }
}
