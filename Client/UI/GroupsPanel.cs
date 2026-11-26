using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EFT;
using Perun.Client.Units;
using UnityEngine;
using static Perun.Client.UI.Constants.GroupsPanel;

namespace Perun.Client.UI;

internal sealed class GroupsPanel
{
    private const float RowHeight = 22f;
    private const float StatusRefresh = 0.25f;
    private const float DoubleClick = 0.35f;

    private static readonly string[] DifficultyNames = { LabelAsSet, LabelEasy, LabelNormal, LabelHard, LabelImpossibleShort };
    private static readonly BotDifficulty?[] Difficulties = { null, BotDifficulty.easy, BotDifficulty.normal, BotDifficulty.hard, BotDifficulty.impossible };

    private sealed class Row
    {
        public GroupPlan Plan;
        public string Section;
        public string Text;
        public string Count;
    }

    private readonly VirtualList _list = new(RowHeight);
    private readonly List<Row> _rows = new();
    private IReadOnlyList<GroupPlan> _plans;
    private int _customVersion = -1;
    private readonly HashSet<string> _collapsed = new();
    private string _toggleSection;
    private float _lastClick;
    private string _search = "";
    private string _activeSearch;
    private Vector2 _scroll;
    private GroupPlan _selected;
    private bool _armed;
    private int _difficulty;
    private float _nextStatus;
    private string _title = LabelGroupHeader;
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

    public bool Linking { get; private set; }

    public void StopLinking()
    {
        Linking = false;
        _nextStatus = 0f;
    }

    public void Disarm()
    {
        _armed = false;
        _nextStatus = 0f;
    }

