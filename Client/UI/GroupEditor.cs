using System;
using System.Collections.Generic;
using EFT;
using Perun.Client.Units;
using UnityEngine;

namespace Perun.Client.UI;

internal sealed class GroupEditor
{
    private const string ChipAuto = "AUTO";
    private const string ChipManual = "MANUAL";
    private const string ChipDelete = "Delete";
    private const string ChipCancel = "Cancel";
    private const string ChipSave = "Save";
    private const string ChipDiscard = "Discard";
    private const string ChipSaveAs = "Save as";
    private const string ChipClose = "Close";
    private const string ChipPlaceGroup = "PLACE GROUP";

    private const string LabelEasy = "Easy";
    private const string LabelNormal = "Normal";
    private const string LabelHard = "Hard";
    private const string LabelImpossible = "Impossible";
    private const string LabelGroupName = "Group name…";
    private const string LabelLeader = "Leader";
    private const string LabelAddTypesHint = "<color=#9fb4c8>Add types from the list on the right.</color>";
    private const string LabelAddType = "ADD TYPE";
    private const string LabelSearchTypes = "Search types…";
    private const string LabelNothingMatches = "Nothing matches.";
    private const string LabelHostileBlocked = "<color=#7f8a95>Hostile types are blocked by the setting.</color>";
    private const string LabelNewGroup = "NEW GROUP";
    private const string LabelGroupEditorUnsaved = "GROUP EDITOR · {0} <color=#7f8a95>· not saved</color>";
    private const string LabelGroupEditor = "GROUP EDITOR · {0}";
    private const string LabelMembersMax = "MEMBERS ({0} / max {1})";
    private const string LabelHostileMembers = "⚠ {0} member type(s) can be hostile to {1}. Inside one group they fight as allies; other groups see each by its own role.";
    private const string LabelOverUnitLimit = "⚠ {0} members, but only {1} Perun units can be alive at once; the rest won't spawn.";
    private const string LabelConfirmDelete = "Delete \"{0}\"?";
    private const string LabelUnsavedChanges = "Unsaved changes.";

    public const string NameControl = "PerunGroupName";

    public const string ControlSearch = "PerunGroupAddSearch";

    private const float RowHeight = 22f;
    private const float AddWidth = 240f;
    private const float ColumnGap = 12f;

    private static readonly string[] LeaderModes = { ChipAuto, ChipManual };
    private static readonly BotDifficulty[] Difficulties = { BotDifficulty.easy, BotDifficulty.normal, BotDifficulty.hard, BotDifficulty.impossible };
    private static readonly string[] DifficultyNames = { LabelEasy, LabelNormal, LabelHard, LabelImpossible };
    private static readonly string[] CountNames = BuildCountNames();

    private enum Confirm
    {
        None,
        Delete,
        Unsaved
    }

    private sealed class MemberRow
    {
        public string Name;
        public string Badge;
        public string Difficulty;
    }

    private readonly RoleCatalog _catalog = RoleCatalog.Shared;
    private readonly VirtualList _addList = new(RowHeight);
    private readonly List<WildSpawnType> _found = new();
    private readonly List<string> _foundNames = new();
    private readonly Dictionary<WildSpawnType, Stance> _stances = new();
    private readonly Dictionary<WildSpawnType, string> _badges = new();
    private readonly List<MemberRow> _rows = new();

    private GroupDraft _draft;
    private string _savedName;
    private int _savedVersion;
    private Action _pending;
    private Confirm _confirm;
    private string _error = "";
    private string _confirmText = "";
    private string _header = "";
    private string _membersTitle = "";
    private string _warning = "";
    private readonly GUIContent _warningContent = new();
    private int _builtVersion = -1;
    private (WildSpawnType leader, int hostility, int maxUnits) _builtKey;
    private bool _block;
    private int _category;
    private int _pendingCategory = -1;
    private string _search = "";
    private string _activeSearch = "";
    private string _searched;
    private Vector2 _memberScroll;
    private Vector2 _addScroll;
    private GUIStyle _count;

