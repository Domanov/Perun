using System.Collections.Generic;
using System.Text;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using Perun.Client.Mode;
using Perun.Client.Orders;
using Perun.Client.Units;
using Perun.Client.View;
using UnityEngine;

namespace Perun.Client.UI;

internal enum AssetTab
{
    Units,
    Environment,
    Arsenal
}

// Too large for one class (top bar, dock, selection card, entity list, help, messages). Splitting it
// needs the IMGUI layout rule kept across panels (control counts change only on Layout), so it is
// left as one class until it can be retested panel by panel in game.
internal sealed class PerunHud
{
    private const float StatusRefresh = 0.1f;
    private const float LabelPruneInterval = 5f;
    private const float Gap = 10f;
    private const float TopHeight = 34f;
    private const float StatusHeight = 26f;
    private const float LeftWidth = 290f;
    private const float RightWidth = 340f;
    private const float CardHeight = 176f;
    private const int MaxJobsShown = 3;
    private const float JobHeight = 44f;
    private static readonly string[] Spinner = { "◐", "◓", "◑", "◒" };

    private const string PausedHost = "<size=20><b>PAUSED</b></size>  <color=#9fb4c8>P to resume</color>";
    private const string PausedClient = "<size=20><b>PAUSED</b></size>  <color=#9fb4c8>by the game master</color>";
    private const string CantPlace = "<color=#ff8080>can't place here</color>";
    private const string HelpText =
        "<b><color=#ffc847>CAMERA</color></b>\n" +
        "WASD pan · Shift faster · Q/E or middle-drag rotate · wheel zoom · {0}+wheel tilt · R/F floor up/down\n" +
        "Home back to your body · click the magenta dot, then the map, to change where you return · L lock camera on the selected unit · Ctrl+F fly to the selection\n\n" +
        "<b><color=#ffc847>UNITS</color></b>\n" +
        "LMB select · drag a box · Shift adds · click empty ground clears\n" +
        "RMB move · Shift+RMB run · Ctrl+RMB forced move (ignores enemies) · X halt\n" +
        "Del remove · Ctrl+Del kill · Heal on the card (click a player to select them) · Alt+right-click teleport (or Teleport on the card, then click)\n\n" +
        "<b><color=#ffc847>EDITOR</color></b>\n" +
        "B units browser · T environment · I arsenal (gear of the selected unit) · N vision (night green, white, amber, then thermal white hot, black hot, fusion, rainbow, off) · P pause the world · H hide the interface · F1 this help · {1} leave\n\n" +
        "<b><color=#ffc847>TAKE CONTROL</color></b>\n" +
        "Select one bot and press {2} (or Control on the card) to play it with the game's own controls, HUD and inventory · {1} gives it back";


    private sealed class LabelCache
    {
        public int Key = -1;
        public string Text;
    }

    private readonly Dictionary<Player, LabelCache> _labels = new();
    private readonly List<Player> _labelGone = new();
    private readonly StringBuilder _builder = new(512);
    private readonly PerunMode _mode;
    private readonly WeatherPanel _weather = new();
    private readonly SpawnPanel _spawn = new();
    private readonly EntityPanel _entities = new();
    private readonly ArsenalPanel _arsenal = new();
    private readonly List<Rect> _uiRects = new();

    private float _nextLabelPrune;
    private float _nextStatus;
    private string _statusLeft = "";
    private string _statusRight = "";
    private string _topInfo = "";
    private string _cardTitle = "";
    private string _cardInfo = "";
    private int _cardHealth = 127;
    private string _help;
    private GameWorld _labelWorld;
    private WildSpawnType _ghostRole;
    private int _ghostCount = -1;
    private bool _ghostReady;
    private string _ghost;
    private GroupPlan _ghostGroup;
    private bool _ghostGroupReady;
    private string _groupGhost;

    private bool _dockOpen = true;
    private AssetTab _tab = AssetTab.Units;
    private int _pendingTab = -1;
    private bool _pendingClose;
    private bool _pendingHelp;
    private bool _cardShown;
    private float _loadingHeight;
    private readonly string[] _jobText = new string[MaxJobsShown];
    private readonly int[] _jobKey = new int[MaxJobsShown];
    private Vector2 _envScroll;

    private GUIStyle _label;
    private GUIStyle _marker;
    private GUIStyle _label2;
    private GUIStyle _banner;

    private string _returnLabel;
    private string _hint;
    private string _controlLabel;
    private string _controlKey;
    private Player _controlHintFor;
    private string _controlHintText = "";

    public PerunHud(PerunMode mode)
    {
        _mode = mode;
        Plugin.ToggleKey.SettingChanged += (_, _) => RefreshKeyLabels();
        Plugin.TiltKey.SettingChanged += (_, _) => RefreshKeyLabels();
        Plugin.ControlKey.SettingChanged += (_, _) => RefreshKeyLabels();
        RefreshKeyLabels();
    }

