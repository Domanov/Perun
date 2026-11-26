using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Perun.Client.Arsenal;
using Perun.Client.Compat;
using Perun.Client.Units;

using UnityEngine;
using static Perun.Client.UI.Constants.ArsenalPanel;

namespace Perun.Client.UI;

internal sealed class ArsenalPanel
{
    private const int CrateView = 2;
    private static readonly string[] Views = { ChipLoadouts, ChipItems, ChipCrate };

    private const float RowHeight = 22f;
    private const float SlotRefresh = 0.5f;
    private const int Inventory = -1;

    private static readonly (string label, EquipmentSlot slot)[] Slots =
    {
        (LabelPrimary, EquipmentSlot.FirstPrimaryWeapon),
        (LabelSecondary, EquipmentSlot.SecondPrimaryWeapon),
        (LabelHolster, EquipmentSlot.Holster),
        (LabelMelee, EquipmentSlot.Scabbard),
        (LabelHeadwear, EquipmentSlot.Headwear),
        (LabelEarpiece, EquipmentSlot.Earpiece),
        (LabelFaceCover, EquipmentSlot.FaceCover),
        (LabelEyewear, EquipmentSlot.Eyewear),
        (LabelArmband, EquipmentSlot.ArmBand),
        (LabelBodyArmor, EquipmentSlot.ArmorVest),
        (LabelRig, EquipmentSlot.TacticalVest),
        (LabelBackpack, EquipmentSlot.Backpack),
        (LabelPockets, EquipmentSlot.Pockets),
        (LabelSecure, EquipmentSlot.SecuredContainer)
    };

    private readonly VirtualList _list = new(RowHeight);
    private readonly List<ArsenalEntry> _shown = new();
    private readonly string[] _slotText = new string[Slots.Length];
    private readonly bool[] _slotFilled = new bool[Slots.Length];
    private readonly Item[] _slotItem = new Item[Slots.Length];
    private int _labelTarget = int.MinValue;

    private Player _unit;
    private string _unitTitle = "";
    private int _target = Inventory;
    private int _pendingTarget = int.MinValue;
    private int _category;
    private int _pendingCategory = -1;
    private string _search = "";
    private string _activeSearch = "";
    private bool _dirty = true;
    private float _nextSlots;
    private Vector2 _scroll;
    private bool _busy;
    private string _status = "";
    private string _targetLabel = "";

    private int _view;
    private int _pendingView = -1;
    private readonly List<Player> _units = new();
    private SavedLoadout[] _savedRows = Array.Empty<SavedLoadout>();
    private Vector2 _savedScroll;
    private string _saveName = "";
    private string _loadoutTitle = "";
    private string _copyLabel = "";
    private string _pasteLabel = "";
    private string _clipboardLabel = "";
    private string _kitLabel = "";
    private string _applyLabel = "";
    private string _progress = "";
    private int _labelUnits = -1;
    private Loadout _labelClipboard;
    private WildSpawnType? _labelRole;
    private WildSpawnType? _role;
    private string _roleName = "";
    private Action _pendingAction;

    public void DrawContent(IReadOnlyList<Player> selection, WildSpawnType? role, string roleName)
    {
        selection = selection?.Where(player => !Bots.IsProtected(player)).ToList();
        Event current = Event.current;
        if (current.type == EventType.Layout)
        {
            ApplyPending(selection != null && selection.Count == 1 ? selection[0] : null);
            ApplyLoadoutPending(selection, role, roleName);
        }

        if (Commands.IsRemote || FikaInfo.Present)
        {
            GUILayout.Label(LabelArsenalHeader, Theme.Header);
            GUILayout.Label(LabelSinglePlayerOnly, Theme.Label);
            return;
        }

        int view = GUILayout.Toolbar(_view, Views, Theme.Tab);
        if (view != _view && current.type != EventType.Layout)
        {
            _pendingView = view;
        }
        GUILayout.Space(4f);

        if (_view == 0)
        {
            DrawLoadouts();
        }
        else if (_view == CrateView)
        {
            DrawCrate();
        }
        else
        {
            DrawItems();
        }
    }