    public bool IsOpen => _draft != null;

    public Action<GroupPlan> Placed { get; set; }

    private bool Dirty => _draft.Version != _savedVersion;

    // savedName: the custom group the draft was loaded from, or null for a new group or a copy of an SPT preset.
    public void Open(GroupDraft draft, string savedName)
    {
        _pending += () =>
        {
            _draft = draft;
            _savedName = savedName;
            _savedVersion = savedName != null ? draft.Version : -1;
            _confirm = Confirm.None;
            _error = "";
            _builtVersion = -1;
            _memberScroll = Vector2.zero;
        };
    }

    public void RequestClose()
    {
        _pending += () =>
        {
            if (_draft == null)
            {
                return;
            }
            if (Dirty && _draft.Members.Count > 0)
            {
                ShowConfirm(Confirm.Unsaved);
            }
            else
            {
                Close();
            }
        };
    }

    // Called on Layout only: everything that adds or removes controls happens here.
    public void ApplyPending()
    {
        Action pending = _pending;
        _pending = null;
        pending?.Invoke();
        if (_draft == null)
        {
            return;
        }

        if (_pendingCategory >= 0)
        {
            _category = _pendingCategory;
            _pendingCategory = -1;
            _addScroll = Vector2.zero;
            _search = "";
        }
        if (_activeSearch != _search)
        {
            _activeSearch = _search;
            _addScroll = Vector2.zero;
        }
        Rebuild();
    }