    public void DrawContent()
    {
        if (Event.current.type == EventType.Layout)
        {
            bool toggled = _toggleSection != null;
            if (toggled && !_collapsed.Remove(_toggleSection))
            {
                _collapsed.Add(_toggleSection);
            }
            _toggleSection = null;

            IReadOnlyList<GroupPlan> plans = BossPresets.All;
            if (toggled || !ReferenceEquals(plans, _plans) || _customVersion != CustomGroups.Version || _activeSearch != _search)
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

        GUI.SetNextControlName(ControlSearch);
        _search = GUILayout.TextField(_search ?? "", 40, Theme.Search);
        if (string.IsNullOrEmpty(_search) && GUI.GetNameOfFocusedControl() != ControlSearch && Event.current.type == EventType.Repaint)
        {
            Rect field = GUILayoutUtility.GetLastRect();
            GUI.Label(new Rect(field.x + 6f, field.y + 3f, field.width, field.height), LabelSearchGroups, Theme.Small);
        }

        _list.Plan(_rows.Count, _scroll);
        _scroll = GUILayout.BeginScrollView(_scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
        _list.SpaceBefore();
        for (int i = _list.First; i < _list.Last; i++)
        {
            Row row = _rows[i];
            if (row.Plan == null)
            {
                if (GUILayout.Button(row.Text, Theme.Group, GUILayout.Height(RowHeight)))
                {
                    _toggleSection = row.Section;
                }
                continue;
            }

            bool isSelected = ReferenceEquals(_selected, row.Plan);
            if (GUILayout.Button(row.Text, isSelected ? Theme.SectionRowSelected : Theme.SectionRow, GUILayout.Height(RowHeight)))
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
            GUILayout.Label(string.IsNullOrEmpty(_activeSearch) ? LabelNoGroups : LabelNothingMatches, Theme.Small);
        }
        GUILayout.EndScrollView();
        _list.MeasureView();

        GUILayout.Space(6f);
        GUILayout.Label(_title, Theme.Header);
        GUILayout.Label(_summary, Theme.Label);

        GUILayout.Label(LabelDifficulty, Theme.Header);
        int difficulty = GUILayout.Toolbar(_difficulty, DifficultyNames, Theme.Tab);
        if (difficulty != _difficulty && Event.current.type != EventType.Layout)
        {
            _difficulty = difficulty;
            _nextStatus = 0f;
        }

        GUILayout.Space(4f);
        GUI.enabled = _selected != null && !Editor.IsOpen;
        bool armed = Armed;
        if (GUILayout.Toggle(armed, ChipPlaceGroup, Theme.Tab) != armed)
        {
            _armed = !armed;
            Linking &= !_armed;
            _nextStatus = 0f;
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(_selected is { Source: GroupSource.Custom } ? ChipEdit : ChipEditCopy, Theme.Button))
        {
            OpenEditor(_selected);
        }
        GUI.enabled = !Editor.IsOpen;
        if (GUILayout.Button(ChipNewGroup, Theme.Button))
        {
            _armed = false;
            Editor.Open(new GroupDraft(), null);
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUI.enabled = !Commands.IsRemote;
        bool linking = Linking;
        if (GUILayout.Toggle(linking, ChipLinkLeaders, Theme.Tab) != linking)
        {
            Linking = !linking;
            _armed &= !Linking;
            _nextStatus = 0f;
        }
        GUI.enabled = true;

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
        string name = plan.Name + LabelCopySuffix;
        Editor.Open(GroupDraft.From(plan, name.Length > CustomGroups.MaxNameLength ? name.Substring(0, CustomGroups.MaxNameLength) : name), null);
    }

    private void BuildRows()
    {
        _rows.Clear();
        string needle = (_activeSearch ?? "").Trim();
        if (_plans != null)
        {
            AddSection(LabelSptPresets, _plans.Where(p => p.Faction == null), needle);
            foreach (var faction in _plans.Where(p => p.Faction != null).GroupBy(p => p.Faction).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                AddSection(faction.Key.ToUpperInvariant(), faction, needle);
            }
        }
        AddSection(LabelCustom, CustomGroups.All, needle);
    }

    // A search shows every match, so collapsed sections open while it lasts.
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
        if (found == 0)
        {
            return;
        }
        bool collapsed = needle.Length == 0 && _collapsed.Contains(title);
        if (collapsed)
        {
            _rows.RemoveRange(header, found);
        }
        _rows.Insert(header, new Row { Section = title, Text = $"{(collapsed ? "▸" : "▾")} {title}  <color=#7f8a95>{found}</color>" });
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
        string tail = others == 0 ? LabelAlone : others == 1 ? "+ " + SpawnNames.Of(plan.Members[plan.LeaderIndex == 0 ? 1 : 0].Role) : string.Format(LabelMoreKinds, others);
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

        string pending = Commands.Pending > 0 ? string.Format(LabelSpawning, Commands.Pending) : "";
        string units = string.Format(LabelPerunUnits, Commands.AliveCount, Commands.MaxUnits, pending);
        string result = string.IsNullOrEmpty(Commands.LastResult) ? "" : "\n" + Commands.LastResult;

        if (Editor.IsOpen)
        {
            _prompt = LabelPlaceFromEditor;
            _status = units + result;
            return;
        }

        if (Linking)
        {
            _prompt = LabelLinkHint;
            _status = units + result;
            return;
        }

        if (_selected == null)
        {
            _prompt = LabelPickAGroup;
            _status = units + result;
            return;
        }

        Commands.PrewarmGroup(_selected, Difficulty);
        int ready = Commands.GroupReadyCount(_selected, Difficulty);
        int wanted = _selected.Max;
        Ready = ready >= wanted;
        _prompt = Armed
            ? string.Format(LabelClickTheMap, _selected.Name, _selected.CountText)
            : LabelPlaceGroupHint;
        string readiness = Commands.IsRemote
            ? LabelHostLoads
            : Ready
                ? string.Format(LabelReadyToDrop, wanted)
                : string.Format(LabelPreparingInAdvance, ready, wanted);
        int room = Commands.MaxUnits - Commands.AliveCount - Commands.Pending;
        string limit = room < _selected.Min ? string.Format(LabelOverUnitLimit, Math.Max(room, 0), Commands.MaxUnits) : "";
        _status = $"{readiness}\n{units}{limit}{result}";
    }

    private void BuildSummary()
    {
        if (_selected == null)
        {
            _title = LabelGroupHeader;
            _summary = LabelNothingSelected;
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
            b.Append(MarkupColorOpen).Append(Hex(member.Role)).Append('>').Append(SpawnNames.Of(member.Role)).Append("</color>");
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
            b.Append(plan.LeaderChoice.HasValue ? LabelCustomLeader : LabelCustomLine);
        }
        else if (plan.Maps.Count > 0)
        {
            b.Append("\n<color=#7f8a95>").Append(plan.Faction ?? LabelSptSource).Append(" · ").Append(string.Join(", ", plan.Maps)).Append("</color>");
        }
        _summary = b.ToString();
    }
}
