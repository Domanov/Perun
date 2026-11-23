using System.Collections.Generic;
using EFT;
using Perun.Client.Units;
using UnityEngine;

namespace Perun.Client.UI;

internal sealed class SpawnPanel
{
    private static readonly string[] DifficultyNames = { "Easy", "Normal", "Hard", "Impossible" };
    private static readonly BotDifficulty[] Difficulties = { BotDifficulty.easy, BotDifficulty.normal, BotDifficulty.hard, BotDifficulty.impossible };
    private static readonly string[] SubTabs = { "BOTS", "GROUPS" };
    private static readonly string[] CountNames = { "1", "2", "3", "4", "5", "6", "8", "10" };
    private static readonly int[] CountValues = { 1, 2, 3, 4, 5, 6, 8, 10 };

    public const string SearchControl = "PerunSearch";

    private const float RowHeight = 22f;

    private readonly VirtualList _list = new(RowHeight);

    private const float StatusRefresh = 0.25f;
    private const int BotsTab = 0;
    private const int GroupsTab = 1;

    private readonly RoleCatalog _catalog = RoleCatalog.Shared;
    private readonly List<WildSpawnType> _found = new();
    private readonly List<string> _foundNames = new();
    private string _search = "";
    private string _activeSearch = "";
    private string _searched;
    private int _pendingCategory = -1;
    private float _nextStatus;
    private string _status = "";
    private string _prompt = "";
    private string _difficultyLabel = "Difficulty";
    private Vector2 _scroll;
    private int _category;
    private int _difficulty = 1;
    private int _countIndex;
    private WildSpawnType? _selected;
    private string _selectedName = "";
    private readonly Dictionary<WildSpawnType, string> _badges = new();
    private (WildSpawnType role, EPlayerSide side, bool bot, int version)? _badgeKey;
    private string _badgeNote = "";
    private readonly GroupsPanel _groups = new();
    private int _sub = BotsTab;
    private int _pendingSub = -1;

    public Player BadgeTarget { get; set; }

    public bool Visible { get; set; }

    public bool GroupsVisible => Visible && _sub == GroupsTab;
    public GroupEditor Editor => _groups.Editor;

    public bool Armed => Visible && (_sub == BotsTab ? _selected.HasValue : _groups.Armed);
    public GroupPlan Group => Visible && _sub == GroupsTab && _groups.Armed ? _groups.Selected : null;
    public BotDifficulty? GroupDifficulty => _groups.Difficulty;
    public bool GroupReady => _groups.Ready;
    public WildSpawnType Role => _selected ?? WildSpawnType.assault;
    public string RoleName => _selectedName;
    public WildSpawnType? Chosen => _selected;
    public BotDifficulty Difficulty => _selected.HasValue ? SpawnService.AllowedDifficulty(_selected.Value, Difficulties[_difficulty]) : Difficulties[_difficulty];
    public int Count => CountValues[_countIndex];

    public void Disarm()
    {
        _selected = null;
        _groups.Disarm();
        _nextStatus = 0f;
    }

    public void DrawContent()
    {
        if (Event.current.type == EventType.Layout && _pendingSub >= 0)
        {
            _sub = _pendingSub;
            _pendingSub = -1;
            _selected = null;
            _groups.Disarm();
            _nextStatus = 0f;
        }

        int sub = GUILayout.Toolbar(_sub, SubTabs, Theme.Tab);
        if (sub != _sub && Event.current.type != EventType.Layout)
        {
            _pendingSub = sub;
        }
        GUILayout.Space(4f);

        if (_sub == GroupsTab)
        {
            _groups.DrawContent();
            return;
        }
        DrawBots();
    }