    public void Draw(Rect rect)
    {
        if (_draft == null)
        {
            return;
        }
        _count ??= new GUIStyle(Theme.Label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };

        GUI.Box(rect, GUIContent.none, Theme.Panel);
        Theme.Rect(new Rect(rect.x, rect.y, rect.width, 2f), Theme.Accent);
        GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f));

        GUILayout.BeginHorizontal();
        GUILayout.Label(_header, Theme.Header, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("✕", Theme.Button, GUILayout.Width(28f)))
        {
            RequestClose();
        }
        GUILayout.EndHorizontal();

        DrawNameRow();

        float width = rect.width - 16f;
        GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
        GUILayout.BeginVertical(GUILayout.Width(width - AddWidth - ColumnGap));
        DrawMembers();
        GUILayout.EndVertical();
        GUILayout.Space(ColumnGap);
        GUILayout.BeginVertical(GUILayout.Width(AddWidth));
        DrawAddList();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        if (_warning.Length > 0)
        {
            Rect strip = GUILayoutUtility.GetRect(_warningContent, Theme.Label);
            Theme.Rect(strip, new Color(Theme.Danger.r, Theme.Danger.g, Theme.Danger.b, 0.18f));
            GUI.Label(strip, _warning, Theme.Label);
        }
        if (_error.Length > 0)
        {
            GUILayout.Label(_error, Theme.Label);
        }
        GUILayout.Space(4f);
        DrawFooter();

        GUILayout.EndArea();
    }

    private void DrawNameRow()
    {
        GUILayout.BeginHorizontal();
        GUI.SetNextControlName(NameControl);
        string name = GUILayout.TextField(_draft.Name, CustomGroups.MaxNameLength, Theme.Search, GUILayout.ExpandWidth(true));
        if (string.IsNullOrEmpty(name) && GUI.GetNameOfFocusedControl() != NameControl && Event.current.type == EventType.Repaint)
        {
            Rect field = GUILayoutUtility.GetLastRect();
            GUI.Label(new Rect(field.x + 6f, field.y + 3f, field.width, field.height), LabelGroupName, Theme.Small);
        }
        if (name != _draft.Name)
        {
            _draft.Name = name;
        }

        GUILayout.Label(LabelLeader, Theme.Small, GUILayout.Width(44f));
        int mode = _draft.Leader.HasValue ? 1 : 0;
        int picked = GUILayout.Toolbar(mode, LeaderModes, Theme.Tab, GUILayout.Width(150f));
        if (picked != mode && Event.current.type != EventType.Layout)
        {
            _pending += () => _draft.SetLeader(picked == 1 ? _draft.LeaderIndex : null);
        }
        GUILayout.EndHorizontal();
    }

    private void DrawMembers()
    {
        GUILayout.Label(_membersTitle, Theme.Header);
        _memberScroll = GUILayout.BeginScrollView(_memberScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
        if (_draft.Members.Count == 0)
        {
            GUILayout.Label(LabelAddTypesHint, Theme.Label);
        }

        bool manual = _draft.Leader.HasValue;
        for (int i = 0; i < _draft.Members.Count; i++)
        {
            int index = i;
            GroupDraft.Member member = _draft.Members[i];
            MemberRow row = _rows[i];
            GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));

            GUI.enabled = manual;
            if (GUILayout.Button(i == _draft.LeaderIndex ? "♛" : "○", Theme.Button, GUILayout.Width(24f), GUILayout.Height(RowHeight)))
            {
                _pending += () => _draft.SetLeader(index);
            }
            GUI.enabled = true;

            GUILayout.Label(row.Name, Theme.Row, GUILayout.ExpandWidth(true), GUILayout.Height(RowHeight));
            GUILayout.Label(row.Badge, Theme.Badge, GUILayout.Width(72f), GUILayout.Height(RowHeight));
            if (GUILayout.Button(row.Difficulty, Theme.Button, GUILayout.Width(76f), GUILayout.Height(RowHeight)))
            {
                _pending += () => NextDifficulty(index);
            }
            if (GUILayout.Button("−", Theme.Button, GUILayout.Width(22f), GUILayout.Height(RowHeight)))
            {
                _pending += () => _draft.SetCount(index, _draft.Members[index].Count - 1);
            }
            GUILayout.Label(CountNames[member.Count], _count, GUILayout.Width(24f), GUILayout.Height(RowHeight));
            if (GUILayout.Button("+", Theme.Button, GUILayout.Width(22f), GUILayout.Height(RowHeight)))
            {
                _pending += () => _draft.SetCount(index, _draft.Members[index].Count + 1);
            }
            if (GUILayout.Button("✕", Theme.DangerButton, GUILayout.Width(24f), GUILayout.Height(RowHeight)))
            {
                _pending += () => _draft.Remove(index);
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    private void DrawAddList()
    {
        GUILayout.Label(LabelAddType, Theme.Header);
        int category = GUILayout.SelectionGrid(_category, _catalog.CategoryNames, 3, Theme.Tab);
        if (category != _category)
        {
            _pendingCategory = category;
        }

        GUI.SetNextControlName(ControlSearch);
        _search = GUILayout.TextField(_search ?? "", 40, Theme.Search);
        if (string.IsNullOrEmpty(_search) && GUI.GetNameOfFocusedControl() != ControlSearch && Event.current.type == EventType.Repaint)
        {
            Rect field = GUILayoutUtility.GetLastRect();
            GUI.Label(new Rect(field.x + 6f, field.y + 3f, field.width, field.height), LabelSearchTypes, Theme.Small);
        }

        List<WildSpawnType> roles;
        IList<string> names;
        if (string.IsNullOrEmpty(_activeSearch))
        {
            roles = _catalog.Roles[_category];
            names = _catalog.Names[_category];
        }
        else
        {
            if (_searched != _activeSearch)
            {
                _searched = _activeSearch;
                _catalog.Search(_activeSearch, _found, _foundNames);
            }
            roles = _found;
            names = _foundNames;
        }

        _addList.Plan(roles.Count, _addScroll);
        _addScroll = GUILayout.BeginScrollView(_addScroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
        _addList.SpaceBefore();
        for (int i = _addList.First; i < _addList.Last; i++)
        {
            WildSpawnType role = roles[i];
            bool hostile = _draft.Members.Count > 0 && StanceOf(role).Kind == Attitude.Enemy;
            GUI.enabled = !(_block && hostile);
            if (GUILayout.Button(names[i], Theme.RowBadged, GUILayout.Height(RowHeight)))
            {
                _pending += () => _draft.Add(role, SpawnService.AllowedDifficulty(role, BotDifficulty.normal));
            }
            GUI.enabled = true;
            if (_draft.Members.Count > 0 && Event.current.type == EventType.Repaint)
            {
                GUI.Label(GUILayoutUtility.GetLastRect(), Badge(role), Theme.Badge);
            }
        }
        _addList.SpaceAfter();
        if (roles.Count == 0)
        {
            GUILayout.Label(LabelNothingMatches, Theme.Small);
        }
        GUILayout.EndScrollView();
        _addList.MeasureView();
        if (_block)
        {
            GUILayout.Label(LabelHostileBlocked, Theme.Small);
        }
    }

    private void DrawFooter()
    {
        GUILayout.BeginHorizontal();
        switch (_confirm)
        {
            case Confirm.Delete:
                GUILayout.Label(_confirmText, Theme.Label, GUILayout.ExpandWidth(true));
                if (GUILayout.Button(ChipDelete, Theme.DangerButton, GUILayout.Width(90f)))
                {
                    _pending += () =>
                    {
                        string error = CustomGroups.Delete(_savedName);
                        if (error == null)
                        {
                            Close();
                            return;
                        }
                        _error = $"<color=#ff8080>{error}</color>";
                        ShowConfirm(Confirm.None);
                    };
                }
                if (GUILayout.Button(ChipCancel, Theme.Button, GUILayout.Width(90f)))
                {
                    _pending += () => ShowConfirm(Confirm.None);
                }
                break;

            case Confirm.Unsaved:
                GUILayout.Label(_confirmText, Theme.Label, GUILayout.ExpandWidth(true));
                if (GUILayout.Button(ChipSave, Theme.Button, GUILayout.Width(90f)))
                {
                    _pending += () =>
                    {
                        if (Save(_savedName))
                        {
                            Close();
                        }
                        else
                        {
                            ShowConfirm(Confirm.None);
                        }
                    };
                }
                if (GUILayout.Button(ChipDiscard, Theme.DangerButton, GUILayout.Width(90f)))
                {
                    _pending += Close;
                }
                if (GUILayout.Button(ChipCancel, Theme.Button, GUILayout.Width(90f)))
                {
                    _pending += () => ShowConfirm(Confirm.None);
                }
                break;

            default:
                GUI.enabled = Dirty;
                if (GUILayout.Button(ChipSave, Theme.Button, GUILayout.Width(80f)))
                {
                    _pending += () => Save(_savedName);
                }
                GUI.enabled = true;
                if (GUILayout.Button(ChipSaveAs, Theme.Button, GUILayout.Width(80f)))
                {
                    _pending += () => Save(null);
                }
                GUI.enabled = _savedName != null;
                if (GUILayout.Button(ChipDelete, Theme.DangerButton, GUILayout.Width(80f)))
                {
                    _pending += () => ShowConfirm(Confirm.Delete);
                }
                GUI.enabled = true;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(ChipClose, Theme.Button, GUILayout.Width(80f)))
                {
                    RequestClose();
                }
                GUI.enabled = _draft.Members.Count > 0;
                if (GUILayout.Button(ChipPlaceGroup, Theme.Tab, GUILayout.Width(150f)))
                {
                    _pending += Place;
                }
                GUI.enabled = true;
                break;
        }
        GUILayout.EndHorizontal();
    }

    private void Rebuild()
    {
        bool block = Plugin.BlockHostileMembers.Value;
        WildSpawnType leader = _draft.Members.Count > 0 ? _draft.Members[_draft.LeaderIndex].Role : WildSpawnType.assault;
        (WildSpawnType leader, int hostility, int maxUnits) key = (leader, Hostility.Version, Commands.MaxUnits);
        if (_builtVersion == _draft.Version && _builtKey == key && _block == block)
        {
            return;
        }
        if (_builtKey.leader != key.leader || _builtKey.hostility != key.hostility)
        {
            _stances.Clear();
            _badges.Clear();
        }
        _builtVersion = _draft.Version;
        _builtKey = key;
        _block = block;

        string title = _savedName == null ? LabelNewGroup : _savedName.ToUpperInvariant();
        _header = Dirty ? string.Format(LabelGroupEditorUnsaved, title) : string.Format(LabelGroupEditor, title);
        _membersTitle = string.Format(LabelMembersMax, _draft.Total, Commands.MaxUnits);

        int hostile = 0;
        _rows.Clear();
        for (int i = 0; i < _draft.Members.Count; i++)
        {
            GroupDraft.Member member = _draft.Members[i];
            bool isLeader = i == _draft.LeaderIndex;
            string badge = isLeader ? "" : Badge(member.Role);
            if (!isLeader && StanceOf(member.Role).Kind is Attitude.Enemy or Attitude.Chance)
            {
                hostile++;
            }
            string color = BotKinds.Hex(BotKinds.Classify(member.Role, HostilityRules.SideOf(member.Role)));
            _rows.Add(new MemberRow
            {
                Name = $"<color={color}>{SpawnNames.Of(member.Role)}</color>",
                Badge = badge,
                Difficulty = DifficultyNames[Math.Max(Array.IndexOf(Difficulties, member.Difficulty), 0)]
            });
        }

        _warning = hostile == 0
            ? ""
            : string.Format(LabelHostileMembers, hostile, SpawnNames.Of(leader));
        if (_draft.Total > Commands.MaxUnits)
        {
            _warning += (_warning.Length > 0 ? "\n" : "") + string.Format(LabelOverUnitLimit, _draft.Total, Commands.MaxUnits);
        }
        _warningContent.text = _warning;
    }

    private Stance StanceOf(WildSpawnType role)
    {
        if (!_stances.TryGetValue(role, out Stance stance))
        {
            WildSpawnType leader = _builtKey.leader;
            stance = Hostility.Between(leader, HostilityRules.SideOf(leader), role, HostilityRules.SideOf(role));
            _stances[role] = stance;
        }
        return stance;
    }

    private string Badge(WildSpawnType role)
    {
        if (!_badges.TryGetValue(role, out string badge))
        {
            badge = HostilityRules.Badge(StanceOf(role));
            _badges[role] = badge;
        }
        return badge;
    }

    private void NextDifficulty(int index)
    {
        GroupDraft.Member member = _draft.Members[index];
        int at = Array.IndexOf(Difficulties, member.Difficulty);
        for (int step = 1; step <= Difficulties.Length; step++)
        {
            BotDifficulty next = Difficulties[(at + step + Difficulties.Length) % Difficulties.Length];
            if (SpawnService.AllowedDifficulty(member.Role, next) == next)
            {
                _draft.SetDifficulty(index, next);
                return;
            }
        }
    }

    private bool Save(string replacing)
    {
        if (!CustomGroups.TrySave(_draft, replacing, out GroupPlan plan, out string error))
        {
            _error = $"<color=#ff8080>{error}</color>";
            return false;
        }
        _savedName = plan.Name;
        _savedVersion = _draft.Version;
        _builtVersion = -1;
        _error = "";
        return true;
    }

    private void Place()
    {
        if (Dirty && !Save(_savedName))
        {
            return;
        }
        GroupPlan plan = CustomGroups.Find(_savedName);
        Close();
        if (plan != null)
        {
            Placed?.Invoke(plan);
        }
    }

    private void ShowConfirm(Confirm confirm)
    {
        _confirm = confirm;
        _confirmText = confirm switch
        {
            Confirm.Delete => string.Format(LabelConfirmDelete, _savedName),
            Confirm.Unsaved => LabelUnsavedChanges,
            _ => ""
        };
    }

    private void Close()
    {
        _draft = null;
        _confirm = Confirm.None;
        _error = "";
        string focused = GUI.GetNameOfFocusedControl();
        if (focused == NameControl || focused == ControlSearch)
        {
            GUIUtility.keyboardControl = 0;
        }
    }

    private static string[] BuildCountNames()
    {
        string[] names = new string[GroupDraft.MaxCount + 1];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = i.ToString();
        }
        return names;
    }
}
