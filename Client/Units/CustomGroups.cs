using System;
using System.Collections.Generic;
using System.IO;
using EFT;
using Newtonsoft.Json;

namespace Perun.Client.Units;

internal sealed class SavedGroup
{
    public string Name { get; set; }
    public int? Leader { get; set; }
    public List<SavedMember> Members { get; set; } = new();
}

internal sealed class SavedMember
{
    public string Role { get; set; }
    public string Difficulty { get; set; }
    public int Count { get; set; }
}

internal static class CustomGroups
{
    private const string FileGroups = "com.dmnv.perun.groups.json";
    private const string FileBadSuffix = ".bad";

    private const string LabelNeedMember = "Add at least one member";
    private const string LabelTooManyGroups = "Too many saved groups ({0}); delete one first";
    private const string LabelNeedName = "Give the group a name";
    private const string LabelNameTooLong = "Name is longer than {0} characters";
    private const string LabelNameTaken = "A group named \"{0}\" already exists";
    private const string LabelGroupSkipped = "Group \"{0}\": unnamed or duplicate, skipped";
    private const string LabelRoleSkipped = "Group \"{0}\": unknown role \"{1}\" skipped";
    private const string LabelNoMembersSkipped = "Group \"{0}\": no members left, skipped";
    private const string LabelCouldNotWrite = "Couldn't write {0}: {1}";

    public const int Limit = 64;
    public const int MaxNameLength = 32;

    private static readonly string FilePath = Path.Combine(BepInEx.Paths.ConfigPath, FileGroups);
    private static List<GroupPlan> _all;

    public static IReadOnlyList<GroupPlan> All => _all ??= Read();

    public static int Version { get; private set; }

    public static GroupPlan Find(string name) => Find(All, name);

    // replacing: the saved name the editor opened, so renaming or re-saving a group doesn't clash with itself.
    public static string CheckName(string name, string replacing) => CheckName(All, name, replacing);

    public static bool TrySave(GroupDraft draft, string replacing, out GroupPlan plan, out string error)
    {
        plan = null;
        error = CheckName(draft.Name, replacing);
        if (error == null && draft.Members.Count == 0)
        {
            error = LabelNeedMember;
        }
        if (error == null && Find(replacing) == null && All.Count >= Limit)
        {
            error = string.Format(LabelTooManyGroups, Limit);
        }
        if (error != null)
        {
            return false;
        }

        GroupPlan saved = draft.ToPlan();
        List<GroupPlan> all = new(All);
        all.RemoveAll(p => Same(p.Name, replacing) || Same(p.Name, saved.Name));
        all.Add(saved);
        all.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
        error = Replace(all);
        if (error != null)
        {
            return false;
        }
        plan = saved;
        return true;
    }

    public static string Delete(string name)
    {
        List<GroupPlan> all = new(All);
        return all.RemoveAll(p => Same(p.Name, name)) > 0 ? Replace(all) : null;
    }

    internal static GroupPlan Find(IReadOnlyList<GroupPlan> plans, string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }
        foreach (GroupPlan plan in plans)
        {
            if (Same(plan.Name, name))
            {
                return plan;
            }
        }
        return null;
    }

    internal static string CheckName(IReadOnlyList<GroupPlan> plans, string name, string replacing)
    {
        string trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            return LabelNeedName;
        }
        if (trimmed.Length > MaxNameLength)
        {
            return string.Format(LabelNameTooLong, MaxNameLength);
        }
        if (!Same(trimmed, replacing) && Find(plans, trimmed) != null)
        {
            return string.Format(LabelNameTaken, trimmed);
        }
        return null;
    }

    internal static string Serialize(IEnumerable<GroupPlan> plans)
    {
        List<SavedGroup> saved = new();
        foreach (GroupPlan plan in plans)
        {
            SavedGroup group = new() { Name = plan.Name, Leader = plan.LeaderChoice };
            foreach (GroupMember member in plan.Members)
            {
                group.Members.Add(new SavedMember { Role = member.Role.ToString(), Difficulty = member.Difficulty.ToString(), Count = member.Max });
            }
            saved.Add(group);
        }
        return JsonConvert.SerializeObject(saved, Formatting.Indented);
    }

    // Unknown roles (a removed mod's faction) are dropped with a note instead of failing the whole file.
    internal static List<GroupPlan> Parse(string json, List<string> problems)
    {
        List<GroupPlan> plans = new();
        List<SavedGroup> saved = JsonConvert.DeserializeObject<List<SavedGroup>>(json);
        if (saved == null)
        {
            return plans;
        }
        foreach (SavedGroup group in saved)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.Name) || Find(plans, group.Name.Trim()) != null)
            {
                problems.Add(string.Format(LabelGroupSkipped, group?.Name));
                continue;
            }
            List<GroupMember> members = new();
            int? leader = null;
            for (int i = 0; i < (group.Members?.Count ?? 0); i++)
            {
                SavedMember member = group.Members[i];
                if (member == null || string.IsNullOrEmpty(member.Role) || !Enum.TryParse(member.Role, true, out WildSpawnType role))
                {
                    problems.Add(string.Format(LabelRoleSkipped, group.Name, member?.Role));
                    continue;
                }
                if (group.Leader == i)
                {
                    leader = members.Count;
                }
                BotDifficulty difficulty = Enum.TryParse(member.Difficulty, true, out BotDifficulty parsed) ? parsed : BotDifficulty.normal;
                int count = Math.Min(Math.Max(member.Count, 1), GroupDraft.MaxCount);
                members.Add(new GroupMember(role, difficulty, new[] { count }));
            }
            if (members.Count == 0)
            {
                problems.Add(string.Format(LabelNoMembersSkipped, group.Name));
                continue;
            }
            plans.Add(new GroupPlan(GroupSource.Custom, group.Name.Trim(), members, null, leader));
        }
        return plans;
    }

    private static bool Same(string a, string b) => a != null && b != null && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    // The list only changes once the file is written, so what the panel shows is what a restart loads.
    private static string Replace(List<GroupPlan> all)
    {
        try
        {
            File.WriteAllText(FilePath, Serialize(all));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Saved groups could not be written: {ex.Message}");
            return string.Format(LabelCouldNotWrite, Path.GetFileName(FilePath), ex.Message);
        }
        _all = all;
        Version++;
        return null;
    }

    // A file that can't be read is kept aside, so the next save doesn't overwrite the groups in it.
    private static List<GroupPlan> Read()
    {
        if (!File.Exists(FilePath))
        {
            return new List<GroupPlan>();
        }
        try
        {
            List<string> problems = new();
            List<GroupPlan> plans = Parse(File.ReadAllText(FilePath), problems);
            foreach (string problem in problems)
            {
                Plugin.Log.LogWarning($"Saved groups: {problem}");
            }
            return plans;
        }
        catch (Exception ex)
        {
            string aside = FilePath + FileBadSuffix;
            Plugin.Log.LogWarning($"Saved groups could not be read ({ex.Message}); the file was copied to {aside}");
            try
            {
                File.Copy(FilePath, aside, true);
            }
            catch (Exception copy)
            {
                Plugin.Log.LogWarning($"Saved groups: copying the unreadable file failed: {copy.Message}");
            }
            return new List<GroupPlan>();
        }
    }
}
