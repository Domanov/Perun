using System;
using System.Collections.Generic;
using System.Text;
using Comfort.Common;
using EFT;
using Perun.Client.Mode;
using Perun.Client.Orders;
using Perun.Client.Units;
using UnityEngine;

namespace Perun.Client.UI;

internal sealed class EntityPanel
{
    private const float Refresh = 0.5f;
    private const float DoubleClick = 0.35f;

    private sealed class Entry
    {
        public Player Player;
        public string Text;
        public int Key = -1;
    }

    private static readonly BotKind[] Order = (BotKind[])Enum.GetValues(typeof(BotKind));
    private static readonly string[] KindNames = { "Players", "USEC", "BEAR", "Scavs", "Raiders & rogues", "Bosses", "Followers", "Cultists", "Infected", "Factions", "Other" };

    private const float RowHeight = 22f;

    private struct Row
    {
        public int Group;
        public Entry Entry;
    }

    private readonly List<Entry>[] _groups = new List<Entry>[Order.Length];
    private readonly List<Row> _rows = new();
    private readonly VirtualList _list = new(RowHeight);
    private readonly string[] _groupLabels = new string[Order.Length];
    private readonly bool[] _collapsed = new bool[Order.Length];
    private readonly Dictionary<Player, Entry> _entries = new();
    private readonly StringBuilder _builder = new(128);
    private readonly List<Player> _gone = new();
    private Vector2 _scroll;
    private float _nextRefresh;
    private bool _oursOnly;
    private bool _oursOnlyWanted;
    private int _toggleGroup = -1;
    private int _total;
    private int _headerTotal = -1;
    private string _header = "";
    private Player _lastClicked;
    private float _lastClickAt;
    private GameWorld _world;

    public EntityPanel()
    {
        for (int i = 0; i < _groups.Length; i++)
        {
            _groups[i] = new List<Entry>();
        }
    }

