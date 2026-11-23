using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EFT;
using Perun.Client.Units;
using UnityEngine;

namespace Perun.Client.UI;

internal sealed class GroupsPanel
{
    public const string SearchControl = "PerunGroupSearch";

    private const float RowHeight = 22f;
    private const float StatusRefresh = 0.25f;
    private const float DoubleClick = 0.35f;

    private static readonly string[] DifficultyNames = { "As set", "Easy", "Normal", "Hard", "Imposs." };
    private static readonly BotDifficulty?[] Difficulties = { null, BotDifficulty.easy, BotDifficulty.normal, BotDifficulty.hard, BotDifficulty.impossible };

    private sealed class Row
    {
        public GroupPlan Plan;
        public string Text;
        public string Count;
    }

    private readonly VirtualList _list = new(RowHeight);
    private readonly List<Row> _rows = new();
    private IReadOnlyList<GroupPlan> _plans;
    private int _customVersion = -1;
    private float _lastClick;
    private string _search = "";
    private string _activeSearch;
    private Vector2 _scroll;
    private GroupPlan _selected;
    private bool _armed;
    private int _difficulty;
    private float _nextStatus;
    private string _title = "GROUP";
    private string _summary = "";
    private string _prompt = "";
    private string _status = "";
    private GroupPlan _summaryFor;
    private int _summaryDifficulty = -1;

    public GroupsPanel()
    {
        Editor.Placed = plan =>
        {
            _selected = plan;
            _armed = true;
            _nextStatus = 0f;
        };
    }

    public GroupEditor Editor { get; } = new();
    public GroupPlan Selected => _selected;
    public bool Armed => _armed && _selected != null && !Editor.IsOpen;
    public bool Ready { get; private set; }
    public BotDifficulty? Difficulty => Difficulties[_difficulty];

    public void Disarm()
    {
        _armed = false;
        _nextStatus = 0f;
    }