    private void DrawLoadouts()
    {
        bool idle = !_busy;
        int count = _units.Count;
        GUILayout.Label(_loadoutTitle, Theme.Header);

        GUILayout.BeginHorizontal();
        GUI.enabled = idle && count == 1;
        if (GUILayout.Button(_copyLabel, Theme.Button))
        {
            _pendingAction = CopySelected;
        }
        GUI.enabled = idle && count > 0 && Loadouts.Clipboard != null;
        if (GUILayout.Button(_pasteLabel, Theme.Button))
        {
            Loadout clipboard = Loadouts.Clipboard;
            _pendingAction = () => ApplyToUnits(clipboard);
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        GUILayout.Label(_clipboardLabel, Theme.Small);

        GUILayout.BeginHorizontal();
        GUI.SetNextControlName(ControlName);
        _saveName = GUILayout.TextField(_saveName ?? "", 32, Theme.Search);
        GUI.enabled = idle && Loadouts.Clipboard != null;
        if (GUILayout.Button(ChipSaveCopied, Theme.Button, GUILayout.Width(96f)))
        {
            string name = _saveName;
            _pendingAction = () => SaveClipboard(name);
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.Space(4f);
        GUILayout.Label(LabelSavedLoadouts, Theme.Header);
        _savedScroll = GUILayout.BeginScrollView(_savedScroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
        for (int i = 0; i < _savedRows.Length; i++)
        {
            SavedLoadout saved = _savedRows[i];
            GUILayout.BeginHorizontal();
            GUILayout.Label(saved.Name, Theme.Row, GUILayout.Height(RowHeight));
            GUI.enabled = idle && count > 0;
            if (GUILayout.Button(_applyLabel, Theme.Button, GUILayout.Width(84f), GUILayout.Height(RowHeight)))
            {
                _pendingAction = () => ApplyToUnits(Loadouts.From(saved));
            }
            GUI.enabled = idle;
            if (GUILayout.Button("✕", Theme.Button, GUILayout.Width(26f), GUILayout.Height(RowHeight)))
            {
                _pendingAction = () => DeleteSaved(saved);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
        if (_savedRows.Length == 0)
        {
            GUILayout.Label(LabelNothingSavedYet, Theme.Small);
        }
        GUILayout.EndScrollView();

        GUILayout.Space(4f);
        GUILayout.Label(LabelRandomKitFrom, Theme.Header);
        GUI.enabled = idle && count > 0 && _role.HasValue;
        if (GUILayout.Button(_kitLabel, Theme.Button))
        {
            WildSpawnType kitRole = _role.Value;
            string kitName = _roleName;
            _pendingAction = () => ApplyKit(kitRole, kitName);
        }
        GUI.enabled = true;

        GUILayout.Label(_busy ? string.Format(LabelWorking, _progress) : _status, Theme.Label);
    }

    private void ApplyLoadoutPending(IReadOnlyList<Player> selection, WildSpawnType? role, string roleName)
    {
        if (_pendingView >= 0)
        {
            _view = _pendingView;
            _pendingView = -1;
            _scroll = Vector2.zero;
            _dirty = true;
        }

        if (_crateHintKey != Plugin.CrateKey.Value)
        {
            _crateHintKey = Plugin.CrateKey.Value;
            _crateHint = string.Format(LabelCrateHint, Plugin.ControlKey.Value, _crateHintKey);
        }

        if (_view == CrateView && Time.unscaledTime >= _nextCrate)
        {
            _nextCrate = Time.unscaledTime + CrateRefresh;
            RefreshCrate();
            if (_crateCount != _crateItems.Count)
            {
                _crateCount = _crateItems.Count;
                _crateTitle = string.Format(LabelCrateHeader, _crateCount);
            }
        }

        Action action = _pendingAction;
        _pendingAction = null;
        action?.Invoke();

        _units.Clear();
        if (selection != null)
        {
            foreach (Player player in selection)
            {
                if (player != null && player.HealthController != null && player.HealthController.IsAlive)
                {
                    _units.Add(player);
                }
            }
        }

        if (_savedRows.Length != Loadouts.Saved.Count || (_savedRows.Length > 0 && !ReferenceEquals(_savedRows[0], Loadouts.Saved[0])))
        {
            _savedRows = Loadouts.Saved.ToArray();
        }

        _role = role;
        _roleName = roleName ?? "";
        if (_labelUnits == _units.Count && ReferenceEquals(_labelClipboard, Loadouts.Clipboard) && _labelRole == role)
        {
            return;
        }
        _labelUnits = _units.Count;
        _labelClipboard = Loadouts.Clipboard;
        _labelRole = role;
        int count = _units.Count;
        string units = count == 1 ? LabelOneUnit : string.Format(LabelUnits, count);
        _loadoutTitle = count == 0 ? LabelLoadoutsSelectUnits : string.Format(LabelLoadoutsSelected, units);
        _copyLabel = count == 1 ? string.Format(ChipCopy, _units[0].Profile?.Nickname) : ChipCopySelectOne;
        _pasteLabel = string.Format(ChipPasteTo, units);
        _applyLabel = count > 1 ? string.Format(ChipApplyCount, count) : ChipApply;
        _clipboardLabel = Loadouts.Clipboard != null ? string.Format(LabelCopiedSlots, Loadouts.Clipboard.Name, Loadouts.Clipboard.Slots.Count) : LabelNothingCopiedYet;
        _kitLabel = role.HasValue ? string.Format(ChipGiveRandomKit, units, _roleName) : ChipPickRole;
    }

    private void CopySelected()
    {
        if (_units.Count != 1)
        {
            return;
        }
        Player unit = _units[0];
        Loadout loadout = Loadouts.Capture(unit, string.Format(LabelUnitLoadout, unit.Profile?.Nickname));
        if (loadout == null)
        {
            _status = LabelCouldNotReadGear;
            return;
        }
        Loadouts.Clipboard = loadout;
        if (string.IsNullOrWhiteSpace(_saveName))
        {
            _saveName = unit.Profile?.Nickname ?? "";
        }
        _status = string.Format(LabelCopiedLoadout, unit.Profile?.Nickname);
    }

    private void SaveClipboard(string name)
    {
        if (Loadouts.Clipboard == null)
        {
            return;
        }
        SavedLoadout saved = Loadouts.Save(Loadouts.Clipboard, name);
        _status = string.Format(LabelSaved, saved.Name);
    }

    private void DeleteSaved(SavedLoadout saved)
    {
        Loadouts.Delete(saved);
        _status = string.Format(LabelDeleted, saved.Name);
    }

    private void ApplyToUnits(Loadout loadout)
    {
        if (loadout != null)
        {
            RunOnUnits(loadout.Name, null, _ => Task.FromResult<(Func<int, Loadout>, string)>((_ => loadout, null)));
        }
    }

    private void ApplyKit(WildSpawnType role, string roleName)
    {
        string what = string.Format(LabelKit, roleName);
        RunOnUnits(what, string.Format(LabelAskingKits, roleName), async count =>
        {
            List<Profile> profiles = await SpawnService.FetchProfiles(role, count);
            if (profiles.Count == 0)
            {
                return (null, LabelNoKits);
            }
            return (i => Loadouts.Capture(profiles[i % profiles.Count].Inventory.Equipment, what), null);
        });
    }

    private async void RunOnUnits(string what, string preparing, Func<int, Task<(Func<int, Loadout> pick, string error)>> prepare)
    {
        if (_busy || _units.Count == 0)
        {
            return;
        }
        List<Player> units = new(_units);
        _busy = true;
        _progress = preparing ?? "";
        int done = 0;
        string lastError = null;
        List<string> rollbacks = new();
        try
        {
            (Func<int, Loadout> pick, string error) = await prepare(units.Count);
            lastError = error;
            for (int i = 0; pick != null && i < units.Count; i++)
            {
                string position = $"{i + 1}/{units.Count}";
                _progress = position;
                ArsenalResult result = await Loadouts.Apply(units[i], pick(i), step => _progress = $"{position} {step}");
                if (result.IsOk)
                {
                    done++;
                }
                else if (result.Status == ArsenalStatus.RollbackFailed)
                {
                    rollbacks.Add($"{units[i].Profile?.Nickname}: {result.Message}");
                }
                else
                {
                    lastError = $"{units[i].Profile?.Nickname}: {result.Message}";
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Applying {what} failed: {ex}");
            lastError = LabelFailedSeeLog;
        }
        Finish(done, units.Count, what, lastError);
        if (rollbacks.Count > 0)
        {
            _status = string.Format(LabelAppliedNotRestored, what, done, units.Count, string.Join("; ", rollbacks));
        }
    }

    private void Finish(int done, int total, string what, string error)
    {
        _busy = false;
        _progress = "";
        _nextSlots = 0f;
        string summary = string.Format(LabelAppliedOn, what, done, total);
        Plugin.Log.LogInfo($"Arsenal: {summary}{(error != null ? $"; {error}" : "")}");
        _status = error == null ? string.Format(LabelApplied, summary) : string.Format(LabelAppliedWithProblems, summary, error);
    }

    private void DrawItems()
    {
        Event current = Event.current;
        if (_unit == null)
        {
            GUILayout.Label(LabelArsenalHeader, Theme.Header);
            GUILayout.Label(LabelSelectOneUnit, Theme.Label);
            return;
        }

        GUILayout.Label(_unitTitle, Theme.Header);
        for (int i = 0; i < Slots.Length; i++)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_slotText[i], _target == i ? Theme.RowSelected : Theme.Row, GUILayout.Height(RowHeight)))
            {
                _pendingTarget = _target == i ? Inventory : i;
            }
            GUI.enabled = _slotFilled[i] && !_busy;
            if (GUILayout.Button("✕", Theme.Button, GUILayout.Width(26f), GUILayout.Height(RowHeight)))
            {
                Clear(i);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(4f);
        GUILayout.Label(_targetLabel, Theme.Header);
        DrawCatalogue(Event.current, false);
        GUILayout.Label(_busy ? LabelWorkingPlain : _status, Theme.Label);
    }

    private void DrawCatalogue(Event current, bool crate)
    {
        GUI.enabled = crate || _target == Inventory;
        int category = GUILayout.SelectionGrid(_category, ArsenalCatalog.CategoryNames, 4, Theme.Tab);
        GUI.enabled = true;
        if (category != _category && current.type != EventType.Layout)
        {
            _pendingCategory = category;
        }

        GUI.SetNextControlName(ControlSearch);
        _search = GUILayout.TextField(_search ?? "", 40, Theme.Search);
        if (string.IsNullOrEmpty(_search) && GUI.GetNameOfFocusedControl() != ControlSearch && current.type == EventType.Repaint)
        {
            Rect field = GUILayoutUtility.GetLastRect();
            GUI.Label(new Rect(field.x + 6f, field.y + 3f, field.width, field.height), LabelSearchItems, Theme.Small);
        }

        _list.Plan(_shown.Count, _scroll);
        _scroll = GUILayout.BeginScrollView(_scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
        _list.SpaceBefore();
        for (int i = _list.First; i < _list.Last; i++)
        {
            if (GUILayout.Button(_shown[i].Name, Theme.Row, GUILayout.Height(RowHeight)) && !_busy)
            {
                if (crate)
                {
                    AddToCrate(_shown[i]);
                }
                else
                {
                    Give(_shown[i]);
                }
            }
        }
        _list.SpaceAfter();
        if (_shown.Count == 0)
        {
            GUILayout.Label(LabelNothingMatches, Theme.Small);
        }
        GUILayout.EndScrollView();
        _list.MeasureView();

    }

    private const float CrateRefresh = 0.5f;
    private readonly List<Item> _crateItems = new();
    private readonly List<string> _crateLabels = new();
    private float _nextCrate;
    private Vector2 _crateScroll;
    private string _crateTitle = "";
    private string _crateHint = "";
    private KeyCode _crateHintKey = KeyCode.None;
    private int _crateCount = -1;

    private void DrawCrate()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(_crateTitle, Theme.Header);
        GUI.enabled = !_busy && _crateCount > 0 && !ArsenalCrate.IsOpen;
        if (GUILayout.Button(ChipEmpty, Theme.Button, GUILayout.Width(64f)))
        {
            _pendingAction = () =>
            {
                ArsenalCrate.Empty();
                _nextCrate = 0f;
                _status = LabelCrateEmptied;
            };
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        GUILayout.Label(_crateHint, Theme.Small);

        if (_crateItems.Count > 0)
        {
            _crateScroll = GUILayout.BeginScrollView(_crateScroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(Mathf.Min(_crateItems.Count, 6) * (RowHeight + 2f) + 4f));
            for (int i = 0; i < _crateItems.Count; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(_crateLabels[i], Theme.Row, GUILayout.Height(RowHeight));
                GUI.enabled = !_busy && !ArsenalCrate.IsOpen;
                if (GUILayout.Button("✕", Theme.Button, GUILayout.Width(26f), GUILayout.Height(RowHeight)))
                {
                    Item item = _crateItems[i];
                    _pendingAction = () =>
                    {
                        ArsenalCrate.Remove(item);
                        _nextCrate = 0f;
                    };
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        GUILayout.Space(4f);
        GUILayout.Label(LabelAddToCrate, Theme.Header);
        DrawCatalogue(Event.current, true);
        GUILayout.Label(_busy ? LabelWorkingPlain : _status, Theme.Label);
    }

    private void RefreshCrate()
    {
        ArsenalCrate.Snapshot(_crateItems);
        while (_crateLabels.Count < _crateItems.Count)
        {
            _crateLabels.Add("");
        }
        if (_crateLabels.Count > _crateItems.Count)
        {
            _crateLabels.RemoveRange(_crateItems.Count, _crateLabels.Count - _crateItems.Count);
        }
        for (int i = 0; i < _crateItems.Count; i++)
        {
            Item item = _crateItems[i];
            string name = item.ShortName.Localized();
            _crateLabels[i] = item.StackObjectsCount > 1 ? $"{name}  <color=#9fb4c8>×{item.StackObjectsCount}</color>" : name;
        }
    }

    private async void AddToCrate(ArsenalEntry entry)
    {
        Item item = ArsenalOps.Build(entry.TemplateId, int.MaxValue);
        if (item == null)
        {
            _status = string.Format(LabelCannotCreate, entry.Name);
            return;
        }
        _busy = true;
        ArsenalResult result;
        try
        {
            result = await ArsenalCrate.Add(item);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Arsenal crate add failed: {ex}");
            result = ArsenalResult.Failed(LabelFailedSeeLog);
        }
        finally
        {
            _busy = false;
            _nextCrate = 0f;
        }
        _status = Describe(result, string.Format(LabelAddedToCrate, entry.Name));
    }

    private void ApplyPending(Player unit)
    {
        if (unit != null && (unit.HealthController == null || !unit.HealthController.IsAlive))
        {
            unit = null;
        }

        if (!ReferenceEquals(unit, _unit))
        {
            _unit = unit;
            _unitTitle = unit != null ? string.Format(LabelArsenalUnitHeader, unit.Profile?.Nickname) : "";
            System.Array.Clear(_slotText, 0, _slotText.Length);
            System.Array.Clear(_slotItem, 0, _slotItem.Length);
            _status = "";
            _dirty = true;
            _nextSlots = 0f;
        }

        if (_pendingTarget != int.MinValue)
        {
            _target = _pendingTarget;
            _pendingTarget = int.MinValue;
            _scroll = Vector2.zero;
            _dirty = true;
        }

        if (_pendingCategory >= 0)
        {
            _category = _pendingCategory;
            _pendingCategory = -1;
            _scroll = Vector2.zero;
            _dirty = true;
        }

        if (_activeSearch != _search)
        {
            _activeSearch = _search;
            _scroll = Vector2.zero;
            _dirty = true;
        }

        if (_unit != null && Time.unscaledTime >= _nextSlots)
        {
            _nextSlots = Time.unscaledTime + SlotRefresh;
            RefreshSlots();
        }

        if (_dirty)
        {
            _dirty = false;
            Filter();
        }
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < Slots.Length; i++)
        {
            Item item = SlotOf(Slots[i].slot)?.ContainedItem;
            if (_slotText[i] != null && ReferenceEquals(item, _slotItem[i]))
            {
                continue;
            }
            _slotItem[i] = item;
            _slotFilled[i] = item != null;
            string name = item != null ? item.ShortName.Localized() : LabelEmptySlot;
            _slotText[i] = $"<color=#9fb4c8>{Slots[i].label}</color>  {name}";
        }

        if (_labelTarget != _target)
        {
            _labelTarget = _target;
            _targetLabel = _target == Inventory
                ? LabelAddToInventory
                : string.Format(LabelPutIn, Slots[_target].label.ToUpperInvariant());
        }
    }

    private Slot SlotOf(EquipmentSlot slot) => _unit?.InventoryController?.Inventory?.Equipment?.GetSlot(slot);

    private void Filter()
    {
        _shown.Clear();
        bool crate = _view == CrateView;
        if (_unit == null && !crate)
        {
            return;
        }

        bool anywhere = crate || _target == Inventory;
        ItemFilter[] filters = anywhere ? null : SlotOf(Slots[_target].slot)?.Filters;
        string needle = _activeSearch.Trim();
        bool searching = needle.Length > 0;
        IReadOnlyList<ArsenalEntry> entries = ArsenalCatalog.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            ArsenalEntry entry = entries[i];
            if (anywhere)
            {
                if (!searching && entry.Category != _category)
                {
                    continue;
                }
            }
            else if (!ArsenalCatalog.Fits(filters, entry.Template))
            {
                continue;
            }

            if (searching && entry.Search.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }
            _shown.Add(entry);
        }
        RefreshSlots();
    }

    private void Give(ArsenalEntry entry)
    {
        Player unit = _unit;
        Item item = ArsenalOps.Build(entry.TemplateId, int.MaxValue);
        if (item == null)
        {
            _status = string.Format(LabelCannotCreate, entry.Name);
            return;
        }

        EquipmentSlot? slot = _target == Inventory ? null : Slots[_target].slot;
        Run(() => ArsenalOps.GiveAsync(unit, item, slot), string.Format(LabelGave, entry.Name));
    }

    private void Clear(int index)
    {
        Player unit = _unit;
        EquipmentSlot slot = Slots[index].slot;
        Run(() => ArsenalOps.ClearSlotAsync(unit, slot), string.Format(LabelCleared, Slots[index].label));
    }

    private async void Run(Func<Task<ArsenalResult>> work, string success)
    {
        _busy = true;
        ArsenalResult result;
        try
        {
            result = await work();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Arsenal action failed: {ex}");
            result = ArsenalResult.Failed(LabelFailedSeeLog);
        }
        finally
        {
            _busy = false;
            _nextSlots = 0f;
        }
        _status = Describe(result, success);
    }

    private static string Describe(ArsenalResult result, string success) => result.Status switch
    {
        ArsenalStatus.Ok => $"<color=#8be38b>{success}</color>",
        ArsenalStatus.Busy => $"<color=#ffc847>{result.Message}</color>",
        ArsenalStatus.Cancelled => $"<color=#9fb4c8>{result.Message}</color>",
        ArsenalStatus.RollbackFailed => $"<b><color=#ff5a4a>{result.Message}</color></b>",
        _ => $"<color=#ff8a7a>{result.Message}</color>"
    };
}