    private void RefreshKeyLabels()
    {
        string toggle = Plugin.ToggleKey.Value.ToString();
        _help = string.Format(HelpText, Plugin.TiltKey.Value, toggle, Plugin.ControlKey.Value);
        _returnLabel = $"RETURN  ({toggle})";
        _hint = $"<color=#7f8a95>F1 help · H hide · {toggle} return</color>";
        _controlLabel = $"Control ({Plugin.ControlKey.Value})";
        _controlKey = toggle;
        _controlHintFor = null;
    }

    public SpawnPanel Spawn => _spawn;

    public static bool MouseOverUi { get; private set; }

    public static bool Typing { get; private set; }

    public static bool InputBlocked => Typing || Compat.ConfigManagerCompat.WindowOpen;

    public static bool PointerBlocked => MouseOverUi || Compat.ConfigManagerCompat.WindowOpen;

    public bool Hidden { get; set; }

    public bool HelpVisible { get; set; }

    public static void ReleaseInput()
    {
        if (Typing)
        {
            GUIUtility.keyboardControl = 0;
        }
        Typing = false;
        MouseOverUi = false;
    }

    public bool HasMessage => !string.IsNullOrEmpty(_mode.Message) && Time.time < _mode.MessageUntil;

    public void ToggleTab(AssetTab tab)
    {
        if (_dockOpen && _tab == tab)
        {
            _dockOpen = false;
        }
        else
        {
            _dockOpen = true;
            _tab = tab;
        }
        _spawn.Visible = _dockOpen && _tab == AssetTab.Units;
    }

    public void Draw()
    {
        EnsureStyles();
        Theme.Ensure();
        Event current = Event.current;

        if (_mode.Controlling)
        {
            ReleaseInput();
            if (current.type == EventType.Repaint)
            {
                DrawControlHint();
                DrawBanners(false);
            }
            return;
        }

        if (!_mode.Active || Hidden)
        {
            if (Typing)
            {
                GUIUtility.keyboardControl = 0;
            }
            Typing = false;
            MouseOverUi = false;
            if (!_mode.Active)
            {
                _spawn.Visible = false;
            }
            if (current.type == EventType.Repaint)
            {
                DrawBanners(!_mode.Active);
            }
            return;
        }

        if (current.type == EventType.Layout)
        {
            ApplyPending();
            if (Time.unscaledTime >= _nextStatus)
            {
                _nextStatus = Time.unscaledTime + StatusRefresh;
                BuildStatus(_mode.Picker);
            }
        }

        if (Typing && current.type == EventType.KeyDown && (current.keyCode == KeyCode.Escape || current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter))
        {
            GUIUtility.keyboardControl = 0;
            current.Use();
        }

        Layout(out Rect top, out Rect left, out Rect card, out Rect right, out Rect status, out Rect help);

        if (Typing && current.type == EventType.MouseDown && !Over(current.mousePosition))
        {
            GUIUtility.keyboardControl = 0;
        }

        if (current.type == EventType.Repaint)
        {
            DrawWorld();
        }

        DrawTopBar(top);
        DrawLeft(left);
        if (_cardShown)
        {
            DrawCard(card);
        }
        DrawDock(right);
        DrawStatus(status);
        if (HelpVisible)
        {
            DrawHelp(help);
        }

        if (current.type == EventType.Repaint)
        {
            DrawLoading(top);
            DrawBanners(false);
            MouseOverUi = Over(current.mousePosition);
            string focused = GUIUtility.keyboardControl != 0 ? GUI.GetNameOfFocusedControl() : null;
            Typing = focused == SpawnPanel.SearchControl || focused == GroupsPanel.SearchControl || focused == ArsenalPanel.SearchControl || focused == ArsenalPanel.NameControl;
        }
    }

    private void ApplyPending()
    {
        if (_pendingTab >= 0)
        {
            ToggleTab((AssetTab)_pendingTab);
            _pendingTab = -1;
        }
        if (_pendingHelp)
        {
            _pendingHelp = false;
            HelpVisible = !HelpVisible;
        }
        if (_pendingClose)
        {
            _pendingClose = false;
            _dockOpen = false;
            _spawn.Visible = false;
        }
        _cardShown = _mode.Picker.Selection.Count > 0;
        _spawn.Visible = _dockOpen && _tab == AssetTab.Units;
        _spawn.BadgeTarget = _mode.Picker.Selected;
    }

    private void Layout(out Rect top, out Rect left, out Rect card, out Rect right, out Rect status, out Rect help)
    {
        float width = Screen.width;
        float height = Screen.height;
        float body = TopHeight + Gap * 2f;
        float bottom = height - StatusHeight - Gap;

        top = new Rect(Gap, Gap, width - Gap * 2f, TopHeight);
        status = new Rect(Gap, height - StatusHeight - Gap * 0.5f, width - Gap * 2f, StatusHeight);

        float cardSpace = _cardShown ? CardHeight + Gap : 0f;
        left = new Rect(Gap, body, LeftWidth, Mathf.Max(120f, bottom - body - cardSpace - Gap));
        card = new Rect(Gap, left.yMax + Gap, LeftWidth, CardHeight);

        right = _dockOpen
            ? new Rect(width - RightWidth - Gap, body, RightWidth, Mathf.Max(200f, bottom - body - Gap))
            : new Rect(width - RightWidth - Gap, body, RightWidth, 34f);

        help = new Rect(width / 2f - 330f, height / 2f - 180f, 660f, 360f);

        _uiRects.Clear();
        _uiRects.Add(top);
        _uiRects.Add(left);
        if (_cardShown)
        {
            _uiRects.Add(card);
        }
        _uiRects.Add(right);
        _uiRects.Add(status);
        if (HelpVisible)
        {
            _uiRects.Add(help);
        }
    }