    public void DrawContent()
    {
        if (Event.current.type == EventType.Layout)
        {
            IReadOnlyList<GroupPlan> plans = BossPresets.All;
            if (!ReferenceEquals(plans, _plans) || _customVersion != CustomGroups.Version || _activeSearch != _search)
            {
                if (_activeSearch != _search)
                {
                    _scroll = Vector2.zero;
                }
                _plans = plans;
                _customVersion = CustomGroups.Version;
                _activeSearch = _search;
                if (_selected is { Source: GroupSource.Custom })
                {
                    _selected = CustomGroups.Find(_selected.Name);
                    _armed &= _selected != null;
                }
                BuildRows();
            }
            RefreshStatus();
        }

        GUI.SetNextControlName(SearchControl);
        _search = GUILayout.TextField(_search ?? "", 40, Theme.Search);
        if (string.IsNullOrEmpty(_search) && GUI.GetNameOfFocusedControl() != SearchControl && Event.current.type == EventType.Repaint)
        {
            Rect field = GUILayoutUtility.GetLastRect();
            GUI.Label(new Rect(field.x + 6f, field.y + 3f, field.width, field.height), "Search groups…", Theme.Small);
        }

        _list.Plan(_rows.Count, _scroll);
        _scroll = GUILayout.BeginScrollView(_scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
        _list.SpaceBefore();
        for (int i = _list.First; i < _list.Last; i++)
        {
            Row row = _rows[i];
            if (row.Plan == null)
            {
                GUILayout.Label(row.Text, Theme.Group, GUILayout.Height(RowHeight));
                continue;
            }

            bool isSelected = ReferenceEquals(_selected, row.Plan);
            if (GUILayout.Button(row.Text, isSelected ? Theme.RowSelectedBadged : Theme.RowBadged, GUILayout.Height(RowHeight)))
            {
                float now = Time.unscaledTime;
                if (isSelected && now - _lastClick < DoubleClick && !Editor.IsOpen)
                {
                    OpenEditor(row.Plan);
                }
                else if (isSelected)
                {
                    _selected = null;
                    _armed = false;
                }
                else
                {
                    _selected = row.Plan;
                    _armed = true;
                }
                _lastClick = now;
                _nextStatus = 0f;
            }
            if (Event.current.type == EventType.Repaint)
            {
                GUI.Label(GUILayoutUtility.GetLastRect(), row.Count, Theme.Badge);
            }
        }
        _list.SpaceAfter();
        if (_rows.Count == 0)
        {
            GUILayout.Label(string.IsNullOrEmpty(_activeSearch) ? "No groups: the server sent no boss data and nothing is saved." : "Nothing matches.", Theme.Small);
        }
        GUILayout.EndScrollView();
        _list.MeasureView();

        GUILayout.Space(6f);
        GUILayout.Label(_title, Theme.Header);
        GUILayout.Label(_summary, Theme.Label);

        GUILayout.Label("DIFFICULTY", Theme.Header);
        int difficulty = GUILayout.Toolbar(_difficulty, DifficultyNames, Theme.Tab);
        if (difficulty != _difficulty && Event.current.type != EventType.Layout)
        {
            _difficulty = difficulty;
            _nextStatus = 0f;
        }

        GUILayout.Space(4f);
        GUI.enabled = _selected != null && !Editor.IsOpen;
        bool armed = Armed;
        if (GUILayout.Toggle(armed, "PLACE GROUP", Theme.Tab) != armed)
        {
            _armed = !armed;
            _nextStatus = 0f;
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(_selected is { Source: GroupSource.Custom } ? "Edit" : "Edit copy", Theme.Button))
        {
            OpenEditor(_selected);
        }
        GUI.enabled = !Editor.IsOpen;
        if (GUILayout.Button("New group", Theme.Button))
        {
            _armed = false;
            Editor.Open(new GroupDraft(), null);
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.Label(_prompt, Theme.Label);
        GUILayout.Label(_status, Theme.Label);
    }

    private void OpenEditor(GroupPlan plan)
    {
        _armed = false;
        if (plan.Source == GroupSource.Custom)
        {
            Editor.Open(GroupDraft.From(plan, plan.Name), plan.Name);
            return;
        }
        string name = plan.Name + " (copy)";
        Editor.Open(GroupDraft.From(plan, name.Length > CustomGroups.MaxNameLength ? name.Substring(0, CustomGroups.MaxNameLength) : name), null);
    }

    private void BuildRows()
    {
        _rows.Clear();
        string needle = (_activeSearch ?? "").Trim();
        if (_plans != null)
        {
            AddSection("SPT PRESETS", _plans.Where(p => p.Faction == null), needle);
            foreach (var faction in _plans.Where(p => p.Faction != null).GroupBy(p => p.Faction).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                AddSection(faction.Key.ToUpperInvariant(), faction, needle);
            }
        }
        AddSection("CUSTOM", CustomGroups.All, needle);
    }

    private void AddSection(string title, IEnumerable<GroupPlan> plans, string needle)
    {
        int header = _rows.Count;
        foreach (GroupPlan plan in plans)
        {
            if (needle.Length == 0 || Matches(plan, needle))
            {
                _rows.Add(new Row { Plan = plan, Text = RowText(plan), Count = $"<color=#7f8a95>{plan.CountText}</color>" });
            }
        }
        int found = _rows.Count - header;
        if (found > 0)
        {
            _rows.Insert(header, new Row { Text = $"{title} ({found})" });
        }
    }

    private static bool Matches(GroupPlan plan, string needle)
    {
        if (plan.Name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 || plan.Faction?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }
        foreach (GroupMember member in plan.Members)
        {
            if (SpawnNames.Of(member.Role).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                SpawnNames.Id(member.Role).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        foreach (string map in plan.Maps)
        {
            if (map.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private static string RowText(GroupPlan plan)
    {
        string color = Hex(plan.Leader.Role);
        int others = plan.Members.Count - 1;
        string tail = others == 0 ? "alone" : others == 1 ? "+ " + SpawnNames.Of(plan.Members[plan.LeaderIndex == 0 ? 1 : 0].Role) : $"+ {others} kinds";
        return $"<color={color}>{plan.Name}</color>  <color=#7f8a95>{tail}</color>";
    }

    private static string Hex(WildSpawnType role) => BotKinds.Hex(BotKinds.Classify(role, HostilityRules.SideOf(role)));

    private void RefreshStatus()
    {
        if (!ReferenceEquals(_summaryFor, _selected) || _summaryDifficulty != _difficulty)
        {
            _summaryFor = _selected;
            _summaryDifficulty = _difficulty;
            BuildSummary();
        }

        float now = Time.unscaledTime;
        if (now < _nextStatus)
        {
            return;
        }
        _nextStatus = now + StatusRefresh;
        Ready = false;

        string pending = Commands.Pending > 0 ? $" · spawning {Commands.Pending}" : "";
        string units = $"Perun units {Commands.AliveCount}/{Commands.MaxUnits}{pending}";
        string result = string.IsNullOrEmpty(Commands.LastResult) ? "" : "\n" + Commands.LastResult;

        if (Editor.IsOpen)
        {
            _prompt = "<color=#9fb4c8>Place the group from the editor, or close it to place from here.</color>";
            _status = units + result;
            return;
        }

        if (Commands.IsRemote)
        {
            _prompt = "<color=#ffd36f>Group spawning works on the host for now.</color>";
            _status = units + result;
            return;
        }

        if (_selected == null)
        {
            _prompt = "<color=#9fb4c8>Pick a group, then click the map to place it.</color>";
            _status = units + result;
            return;
        }

        Commands.PrewarmGroup(_selected, Difficulty);
        int ready = Commands.GroupReadyCount(_selected, Difficulty);
        int wanted = _selected.Max;
        Ready = ready >= wanted;
        _prompt = Armed
            ? $"<color=#ffc847>Click the map to place {_selected.Name} ({_selected.CountText})</color>  <color=#9fb4c8>· RMB stops</color>"
            : "<color=#9fb4c8>PLACE GROUP to place it on the map.</color>";
        string readiness = Ready
            ? $"<color=#8be38b>● Ready to drop: {wanted}</color> · spawns instantly"
            : $"<color=#ffc847>◐ Preparing in advance… {ready}/{wanted} ready</color>";
        int room = Commands.MaxUnits - Commands.AliveCount - Commands.Pending;
        string limit = room < _selected.Min ? $"\n<color=#ff8080>Only {Math.Max(room, 0)} more units fit (limit {Commands.MaxUnits})</color>" : "";
        _status = $"{readiness}\n{units}{limit}{result}";
    }

    private void BuildSummary()
    {
        if (_selected == null)
        {
            _title = "GROUP";
            _summary = "<color=#7f8a95>Nothing selected.</color>";
            return;
        }

        GroupPlan plan = _selected;
        _title = plan.Name.ToUpperInvariant();
        StringBuilder b = new();
        WildSpawnType leader = plan.Leader.Role;
        EPlayerSide leaderSide = HostilityRules.SideOf(leader);
        BotDifficulty? forced = Difficulty;
        for (int n = 0; n < plan.Members.Count; n++)
        {
            GroupMember member = plan.InSpawnOrder(n);
            if (n > 0)
            {
                b.Append('\n');
            }
            b.Append(n == 0 ? "<color=#ffc847>♛</color> " : "   ");
            b.Append("<color=").Append(Hex(member.Role)).Append('>').Append(SpawnNames.Of(member.Role)).Append("</color>");
            if (n > 0 || member.Max > 1)
            {
                b.Append(" ×").Append(member.CountText);
            }
            b.Append("  <color=#7f8a95>").Append(forced ?? member.Difficulty).Append("</color>");
            if (n > 0 && member.Role != leader)
            {
                string badge = HostilityRules.Badge(Hostility.Between(leader, leaderSide, member.Role, HostilityRules.SideOf(member.Role)));
                if (badge.Length > 0)
                {
                    b.Append("  ").Append(badge);
                }
            }
        }
        if (plan.Source == GroupSource.Custom)
        {
            b.Append(plan.LeaderChoice.HasValue ? "\n<color=#7f8a95>Custom · leader picked by hand</color>" : "\n<color=#7f8a95>Custom</color>");
        }
        else if (plan.Maps.Count > 0)
        {
            b.Append("\n<color=#7f8a95>").Append(plan.Faction ?? "SPT").Append(" · ").Append(string.Join(", ", plan.Maps)).Append("</color>");
        }
        _summary = b.ToString();
    }
}