    private void DrawBots()
    {
        if (Event.current.type == EventType.Layout)
        {
            if (_pendingCategory >= 0)
            {
                _category = _pendingCategory;
                _pendingCategory = -1;
                _scroll = Vector2.zero;
                _search = "";
            }
            if (_activeSearch != _search)
            {
                _activeSearch = _search;
                _scroll = Vector2.zero;
            }
            UpdateBadges();
            RefreshStatus();
        }

        GUILayout.Label("FACTION", Theme.Header);
        int category = GUILayout.SelectionGrid(_category, _catalog.CategoryNames, 3, Theme.Tab);
        if (category != _category)
        {
            _pendingCategory = category;
        }

        GUI.SetNextControlName(SearchControl);
        _search = GUILayout.TextField(_search ?? "", 40, Theme.Search);
        if (string.IsNullOrEmpty(_search) && GUI.GetNameOfFocusedControl() != SearchControl && Event.current.type == EventType.Repaint)
        {
            Rect field = GUILayoutUtility.GetLastRect();
            GUI.Label(new Rect(field.x + 6f, field.y + 3f, field.width, field.height), "Search all units…", Theme.Small);
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
            Filter();
            roles = _found;
            names = _foundNames;
        }

        _list.Plan(roles.Count, _scroll);
        _scroll = GUILayout.BeginScrollView(_scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
        _list.SpaceBefore();
        for (int i = _list.First; i < _list.Last; i++)
        {
            WildSpawnType role = roles[i];
            bool isSelected = _selected == role;
            bool badged = _badgeKey.HasValue;
            GUIStyle style = isSelected ? badged ? Theme.RowSelectedBadged : Theme.RowSelected : badged ? Theme.RowBadged : Theme.Row;
            if (GUILayout.Button(names[i], style, GUILayout.Height(RowHeight)))
            {
                if (isSelected)
                {
                    _selected = null;
                }
                else
                {
                    _selected = role;
                    _selectedName = names[i];
                    Commands.Prewarm(role, Difficulty, Count);
                }
                _nextStatus = 0f;
            }
            if (badged && Event.current.type == EventType.Repaint)
            {
                string badge = Badge(role);
                if (badge.Length > 0)
                {
                    GUI.Label(GUILayoutUtility.GetLastRect(), badge, Theme.Badge);
                }
            }
        }
        _list.SpaceAfter();
        if (roles.Count == 0)
        {
            GUILayout.Label("Nothing matches.", Theme.Small);
        }
        GUILayout.EndScrollView();
        _list.MeasureView();

        GUILayout.Space(6f);
        GUILayout.Label(_difficultyLabel, Theme.Header);
        int difficulty = GUILayout.Toolbar(_difficulty, DifficultyNames, Theme.Tab);
        GUILayout.Label("COUNT", Theme.Header);
        int countIndex = GUILayout.Toolbar(_countIndex, CountNames, Theme.Tab);

        if ((difficulty != _difficulty || countIndex != _countIndex) && Event.current.type != EventType.Layout)
        {
            _difficulty = difficulty;
            _countIndex = countIndex;
            _nextStatus = 0f;
        }

        GUILayout.Space(6f);
        GUILayout.Label(_prompt, Theme.Label);
        GUILayout.Label(_status, Theme.Label);
    }

    private void Filter()
    {
        if (_searched != _activeSearch)
        {
            _searched = _activeSearch;
            _catalog.Search(_activeSearch, _found, _foundNames);
        }
    }

    private void RefreshStatus()
    {
        float now = Time.unscaledTime;
        if (now < _nextStatus)
        {
            return;
        }
        _nextStatus = now + StatusRefresh;

        string pending = Commands.Pending > 0 ? $" · spawning {Commands.Pending}" : "";
        string units = $"Perun units {Commands.AliveCount}/{Commands.MaxUnits}{pending}";
        string result = string.IsNullOrEmpty(Commands.LastResult) ? "" : "\n" + Commands.LastResult;

        if (!_selected.HasValue)
        {
            _difficultyLabel = "DIFFICULTY";
            _prompt = "<color=#9fb4c8>Pick a unit, then click the map to place it.</color>";
            _status = units + result;
            _prompt += _badgeNote;
            return;
        }

        WildSpawnType role = _selected.Value;
        BotDifficulty difficulty = Difficulty;
        Commands.Prewarm(role, difficulty, Count);

        _difficultyLabel = "DIFFICULTY" + AllowedNote(role);
        _prompt = $"<color=#ffc847>Click the map to place {_selectedName} ×{Count}</color>  <color=#9fb4c8>· RMB stops</color>";
        int ready = Commands.ReadyCount(role, difficulty);
        string readiness = ready >= Count
            ? $"<color=#8be38b>● Ready to drop: {ready}</color> · spawns instantly"
            : Commands.IsWarming(role, difficulty)
                ? $"<color=#ffc847>◐ Preparing in advance… {ready}/{Count} ready</color>"
                : $"<color=#ffc847>● {ready}/{Count} ready</color> · the rest loads when you place them";
        _status = $"{readiness}\n{units}{result}";
        _prompt += _badgeNote;
    }

    private void UpdateBadges()
    {
        Player target = BadgeTarget;
        if (target == null || target.Profile == null)
        {
            if (_badgeKey.HasValue)
            {
                _badgeKey = null;
                _badgeNote = "";
                _nextStatus = 0f;
            }
            return;
        }

        bool bot = Bots.IsBot(target);
        WildSpawnType role = bot ? target.Profile.Info.Settings.Role : WildSpawnType.assault;
        var key = (role, target.Side, bot, Hostility.Version);
        if (_badgeKey == key)
        {
            return;
        }

        _badgeKey = key;
        _badges.Clear();
        string who = bot ? $"{target.Profile.Nickname} ({SpawnNames.Of(role)})" : target.Profile.Nickname;
        _badgeNote = $"\n<color=#7f8a95>Badges: attitude toward {who}</color>";
        _nextStatus = 0f;
    }

    private string Badge(WildSpawnType role)
    {
        if (_badges.TryGetValue(role, out string badge))
        {
            return badge;
        }

        var key = _badgeKey.Value;
        Stance stance = key.bot
            ? Hostility.Between(key.role, key.side, role, HostilityRules.SideOf(role))
            : Hostility.TowardPlayer(role, key.side);
        badge = HostilityRules.Badge(stance);
        _badges[role] = badge;
        return badge;
    }

    private string AllowedNote(WildSpawnType role)
    {
        BotDifficulty picked = Difficulties[_difficulty];
        BotDifficulty used = SpawnService.AllowedDifficulty(role, picked);
        return used == picked ? "" : $"  <color=#ffd36f>(only {used})</color>";
    }
}