    private bool Over(Vector2 mouse)
    {
        for (int i = 0; i < _uiRects.Count; i++)
        {
            if (_uiRects[i].Contains(mouse))
            {
                return true;
            }
        }
        return false;
    }

    private void DrawTopBar(Rect rect)
    {
        GUI.Box(rect, GUIContent.none, Theme.Bar);
        GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 4f, rect.width - 20f, rect.height - 8f));
        GUILayout.BeginHorizontal();
        GUILayout.Label("PERUN", Theme.Title, GUILayout.Width(80f), GUILayout.ExpandHeight(true));
        GUILayout.Label(_topInfo, Theme.BarText, GUILayout.ExpandHeight(true));
        GUILayout.FlexibleSpace();

        if (GUILayout.Toggle(View.RtsVision.On, View.RtsVision.Label, Theme.Tab, GUILayout.Width(170f), GUILayout.ExpandHeight(true)) != View.RtsVision.On)
        {
            View.RtsVision.Cycle();
        }

        bool paused = WorldPause.Paused;
        if (GUILayout.Toggle(paused, paused ? "▶  RESUME  (P)" : "❚❚  PAUSE  (P)", Theme.Tab, GUILayout.Width(150f), GUILayout.ExpandHeight(true)) != paused)
        {
            _mode.TogglePause();
        }
        if (GUILayout.Button("HIDE  (H)", Theme.Button, GUILayout.Width(90f), GUILayout.ExpandHeight(true)))
        {
            Hidden = true;
        }
        if (GUILayout.Toggle(HelpVisible, "HELP  (F1)", Theme.Tab, GUILayout.Width(100f), GUILayout.ExpandHeight(true)) != HelpVisible)
        {
            _pendingHelp = true;
        }
        if (GUILayout.Button(_returnLabel, Theme.DangerButton, GUILayout.Width(130f), GUILayout.ExpandHeight(true)))
        {
            _mode.RequestExit();
        }
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void DrawLeft(Rect rect)
    {
        GUI.Box(rect, GUIContent.none, Theme.Panel);
        GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));
        _entities.DrawContent(_mode);
        GUILayout.EndArea();
    }

    private void DrawCard(Rect rect)
    {
        CursorPicker picker = _mode.Picker;
        GUI.Box(rect, GUIContent.none, Theme.Panel);
        GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));
        GUILayout.Label(_cardTitle, Theme.Header);

        Rect bar = GUILayoutUtility.GetRect(rect.width - 16f, 8f);
        if (Event.current.type == EventType.Repaint && _cardHealth <= 100)
        {
            Rect inner = new(bar.x, bar.y + 2f, bar.width, 5f);
            Theme.Rect(inner, new Color(1f, 1f, 1f, 0.08f));
            float t = _cardHealth / 100f;
            Theme.Rect(new Rect(inner.x, inner.y, inner.width * t, inner.height), Color.Lerp(Theme.Danger, new Color(0.45f, 0.9f, 0.45f), t));
        }

        GUILayout.Label(_cardInfo, Theme.Small);
        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Fly to", Theme.Button))
        {
            _mode.FocusSelected();
        }
        bool locked = _mode.Camera.Following != null;
        if (GUILayout.Toggle(locked, "Lock cam", Theme.Tab) != locked)
        {
            _mode.ToggleLock();
        }
        GUI.enabled = _mode.CanControlSelection;
        if (GUILayout.Button(_controlLabel, Theme.Button))
        {
            _mode.RequestControl();
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        bool actionable = !_mode.SelectionWatchOnly;
        GUI.enabled = actionable;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Heal", Theme.Button))
        {
            _mode.HealSelection();
        }
        if (GUILayout.Toggle(_mode.TeleportArmed, "Teleport", Theme.Tab) && !_mode.TeleportArmed)
        {
            _mode.ArmTeleport();
        }
        if (GUILayout.Button("Halt", Theme.Button))
        {
            _mode.HaltSelection();
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUI.enabled = true;
        if (GUILayout.Button("Deselect", Theme.Button))
        {
            picker.ClearSelection();
        }
        GUI.enabled = actionable;
        if (GUILayout.Button("Remove", Theme.DangerButton))
        {
            _mode.RemoveSelection(false);
        }
        if (GUILayout.Button("Kill", Theme.DangerButton))
        {
            _mode.RemoveSelection(true);
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void DrawDock(Rect rect)
    {
        GUI.Box(rect, GUIContent.none, Theme.Panel);
        GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f));

        GUILayout.BeginHorizontal();
        bool units = _dockOpen && _tab == AssetTab.Units;
        bool environment = _dockOpen && _tab == AssetTab.Environment;
        bool arsenal = _dockOpen && _tab == AssetTab.Arsenal;
        if (GUILayout.Toggle(units, "UNITS  (B)", Theme.Tab) != units)
        {
            _pendingTab = (int)AssetTab.Units;
        }
        if (GUILayout.Toggle(environment, "ENV  (T)", Theme.Tab) != environment)
        {
            _pendingTab = (int)AssetTab.Environment;
        }
        if (GUILayout.Toggle(arsenal, "ARSENAL  (I)", Theme.Tab) != arsenal)
        {
            _pendingTab = (int)AssetTab.Arsenal;
        }
        if (_dockOpen && GUILayout.Button("✕", Theme.Button, GUILayout.Width(28f)))
        {
            _pendingClose = true;
        }
        GUILayout.EndHorizontal();

        if (_dockOpen)
        {
            GUILayout.Space(6f);
            if (_tab == AssetTab.Units)
            {
                _spawn.DrawContent();
            }
            else if (_tab == AssetTab.Arsenal)
            {
                _arsenal.DrawContent(_mode.Picker.Selection, _spawn.Chosen, _spawn.RoleName);
            }
            else
            {
                _envScroll = GUILayout.BeginScrollView(_envScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
                _weather.DrawContent();
                GUILayout.EndScrollView();
            }
        }

        GUILayout.EndArea();
    }

    private void DrawStatus(Rect rect)
    {
        GUI.Box(rect, GUIContent.none, Theme.Bar);
        GUI.Label(new Rect(rect.x, rect.y, rect.width * 0.75f, rect.height), _statusLeft, Theme.BarText);
        GUI.Label(rect, _statusRight, Theme.BarTextRight);
    }

    private void DrawHelp(Rect rect)
    {
        GUI.Box(rect, GUIContent.none, Theme.Panel);
        GUILayout.BeginArea(new Rect(rect.x + 14f, rect.y + 12f, rect.width - 28f, rect.height - 24f));
        GUILayout.Label("CONTROLS", Theme.Header);
        GUILayout.Space(6f);
        GUILayout.Label(_help, Theme.Label);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Close  (F1)", Theme.Button))
        {
            _pendingHelp = true;
        }
        GUILayout.EndArea();
    }

    private void DrawBanners(bool showPause)
    {
        if (showPause && WorldPause.Paused)
        {
            GUI.Label(new Rect(Screen.width / 2f - 200f, 40f, 400f, 34f), _mode.Active ? PausedHost : PausedClient, _banner);
        }

        if (HasMessage)
        {
            float y = _mode.Active && !Hidden ? TopHeight + Gap * 2f + 4f + _loadingHeight : 80f;
            GUI.Label(new Rect(Screen.width / 2f - 260f, y, 520f, 28f), _mode.Message, Theme.Toast);
        }

        if (_mode.Active && !Hidden && WorldPause.Paused)
        {
            float t = 3f;
            Color edge = new(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.6f);
            Theme.Rect(new Rect(0f, 0f, Screen.width, t), edge);
            Theme.Rect(new Rect(0f, Screen.height - t, Screen.width, t), edge);
            Theme.Rect(new Rect(0f, 0f, t, Screen.height), edge);
            Theme.Rect(new Rect(Screen.width - t, 0f, t, Screen.height), edge);
        }
    }

    private void DrawControlHint()
    {
        Player bot = _mode.ControlledBot;
        if (bot != _controlHintFor || _controlHintText.Length == 0)
        {
            _controlHintFor = bot;
            string name = bot?.Profile != null ? bot.Profile.Nickname : "a bot";
            _controlHintText = $"<color=#ffc847><b>PERUN</b></color>  controlling {name}  ·  <b>{_controlKey}</b> gives it back  ·  <b>{Plugin.CrateKey.Value}</b> arsenal crate";
        }
        GUI.Label(new Rect(Screen.width / 2f - 280f, 6f, 560f, 24f), _controlHintText, Theme.Toast);
    }

    private void DrawLoading(Rect top)
    {
        IReadOnlyList<IJobView> jobs = Commands.Jobs;
        int shown = Mathf.Min(jobs.Count, MaxJobsShown);
        if (shown == 0)
        {
            _loadingHeight = 0f;
            return;
        }

        float width = 480f;
        float height = 10f + shown * JobHeight + (jobs.Count > shown ? 16f : 0f);
        Rect box = new(Screen.width / 2f - width / 2f, top.yMax + Gap, width, height);
        _loadingHeight = height + 6f;

        GUI.Box(box, GUIContent.none, Theme.Panel);
        Theme.Rect(new Rect(box.x, box.y, box.width, 2f), Theme.Accent);

        float now = Time.realtimeSinceStartup;
        string spin = Spinner[(int)(now * 8f) % Spinner.Length];
        for (int i = 0; i < shown; i++)
        {
            IJobView job = jobs[i];
            Rect row = new(box.x + 12f, box.y + 8f + i * JobHeight, box.width - 24f, JobHeight - 6f);
            GUI.Label(new Rect(row.x, row.y, 20f, 20f), spin, Theme.Title);
            GUI.Label(new Rect(row.x + 22f, row.y, row.width - 22f, 20f), JobLine(i, job), Theme.Label);
            DrawProgress(new Rect(row.x, row.y + 23f, row.width, 8f), job.Progress, job.IsSpawn ? Theme.Accent : Theme.Dim, now);
        }

        if (jobs.Count > shown)
        {
            GUI.Label(new Rect(box.x + 12f, box.yMax - 20f, box.width - 24f, 16f), $"+{jobs.Count - shown} more queued", Theme.Small);
        }
    }

    private string JobLine(int slot, IJobView job)
    {
        int percent = job.Progress >= 0f ? Mathf.RoundToInt(job.Progress * 100f) : -1;
        int seconds = Mathf.FloorToInt(job.Elapsed);
        int key = ((int)job.Stage) | ((percent + 1) << 3) | (job.Created << 11) | (seconds << 17) | (job.IsSpawn ? 1 << 30 : 0);
        if (_jobKey[slot] != key || _jobText[slot] == null)
        {
            _jobKey[slot] = key;
            string stage = job.Stage switch
            {
                LoadStage.Waiting => "<color=#9fb4c8>queued</color>",
                LoadStage.Server => "<color=#ffc847>fetching profiles from the server…</color>",
                LoadStage.Gear => $"<color=#ffc847>loading gear</color> {percent}%",
                LoadStage.Creating => $"<color=#8be38b>spawning</color> {job.Created}/{job.Total}",
                _ => "done"
            };
            string title = job.IsSpawn ? $"<b><color=#ffc847>{job.Title}</color></b>" : $"<color=#c8d2dc>{job.Title}</color> <color=#7f8a95>(in advance)</color>";
            _jobText[slot] = $"{title}  ·  {stage}  <color=#7f8a95>{seconds} s</color>";
        }
        return _jobText[slot];
    }

    private static void DrawProgress(Rect rect, float progress, Color color, float now)
    {
        Theme.Rect(rect, new Color(1f, 1f, 1f, 0.08f));
        if (progress >= 0f)
        {
            Theme.Rect(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(progress), rect.height), color);
            return;
        }

        float segment = rect.width * 0.28f;
        float travel = rect.width + segment;
        float x = rect.x - segment + Mathf.Repeat(now * 0.7f, 1f) * travel;
        float left = Mathf.Max(rect.x, x);
        float right = Mathf.Min(rect.xMax, x + segment);
        if (right > left)
        {
            Theme.Rect(new Rect(left, rect.y, right - left, rect.height), new Color(color.r, color.g, color.b, 0.85f));
        }
    }

    private void DrawWorld()
    {
        Camera camera = _mode.ViewCamera;
        if (camera == null)
        {
            return;
        }

        CursorPicker picker = _mode.Picker;
        DrawSelectionBrackets(camera, picker);

        DrawReturnPoint(camera);

        if (Plugin.ShowLabels.Value && Singleton<GameWorld>.Instantiated)
        {
            DrawLabels(camera, picker, Singleton<GameWorld>.Instance);
        }

        IReadOnlyList<Player> selected = picker.Selection;
        for (int i = 0; i < selected.Count; i++)
        {
            if (Commands.TryGetOrder(selected[i], out Vector3 target, out _, out bool forcedOrder) && CursorPicker.ToScreen(camera, target, out Vector2 destination))
            {
                GUI.Label(new Rect(destination.x - 20f, destination.y - 20f, 40f, 40f), forcedOrder ? "✖" : "✚", _marker);
            }
        }

        if (picker.Dragging)
        {
            Color tint = Plugin.SelectionColor.Value;
            Rect drag = picker.DragRect;
            FillRect(drag, new Color(tint.r, tint.g, tint.b, 0.15f));
            FrameRect(drag, 2f, new Color(0f, 0f, 0f, 0.6f), 1f);
            FrameRect(drag, 1.5f, tint, 0f);
        }

        if (_spawn.Armed && !MouseOverUi)
        {
            Vector3 cursorPosition = Input.mousePosition;
            string ghost;
            GroupPlan group = _spawn.Group;
            if (group != null)
            {
                ghost = picker.HasNavPoint ? GroupGhost(group, _spawn.GroupReady) : CantPlace;
            }
            else
            {
                bool ready = Commands.ReadyCount(_spawn.Role, _spawn.Difficulty) >= _spawn.Count;
                ghost = picker.HasNavPoint ? Ghost(_spawn.RoleName, _spawn.Role, _spawn.Count, ready) : CantPlace;
            }
            GUI.Label(new Rect(cursorPosition.x + 14f, Screen.height - cursorPosition.y - 8f, 300f, 22f), ghost, _label2);
        }
    }

    private string Ghost(string name, WildSpawnType role, int count, bool ready)
    {
        if (role != _ghostRole || count != _ghostCount || ready != _ghostReady)
        {
            _ghostRole = role;
            _ghostCount = count;
            _ghostReady = ready;
            _ghost = ready ? $"+ {name} ×{count}  <color=#8be38b>ready</color>" : $"+ {name} ×{count}  <color=#ffc847>needs loading first</color>";
        }
        return _ghost;
    }

    private string GroupGhost(GroupPlan group, bool ready)
    {
        if (!ReferenceEquals(group, _ghostGroup) || ready != _ghostGroupReady || _groupGhost == null)
        {
            _ghostGroup = group;
            _ghostGroupReady = ready;
            _groupGhost = ready
                ? $"+ {group.Name} ({group.CountText})  <color=#8be38b>ready</color>"
                : $"+ {group.Name} ({group.CountText})  <color=#ffc847>needs loading first</color>";
        }
        return _groupGhost;
    }

    private void BuildStatus(CursorPicker picker)
    {
        StringBuilder b = _builder.Clear();
        b.Append("<color=#7f8a95>CURSOR</color>  ");
        if (picker.HasGround)
        {
            Vector3 ground = picker.Ground;
            b.Append(Mathf.RoundToInt(ground.x)).Append(", ").Append(Mathf.RoundToInt(ground.y)).Append(", ").Append(Mathf.RoundToInt(ground.z));
            if (!picker.HasNavPoint)
            {
                b.Append("  <color=#ff8080>no navmesh</color>");
            }
        }
        else
        {
            b.Append('-');
        }

        b.Append("     <color=#7f8a95>SELECTED</color>  ");
        int count = picker.Selection.Count;
        if (count == 0)
        {
            b.Append('-');
        }
        else if (count == 1)
        {
            AppendDescription(b, picker.Selected);
        }
        else
        {
            b.Append(count).Append(" units");
        }

        Player following = _mode.Camera.Following;
        if (following != null)
        {
            b.Append("     <color=#ffd36f>camera locked on ").Append(following.Profile?.Nickname).Append(" · L or WASD releases</color>");
        }
        _statusLeft = b.ToString();

        _statusRight = _spawn.Armed
            ? "<color=#ffc847>Placing: click the map · RMB cancels</color>"
            : _hint;

        b.Clear();
        if (Singleton<GameWorld>.Instantiated && Singleton<GameWorld>.Instance.GameDateTime != null)
        {
            b.Append("<color=#7f8a95>TIME</color>  ").Append(Theme.Time(Singleton<GameWorld>.Instance.GameDateTime.Calculate())).Append("     ");
        }
        b.Append("<color=#7f8a95>UNITS</color>  ").Append(Commands.AliveCount).Append('/').Append(Commands.MaxUnits);
        if (Commands.Pending > 0)
        {
            b.Append("  <color=#ffc847>+").Append(Commands.Pending).Append(" spawning</color>");
        }
        if (WorldPause.Paused)
        {
            b.Append("     <color=#ffc847><b>WORLD PAUSED</b></color>");
        }
        _topInfo = b.ToString();

        BuildCard(picker);
    }

    private void BuildCard(CursorPicker picker)
    {
        int count = picker.Selection.Count;
        if (count == 0)
        {
            return;
        }

        StringBuilder b = _builder.Clear();
        if (count == 1)
        {
            Player unit = picker.Selected;
            b.Append("<color=").Append(BotKinds.Hex(BotKinds.Classify(unit))).Append('>').Append(unit.Profile?.Nickname ?? "?").Append("</color>");
            if (Commands.IsOurs(unit))
            {
                b.Append("  <color=#ffc847>★</color>");
            }
            _cardTitle = b.ToString();

            _cardHealth = HealthPercent(unit);
            b.Clear();
            if (unit.Profile != null && Bots.IsBot(unit))
            {
                b.Append(SpawnNames.Of(unit.Profile.Info.Settings.Role));
            }
            else
            {
                b.Append("Player");
            }
            if (_cardHealth <= 100)
            {
                b.Append(" · ").Append(Theme.Percent(_cardHealth)).Append(" health");
            }
            if (Commands.TryGetOrder(unit, out Vector3 destination, out bool run, out bool forced))
            {
                b.Append("\n<color=#ffc847>").Append(forced ? "Forced move" : run ? "Running" : "Moving").Append("</color> · ").Append(Mathf.RoundToInt(Vector3.Distance(unit.Position, destination))).Append(" m to go");
            }
            else
            {
                b.Append("\nNo orders");
            }
            _cardInfo = b.ToString();
            return;
        }

        b.Append(count).Append(" UNITS SELECTED");
        _cardTitle = b.ToString();
        _cardHealth = 127;
        b.Clear();
        int ordered = 0;
        int ours = 0;
        IReadOnlyList<Player> selection = picker.Selection;
        for (int i = 0; i < selection.Count; i++)
        {
            Player unit = selection[i];
            if (Commands.HasOrder(unit))
            {
                ordered++;
            }
            if (Commands.IsOurs(unit))
            {
                ours++;
            }
        }
        b.Append(ours).Append(" yours · ").Append(ordered).Append(" with orders\nRMB on the map to move them");
        _cardInfo = b.ToString();
    }

    private void DrawLabels(Camera camera, CursorPicker picker, GameWorld world)
    {
        if (world != _labelWorld)
        {
            _labelWorld = world;
            _labels.Clear();
        }

        float now = Time.unscaledTime;
        if (now >= _nextLabelPrune)
        {
            _nextLabelPrune = now + LabelPruneInterval;
            PruneLabels();
        }

        float maxSqr = Plugin.LabelDistance.Value * Plugin.LabelDistance.Value;
        Vector3 cameraPosition = camera.transform.position;
        Player hoveredPlayer = picker.Hovered;
        int colorVersion = BotKinds.ColorVersion & 0xFFFF;
        List<Player> players = world.AllAlivePlayersList;

        for (int i = 0; i < players.Count; i++)
        {
            Player player = players[i];
            if (player == null || player.IsYourPlayer || player == world.MainPlayer)
            {
                continue;
            }

            Vector3 position = player.Position;
            if ((position - cameraPosition).sqrMagnitude > maxSqr || !CursorPicker.ToScreen(camera, position + Vector3.up * 2.1f, out Vector2 screen))
            {
                continue;
            }

            bool selected = picker.IsSelected(player);
            bool hovered = player == hoveredPlayer;
            bool ordered = Commands.HasOrder(player);
            bool ours = Commands.IsOurs(player);
            int health = selected || hovered ? HealthPercent(player) : 127;

            int key = (selected ? 1 : 0) | (hovered ? 2 : 0) | (ordered ? 4 : 0) | (ours ? 8 : 0) | (health << 4) | (colorVersion << 12);
            if (!_labels.TryGetValue(player, out LabelCache cache))
            {
                cache = new LabelCache();
                _labels[player] = cache;
            }
            if (cache.Key != key)
            {
                cache.Key = key;
                cache.Text = BuildLabel(player, selected, hovered, ordered, ours, health);
            }

            GUI.Label(new Rect(screen.x - 150f, screen.y - 22f, 300f, 22f), cache.Text, _label);
        }
    }

    private static readonly Color ReturnColor = new(1f, 0.2f, 1f, 1f);
    private static Texture2D _dot;

    private void DrawReturnPoint(Camera camera)
    {
        if (_mode.Controlling)
        {
            return;
        }
        _dot ??= Dot(32);
        bool moving = _mode.ReturnArmed;
        if (CursorPicker.ToScreen(camera, _mode.ReturnPoint, out Vector2 screen))
        {
            DrawDot(screen, moving ? 0.45f : 1f, moving ? null : "you return here");
        }
        CursorPicker picker = _mode.Picker;
        if (moving && picker.HasNavPoint && CursorPicker.ToScreen(camera, picker.NavPoint, out Vector2 preview))
        {
            DrawDot(preview, 1f, "return here");
        }
    }

    private void DrawDot(Vector2 screen, float alpha, string caption)
    {
        const float size = 18f;
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f * alpha);
        GUI.DrawTexture(new Rect(screen.x - size / 2f - 2f, screen.y - size / 2f - 2f, size + 4f, size + 4f), _dot);
        GUI.color = new Color(ReturnColor.r, ReturnColor.g, ReturnColor.b, alpha);
        GUI.DrawTexture(new Rect(screen.x - size / 2f, screen.y - size / 2f, size, size), _dot);
        GUI.color = previous;
        if (caption != null)
        {
            GUI.Label(new Rect(screen.x - 60f, screen.y + size / 2f + 2f, 120f, 20f), $"<color=#FF33FF>{caption}</color>", _label);
        }
    }

    private static Texture2D Dot(int size)
    {
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
        float radius = size / 2f;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance));
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private string BuildLabel(Player player, bool selected, bool hovered, bool ordered, bool ours, int health)
    {
        StringBuilder b = _builder.Clear();
        bool emphasised = selected || hovered;
        if (emphasised)
        {
            b.Append("<b>");
        }
        b.Append("<color=").Append(BotKinds.Hex(BotKinds.Classify(player))).Append('>');
        if (selected)
        {
            b.Append("▶ ");
        }
        if (ordered)
        {
            b.Append("» ");
        }
        if (ours)
        {
            b.Append("★ ");
        }
        AppendDescription(b, player);
        b.Append("</color>");
        if (emphasised)
        {
            b.Append("</b>");
            if (health <= 100)
            {
                b.Append("  ").Append(Theme.Percent(health));
            }
        }
        return b.ToString();
    }

    private void PruneLabels()
    {
        _labelGone.Clear();
        foreach (KeyValuePair<Player, LabelCache> entry in _labels)
        {
            Player player = entry.Key;
            if (player == null || player.HealthController == null || !player.HealthController.IsAlive)
            {
                _labelGone.Add(player);
            }
        }
        for (int i = 0; i < _labelGone.Count; i++)
        {
            _labels.Remove(_labelGone[i]);
        }
        _labelGone.Clear();
    }

    private static void AppendDescription(StringBuilder b, Player player)
    {
        Profile profile = player.Profile;
        if (profile == null)
        {
            b.Append('?');
            return;
        }

        if (!Bots.IsBot(player))
        {
            b.Append(profile.Nickname).Append(player.IsYourPlayer || player == Singleton<GameWorld>.Instance?.MainPlayer ? " (you)" : " (player)");
            return;
        }

        b.Append(SpawnNames.Of(profile.Info.Settings.Role)).Append(" · ").Append(profile.Nickname);
    }


    private static int HealthPercent(Player player)
    {
        IHealthController health = player.HealthController;
        if (health == null)
        {
            return 127;
        }

        ValueStruct common = health.GetBodyPartHealth(EBodyPart.Common);
        return common.Maximum > 0f ? Mathf.Clamp(Mathf.RoundToInt(common.Current / common.Maximum * 100f), 0, 100) : 127;
    }


    private static void DrawSelectionBrackets(Camera camera, CursorPicker picker)
    {
        Color tint = Plugin.SelectionColor.Value;
        IReadOnlyList<Player> selection = picker.Selection;
        for (int i = 0; i < selection.Count; i++)
        {
            DrawBrackets(camera, selection[i], tint, 2f);
        }

        Player hovered = picker.Hovered;
        if (hovered != null && !picker.IsSelected(hovered))
        {
            DrawBrackets(camera, hovered, new Color(1f, 1f, 1f, 0.55f), 1f);
        }
    }

    private static void DrawBrackets(Camera camera, Player player, Color color, float thickness)
    {
        if (player == null || player.HealthController == null || !player.HealthController.IsAlive)
        {
            return;
        }

        Vector3 feet = player.Position;
        if (!CursorPicker.ToScreen(camera, feet, out Vector2 bottom) || !CursorPicker.ToScreen(camera, feet + Vector3.up * 1.85f, out Vector2 top))
        {
            return;
        }

        float height = Mathf.Max(bottom.y - top.y, 16f);
        float width = Mathf.Max(height * 0.55f, 12f);
        float centerX = (bottom.x + top.x) * 0.5f;
        Rect rect = new(centerX - width * 0.5f, bottom.y - height, width, height);
        rect = new Rect(rect.x - 4f, rect.y - 4f, rect.width + 8f, rect.height + 8f);
        float arm = Mathf.Clamp(Mathf.Min(rect.width, rect.height) * 0.3f, 5f, 24f);

        Brackets(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), arm + 1f, thickness + 2f, new Color(0f, 0f, 0f, color.a * 0.7f));
        Brackets(rect, arm, thickness, color);
    }

    private static void Brackets(Rect r, float arm, float t, Color color)
    {
        FillRect(new Rect(r.xMin, r.yMin, arm, t), color);
        FillRect(new Rect(r.xMin, r.yMin, t, arm), color);
        FillRect(new Rect(r.xMax - arm, r.yMin, arm, t), color);
        FillRect(new Rect(r.xMax - t, r.yMin, t, arm), color);
        FillRect(new Rect(r.xMin, r.yMax - t, arm, t), color);
        FillRect(new Rect(r.xMin, r.yMax - arm, t, arm), color);
        FillRect(new Rect(r.xMax - arm, r.yMax - t, arm, t), color);
        FillRect(new Rect(r.xMax - t, r.yMax - arm, t, arm), color);
    }

    private static void FrameRect(Rect r, float t, Color color, float outset)
    {
        r = new Rect(r.x - outset, r.y - outset, r.width + outset * 2f, r.height + outset * 2f);
        FillRect(new Rect(r.xMin, r.yMin, r.width, t), color);
        FillRect(new Rect(r.xMin, r.yMax - t, r.width, t), color);
        FillRect(new Rect(r.xMin, r.yMin, t, r.height), color);
        FillRect(new Rect(r.xMax - t, r.yMin, t, r.height), color);
    }

    private static void FillRect(Rect r, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
        GUI.color = previous;
    }

    private void EnsureStyles()
    {
        if (_label != null)
        {
            return;
        }


        _label = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.LowerCenter,
            richText = true,
            fontSize = 12
        };
        _label.normal.textColor = Color.white;


        _banner = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            richText = true,
            fontSize = 14
        };
        _banner.normal.textColor = new Color(1f, 0.85f, 0.2f);

        _label2 = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperLeft,
            richText = true,
            fontSize = 13,
            fontStyle = FontStyle.Bold
        };
        _label2.normal.textColor = new Color(1f, 0.85f, 0.2f);

        _marker = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 26,
            fontStyle = FontStyle.Bold
        };
        _marker.normal.textColor = new Color(1f, 0.85f, 0.2f);
    }
}