    public void DrawContent(PerunMode mode)
    {
        if (Event.current.type == EventType.Layout)
        {
            if (_toggleGroup >= 0)
            {
                _collapsed[_toggleGroup] = !_collapsed[_toggleGroup];
                _toggleGroup = -1;
                _nextRefresh = 0f;
            }
            if (_oursOnlyWanted != _oursOnly)
            {
                _oursOnly = _oursOnlyWanted;
                _nextRefresh = 0f;
            }
            Rebuild(mode);
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label(_header, Theme.Header, GUILayout.ExpandWidth(true));
        _oursOnlyWanted = GUILayout.Toggle(_oursOnlyWanted, "★ Mine", Theme.Tab, GUILayout.Width(64f));
        GUILayout.EndHorizontal();

        _list.Plan(_rows.Count, _scroll);
        _scroll = GUILayout.BeginScrollView(_scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
        _list.SpaceBefore();
        for (int i = _list.First; i < _list.Last; i++)
        {
            Row row = _rows[i];
            if (row.Entry == null)
            {
                if (GUILayout.Button(_groupLabels[row.Group], Theme.Group, GUILayout.Height(RowHeight)))
                {
                    if (Event.current.shift)
                    {
                        SelectGroup(mode, _groups[row.Group]);
                    }
                    else
                    {
                        _toggleGroup = row.Group;
                    }
                }
                continue;
            }

            Entry entry = row.Entry;
            if (GUILayout.Button(entry.Text, mode.Picker.IsSelected(entry.Player) ? Theme.RowSelected : Theme.Row, GUILayout.Height(RowHeight)))
            {
                Clicked(mode, entry.Player);
            }
        }
        _list.SpaceAfter();
        if (_total == 0)
        {
            GUILayout.Label(_oursOnly ? "No Perun units yet." : "Nobody here.", Theme.Small);
        }
        GUILayout.EndScrollView();
        _list.MeasureView();
        GUILayout.Label("Click select · Shift add · double-click fly to · Shift+click group selects all", Theme.Small);
    }

    private void Clicked(PerunMode mode, Player player)
    {
        if (player == null)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (player == _lastClicked && now - _lastClickAt < DoubleClick)
        {
            mode.FlyTo(player);
            _lastClicked = null;
            return;
        }

        _lastClicked = player;
        _lastClickAt = now;
        if (Event.current.shift)
        {
            mode.Picker.Toggle(player);
        }
        else
        {
            mode.Picker.SelectOnly(player);
        }
    }

    private static void SelectGroup(PerunMode mode, List<Entry> group)
    {
        mode.Picker.ClearSelection();
        for (int i = 0; i < group.Count; i++)
        {
            Player player = group[i].Player;
            if (Bots.IsBot(player))
            {
                mode.Picker.Add(player);
            }
        }
    }

    private void Rebuild(PerunMode mode)
    {
        float now = Time.unscaledTime;
        if (now < _nextRefresh)
        {
            return;
        }
        _nextRefresh = now + Refresh;

        GameWorld world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
        if (world != _world)
        {
            _world = world;
            _entries.Clear();
        }

        for (int i = 0; i < _groups.Length; i++)
        {
            _groups[i].Clear();
        }
        _total = 0;

        if (world != null)
        {
            List<Player> players = world.AllAlivePlayersList;
            for (int i = 0; i < players.Count; i++)
            {
                Player player = players[i];
                if (player == null || player.IsYourPlayer || player.HealthController == null || !player.HealthController.IsAlive)
                {
                    continue;
                }

                bool ours = Commands.IsOurs(player);
                if (_oursOnly && !ours)
                {
                    continue;
                }

                BotKind kind = BotKinds.Classify(player);
                Entry entry = Describe(player, kind, ours);
                _groups[(int)kind].Add(entry);
                _total++;
            }
        }

        for (int g = 0; g < _groups.Length; g++)
        {
            List<Entry> group = _groups[g];
            if (group.Count == 0)
            {
                continue;
            }
            group.Sort((a, b) => string.CompareOrdinal(a.Text, b.Text));
            _groupLabels[g] = $"{(_collapsed[g] ? "▸" : "▾")} <color={BotKinds.Hex(Order[g])}>{KindNames[g]}</color>  <color=#7f8a95>{group.Count}</color>";
        }

        if (_headerTotal != _total)
        {
            _headerTotal = _total;
            _header = $"ENTITIES  <color=#9fb4c8>{_total}</color>";
        }

        _rows.Clear();
        for (int g = 0; g < _groups.Length; g++)
        {
            List<Entry> group = _groups[g];
            if (group.Count == 0)
            {
                continue;
            }

            _rows.Add(new Row { Group = g });
            if (_collapsed[g])
            {
                continue;
            }

            for (int i = 0; i < group.Count; i++)
            {
                _rows.Add(new Row { Group = g, Entry = group[i] });
            }
        }

        Prune();
    }

    private Entry Describe(Player player, BotKind kind, bool ours)
    {
        if (!_entries.TryGetValue(player, out Entry entry))
        {
            entry = new Entry { Player = player };
            _entries[player] = entry;
        }

        bool ordered = Commands.HasOrder(player);
        bool paused = WorldPause.Paused;
        int key = (ours ? 1 : 0) | (ordered ? 2 : 0) | (paused ? 4 : 0) | ((int)kind << 3) | (BotKinds.ColorVersion << 8);
        if (entry.Key == key)
        {
            return entry;
        }

        entry.Key = key;
        StringBuilder b = _builder.Clear();
        if (ours)
        {
            b.Append("<color=#ffc847>★</color> ");
        }
        Profile profile = player.Profile;
        b.Append("<color=").Append(BotKinds.Hex(kind)).Append('>').Append(profile?.Nickname ?? "?").Append("</color>");
        if (profile != null && Bots.IsBot(player))
        {
            b.Append("  <color=#7f8a95>").Append(SpawnNames.Of(profile.Info.Settings.Role)).Append("</color>");
        }
        if (ordered)
        {
            b.Append("  <color=#ffc847>»</color>");
        }
        entry.Text = b.ToString();
        return entry;
    }

    private void Prune()
    {
        _gone.Clear();
        foreach (KeyValuePair<Player, Entry> pair in _entries)
        {
            Player player = pair.Key;
            if (player == null || player.HealthController == null || !player.HealthController.IsAlive)
            {
                _gone.Add(player);
            }
        }
        for (int i = 0; i < _gone.Count; i++)
        {
            _entries.Remove(_gone[i]);
        }
        _gone.Clear();
    }
}
