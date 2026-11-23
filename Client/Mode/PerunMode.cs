using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.CameraControl;
using EFT.UI;
using Perun.Client.Arsenal;
using Perun.Client.View;
using Perun.Client.Compat;
using Perun.Client.UI;
using Perun.Client.Orders;
using Perun.Client.Units;
using UnityEngine;

namespace Perun.Client.Mode;

internal sealed class PerunMode
{
    private const float GraceSeconds = 1.5f;

    public static bool OwnsCamera { get; private set; }

    public bool Active { get; private set; }
    public RtsCamera Camera { get; } = new();

    public Vector3 ReturnPoint => _parking.SavedPosition;

    public bool IsParked(Player player) => player != null && ReferenceEquals(_parking.Body, player);
    public CursorPicker Picker { get; } = new();
    public bool Controlling => Active && _takeover.Active;
    public Player ControlledBot => _takeover.Bot;
    public string Message { get; private set; }
    public float MessageUntil { get; private set; }

    private readonly BodyParking _parking = new();
    private readonly SmoothieSettings _smoothie = new();
    private readonly GameCulling _culling = new();
    private readonly Takeover _takeover = new();

    private Player _player;
    private GamePlayerOwner _owner;
    private Transform _cameraParent;
    private UnityEngine.Camera _camera;
    private float _graceUntil = -1f;
    private bool _nightVisionWasOn;
    private bool _thermalWasOn;
    private bool _exitRequested;
    private bool _claimPending;
    private bool _controlRequested;
    private float _cullingRefreshAt;
    private Task _handsLoad;
    private Player _handsFor;
    private float _claimUntil;
    private bool _teleportArmed;
    private bool _returnArmed;
    private Player _settling;
    private readonly CameraFlight _flight = new();
    private bool _flyingBack;
    private Player _finishWhenArrived;
    private bool _finalApproach;
    private const float FinalApproach = 0.12f;
    private float _settleUntil;
    private float _settleFrom;
    private readonly (KeyCode key, bool needsCtrl, Action<bool> action)[] _hotkeys;

    public Player Player => _player;

    public UnityEngine.Camera ViewCamera => _camera;

    public PerunMode()
    {
        _hotkeys = new (KeyCode, bool, Action<bool>)[]
        {
            (KeyCode.F, true, _ => FocusSelected()),
            (KeyCode.T, false, _ => Hud?.ToggleTab(AssetTab.Environment)),
            (KeyCode.N, false, _ => RtsVision.Cycle()),
            (KeyCode.B, false, _ => Hud?.ToggleTab(AssetTab.Units)),
            (KeyCode.I, false, _ => Hud?.ToggleTab(AssetTab.Arsenal)),
            (KeyCode.P, false, _ => TogglePause()),
            (KeyCode.Delete, false, RemoveSelection),
            (KeyCode.X, false, _ => HaltSelection()),
            (KeyCode.H, false, _ => ToggleHud()),
            (KeyCode.F1, false, _ => ToggleHelp()),
            (KeyCode.L, false, _ => ToggleLock()),
            (KeyCode.Home, false, _ => BackToBody())
        };

        Picker.PlaceHandler = PlaceUnits;
        Camera.WheelHandler = SpreadGroup;
        Picker.CancelHandler = CancelPlacing;
        Picker.IsPlacing = IsPlacing;
        Picker.ScreenClickHandler = ClickOnScreen;
        Picker.OrderHandler = IssueOrder;
    }

    private void ToggleHud()
    {
        if (Hud != null)
        {
            Hud.Hidden = !Hud.Hidden;
        }
    }

    private void ToggleHelp()
    {
        if (Hud != null)
        {
            Hud.HelpVisible = !Hud.HelpVisible;
        }
    }

    private bool IsPlacing() => _teleportArmed || _returnArmed || (Hud != null && Hud.Spawn.Armed);

    public float GroupSpacing { get; private set; } = SpawnService.DefaultGroupSpacing;

    private bool SpreadGroup(float wheel)
    {
        if (Hud?.Spawn.Group == null || !(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            return false;
        }
        GroupSpacing = Mathf.Clamp(Mathf.Round((GroupSpacing + wheel * 0.1f) * 10f) / 10f, SpawnService.MinGroupSpacing, SpawnService.MaxGroupSpacing);
        return true;
    }

    public bool ReturnArmed => _returnArmed;

    private const float DotRadius = 14f;
    private const float KillSelfConfirmSeconds = 3f;
    private float _killSelfUntil;
    private bool _killSelfRequested;

    // The dot stands for your parked character: clicking it selects you and arms moving where you return.
    private bool ClickOnScreen(Vector2 cursor)
    {
        if (_returnArmed || _camera == null || !CursorPicker.ToScreen(_camera, ReturnPoint, out Vector2 dot) || (dot - cursor).sqrMagnitude > DotRadius * DotRadius)
        {
            return false;
        }
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            Picker.Toggle(_player);
        }
        else
        {
            Picker.SelectOnly(_player);
        }
        _teleportArmed = false;
        Hud?.Spawn.Disarm();
        _returnArmed = true;
        Notify("Click where your character should return · right-click cancels");
        return true;
    }

    public bool TeleportArmed => _teleportArmed;

    public void ArmTeleport()
    {
        if (Picker.Selection.Count == 0)
        {
            Notify("Select units first");
            return;
        }
        if (OnlyWatchable())
        {
            return;
        }
        _returnArmed = false;
        _teleportArmed = true;
        Notify("Click where to teleport the selection · right-click cancels");
    }

    private void TeleportSelection(Vector3 point)
    {
        if (float.IsInfinity(point.x))
        {
            Notify("Can't teleport there");
            return;
        }
        bool self = Picker.IsSelected(_player);
        if (self)
        {
            _parking.MoveReturn(point);
        }
        int count = Commands.Teleport(WithoutSelf(), point);
        Notify(count == 0 && self ? "Your character will return here" : count == 0 ? "Nothing to teleport" : $"Teleported {count} unit(s){Skipped}");
    }

    public PerunHud Hud { get; set; }

    public void Tick()
    {
        _takeover.Tick();
        if (_graceUntil > 0f && Time.time >= _graceUntil)
        {
            _graceUntil = -1f;
            if (!Active)
            {
                GodMode.Disable();
                AiIgnore.Disable();
                Log("protection off");
            }
        }

        if (Active && !IsValid(_player))
        {
            Abort("raid or character gone");
            return;
        }

        if (_takeover.Active && Input.GetKeyDown(Plugin.ToggleKey.Value.MainKey) && !PerunHud.InputBlocked)
        {
            StopControl(null);
            return;
        }

        if (Plugin.ToggleKey.Value.IsDown() && !PerunHud.InputBlocked)
        {
            Log($"toggle key: active={Active} controlling={_takeover.Active}");
            if (Controlling)
            {
                StopControl(null);
                return;
            }
            if (Active)
            {
                Exit();
            }
            else
            {
                TryEnter();
            }
        }

        if (!Active)
        {
            return;
        }

        _parking.Hold(_player);

        if (_exitRequested)
        {
            _exitRequested = false;
            Exit();
            return;
        }

        if (_killSelfRequested)
        {
            _killSelfRequested = false;
            KillSelf();
            return;
        }

        if (_takeover.Active)
        {
            TickControl();
            return;
        }

        if (_settling != null)
        {
            TickSettle();
            return;
        }

        if (_finishWhenArrived != null)
        {
            TickArrival();
            return;
        }

        if (_handsLoad != null && _handsLoad.IsCompleted)
        {
            if (_handsLoad.IsFaulted)
            {
                Plugin.Log.LogWarning($"Control: loading the bot's arms failed: {_handsLoad.Exception?.GetBaseException().Message}");
            }
            _handsLoad = null;
            Player waiting = _handsFor;
            _handsFor = null;
            if (waiting != null && Picker.Selected == waiting)
            {
                TakeControl(false);
            }
            else
            {
                FlyBack();
            }
            return;
        }

        if (_controlRequested)
        {
            _controlRequested = false;
            TakeControl();
            return;
        }

        if (_flight.Active)
        {
            return;
        }

        KeepCursor();
        Camera.Tick();
        Picker.Tick(_camera);

        if (PerunHud.InputBlocked)
        {
            return;
        }

        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        foreach ((KeyCode key, bool needsCtrl, Action<bool> action) in _hotkeys)
        {
            if (Input.GetKeyDown(key) && (!needsCtrl || ctrl))
            {
                action(ctrl);
            }
        }

        if (Input.GetKeyDown(Plugin.ControlKey.Value) && _handsLoad == null && _settling == null)
        {
            TakeControl();
        }
    }

    private static bool FikaAllowsControl => !FikaInfo.Present || (Plugin.FikaControl.Value && FikaInfo.IsServer && !FikaInfo.IsHeadless);

    public void RequestControl() => _controlRequested = Active && !_takeover.Active && _handsLoad == null && _settling == null && !_flight.Active;

    public bool CanControlSelection =>
        !Commands.IsRemote && FikaAllowsControl && Picker.Selection.Count == 1 && Picker.Selected?.AIData?.BotOwner != null && !Bots.IsProtected(Picker.Selected);

    private void TakeControl(bool loadArms = true)
    {
        if (Reflect.Lacks(Feature.Control))
        {
            Notify("Taking control doesn't work with this game version (see the log)");
            return;
        }
        if (Commands.IsRemote || !FikaAllowsControl)
        {
            Notify(FikaInfo.Present ? "Taking control in Fika: only the host, with \"Allow take control in Fika\" on (F12)" : "Taking control of a bot isn't available here");
            return;
        }

        if (WorldPause.Paused)
        {
            Notify("Resume the world first (P)");
            return;
        }

        if (OnlyWatchable())
        {
            return;
        }

        if (!CanControlSelection)
        {
            Notify("Select one bot first");
            return;
        }

        Player bot = Picker.Selected;
        if (Compat.OrbitCompat.IsAsleep(bot))
        {
            FlyTo(bot);
            Notify($"ORBIT has {bot.Profile?.Nickname} asleep; it wakes when the camera gets close, then try again");
            return;
        }
        if (!_flight.Active || _flyingBack)
        {
            _flyingBack = false;
            _flight.Start(_camera.transform, () => ShoulderPose(bot), Time.unscaledTime);
        }
        if (loadArms)
        {
            System.Threading.Tasks.Task load = null;
            try
            {
                load = ControlHands.Prepare(bot);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Control: couldn't start loading the bot's arms: {ex.Message}");
            }
            if (load != null)
            {
                _handsLoad = load;
                _handsFor = bot;
                Notify($"Loading {bot.Profile?.Nickname}'s arms…");
                return;
            }
        }

        if (!Possession.Begin(bot))
        {
            FlyBack();
            Notify("That bot can't be controlled");
            return;
        }

        if (Possession.JustWoke || !Settled(bot))
        {
            _settling = bot;
            _settleFrom = Time.time + (Possession.JustWoke ? WakeDelay : 0f);
            _settleUntil = _settleFrom + 3f;
            Plugin.Log.LogInfo($"Control: waiting for {bot.Profile?.Nickname} to stand still with free hands");
            return;
        }

        FinishWhenArrived(bot);
    }

    private static Pose EyePose(Player bot)
    {
        if (bot == null || bot.PlayerBones == null)
        {
            return new Pose(Vector3.zero, Quaternion.identity);
        }
        Vector3 look = bot.LookDirection.sqrMagnitude > 0.01f ? bot.LookDirection.normalized : bot.Transform.forward;
        return new Pose(bot.PlayerBones.Head.position + look * FaceOffset, Quaternion.LookRotation(look));
    }

    // The camera waits for the arms just behind the bot's head and ends the zoom a hand's width in front of its face,
    // so it never sits inside the head or helmet model, which would show their insides.
    private const float FaceOffset = 0.15f;
    private const float ZoomOut = 1.5f;

    private static Pose ShoulderPose(Player bot)
    {
        Pose eye = EyePose(bot);
        return new Pose(eye.position - eye.rotation * Vector3.forward * 0.95f + Vector3.up * 0.15f, eye.rotation);
    }

    private void FinishWhenArrived(Player bot)
    {
        _finishWhenArrived = bot;
        _finalApproach = false;
    }

    private void TickArrival()
    {
        Player bot = _finishWhenArrived;
        if (bot == null || bot.HealthController == null || !bot.HealthController.IsAlive || WorldPause.Paused)
        {
            _finishWhenArrived = null;
            _finalApproach = false;
            Possession.End();
            FlyBack();
            return;
        }
        bot.Move(Vector2.zero);
        if (!_flight.Arrived)
        {
            return;
        }
        if (!_finalApproach)
        {
            _finalApproach = true;
            _flight.Start(_camera.transform, () => EyePose(bot), Time.unscaledTime, FinalApproach);
            return;
        }
        _finishWhenArrived = null;
        _finalApproach = false;
        FinishTakeControl(bot);
    }

    private void FlyBack()
    {
        if (_flight.Active && _camera != null)
        {
            _flyingBack = true;
            _flight.Start(_camera.transform, Camera.CurrentPose, Time.unscaledTime);
        }
    }


    // A bot woken from standby has its hands animator running again only from the next frames; taking it over at once
    // left its weapon swap stuck halfway.
    private const float WakeDelay = 1f;

    private static bool Settled(Player bot)
    {
        EPlayerState state = bot.CurrentManagedState?.Name ?? EPlayerState.None;
        return !bot.IsSprintEnabled && (state == EPlayerState.Idle || state == EPlayerState.ProneIdle) && HandsFree(bot);
    }

    private static bool HandsFree(Player bot)
    {
        switch (bot.HandsController)
        {
            case null:
                return false;
            case Player.FirearmController firearm:
                return firearm.CanRemove() && !firearm.IsInReloadOperation() && !firearm.Blindfire;
            case Player.EmptyHandsController:
            case Player.KnifeController:
                return bot.HandsController.CanRemove();
            default:
                return false;
        }
    }

    private void TickSettle()
    {
        Player bot = _settling;
        if (bot == null || bot.HealthController == null || !bot.HealthController.IsAlive || WorldPause.Paused)
        {
            _settling = null;
            Possession.End();
            FlyBack();
            return;
        }

        bot.EnableSprint(false);
        bot.Move(Vector2.zero);
        bool settled = Time.time >= _settleFrom && Settled(bot);
        if (!settled && Time.time < _settleUntil)
        {
            return;
        }

        _settling = null;
        if (!settled)
        {
            Possession.End();
            FlyBack();
            Plugin.Log.LogWarning($"Control: {bot.Profile?.Nickname} didn't settle (state {bot.CurrentManagedState?.Name}, hands {bot.HandsController?.GetType().Name}, free {HandsFree(bot)})");
            Notify($"{bot.Profile?.Nickname} is busy, try again");
            return;
        }
        FinishWhenArrived(bot);
    }

    private void FinishTakeControl(Player bot)
    {
        RtsVision.Off();
        string error = _takeover.Begin(_player, _owner, bot);
        if (error != null)
        {
            Possession.End();
            FlyBack();
            Notify($"Can't take control: {error}");
            return;
        }

        _flight.Stop();
        _flyingBack = false;
        Camera.Follow(null);
        Picker.Reset();
        _cullingRefreshAt = Time.time + 1f;
        ShowCursor(false);
        Notify($"You control {bot.Profile?.Nickname} · {Plugin.ToggleKey.Value} gives it back");
        Plugin.Log.LogInfo($"Control: took {bot.Profile?.Nickname}");
    }

    private void OpenCrate(Player bot)
    {
        if (FikaInfo.Present || Commands.IsRemote)
        {
            Notify("The arsenal crate works in single player for now");
            return;
        }
        try
        {
            string error = ArsenalCrate.Open(_takeover.Owner, bot);
            if (error != null)
            {
                Notify($"Can't open the arsenal crate: {error}");
            }
            else if (ArsenalCrate.Count == 0)
            {
                Notify("The arsenal crate is empty: fill it in the ARSENAL tab (CRATE view)");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Arsenal crate failed to open: {ex}");
            Notify("Can't open the arsenal crate (see log)");
        }
    }

    private void TickControl()
    {
        if (Time.time >= _cullingRefreshAt)
        {
            _culling.Reinforce(64);
        }
        Player bot = _takeover.Bot;
        if (bot == null || bot.HealthController == null || !bot.HealthController.IsAlive)
        {
            StopControl("Control ended: the bot died", died: true);
            return;
        }

        if (Input.GetKeyDown(Plugin.CrateKey.Value) && !PerunHud.InputBlocked && !ArsenalCrate.IsOpen)
        {
            OpenCrate(bot);
        }

        if (WorldPause.Paused)
        {
            StopControl("Control ended: the world was paused");
        }
    }

    private void RebindCamera()
    {
        UnityEngine.Camera current = CameraManager.Exist ? CameraManager.Instance.Camera : null;
        if (current == null)
        {
            return;
        }
        if (current != _camera)
        {
            _cameraParent = current.transform.parent;
            _camera = current;
            Camera.Rebind(current);
            Plugin.Log.LogInfo("Control: the game replaced its camera, RTS view moved to the new one");
        }
        if (current.transform.parent != null)
        {
            current.transform.SetParent(null, true);
        }
    }

    private void StopControl(string reason, bool died = false)
    {
        Player bot = _takeover.Bot;
        IReadOnlyList<string> failed = _takeover.End();
        Possession.End();
        if (!Active)
        {
            return;
        }
        if (_takeover.LastEndCritical)
        {
            Plugin.Log.LogError($"Control: giving {bot?.Profile?.Nickname} back left things broken ({string.Join(", ", failed)}); leaving RTS mode to restore your character");
            Exit();
            Notify($"Giving the bot back failed ({failed[0]}); back to your character");
            return;
        }
        RebindCamera();
        _culling.Apply();
        ShowCursor(true);
        if (bot != null && _camera != null)
        {
            Pose eyes = EyePose(bot);
            if (died)
            {
                Camera.JumpTo(bot.Position, eyes.rotation.eulerAngles.y);
            }
            _flyingBack = true;
            _flight.Start(_camera.transform, eyes, Camera.CurrentPose, Time.unscaledTime, ZoomOut);
            Log($"zoom out from {bot.Profile?.Nickname}");
        }
        Notify(failed.Count == 0 ? reason ?? $"{bot?.Profile?.Nickname} is back to its AI" : $"{bot?.Profile?.Nickname} is back to its AI, but {failed.Count} cleanup step(s) failed (see the log)");
        Plugin.Log.LogInfo("Control: released");
    }

    public void RequestExit() => _exitRequested = Active;

    public void TogglePause() => Commands.SetPaused(!WorldPause.Paused);

    public void FocusSelected()
    {
        if (Picker.Selected != null)
        {
            FlyTo(Picker.Selected);
        }
    }

    public void FlyTo(Player player)
    {
        if (player == null)
        {
            return;
        }

        Camera.Follow(null);
        Camera.Focus(IsParked(player) ? ReturnPoint : player.Position);
    }

    public void ToggleLock()
    {
        if (Camera.Following != null)
        {
            Camera.Follow(null);
        }
        else if (IsParked(Picker.Selected))
        {
            BackToBody();
        }
        else if (Picker.Selected != null)
        {
            Camera.Follow(Picker.Selected);
        }
    }

    public void BackToBody()
    {
        Camera.Follow(null);
        Camera.Focus(_parking.SavedPosition);
    }

    public void HealSelection()
    {
        if (Picker.Selection.Count == 0)
        {
            return;
        }

        if (OnlyWatchable())
        {
            return;
        }

        int healed = Commands.Heal(Picker.Selection);
        Notify(healed == 0 ? "Nobody to heal" : $"Healed {healed} unit(s){Skipped}");
    }

    public void HaltSelection()
    {
        if (Picker.Selection.Count == 0)
        {
            return;
        }

        if (OnlyWatchable())
        {
            return;
        }

        int halted = Commands.Halt(Picker.Selection);
        Notify($"Halted {halted} unit(s){Skipped}");
    }

    public void RemoveSelection(bool kill)
    {
        if (Picker.Selection.Count == 0)
        {
            return;
        }

        if (OnlyWatchable())
        {
            return;
        }

        bool self = Picker.IsSelected(_player);
        if (self && kill && Time.time > _killSelfUntil)
        {
            _killSelfUntil = Time.time + KillSelfConfirmSeconds;
            Notify("Kill your own character? Press Kill again to confirm · it ends your raid");
            return;
        }
        if (self && !kill && Picker.Selection.Count == 1)
        {
            Notify("Your character can't be removed");
            return;
        }

        Commands.Remove(WithoutSelf(), kill);
        Picker.ClearSelection();
        _killSelfRequested = self && kill;
    }

    private List<Player> WithoutSelf()
    {
        List<Player> units = new(Picker.Selection);
        units.Remove(_player);
        return units;
    }

    // Your character is parked under the map and protected while RTS mode runs, so it leaves RTS mode first. Runs from
    // Tick: the Kill button is drawn by the HUD, which can't have RTS mode torn down mid-frame.
    private void KillSelf()
    {
        Player player = _player;
        _killSelfUntil = 0f;
        Exit();
        _graceUntil = -1f;
        GodMode.Disable();
        AiIgnore.Disable();
        Log("protection off, killing your character");
        player.ActiveHealthController?.Kill(EDamageType.Undefined);
    }

    private bool PlaceUnits(Vector3 point, float? yaw)
    {
        if (_returnArmed)
        {
            _returnArmed = false;
            if (float.IsInfinity(point.x))
            {
                Notify("Your character can't return there");
                return true;
            }
            _parking.MoveReturn(point);
            Notify("Your character will return here");
            return true;
        }

        if (_teleportArmed)
        {
            _teleportArmed = false;
            TeleportSelection(point);
            return true;
        }

        SpawnPanel spawn = Hud?.Spawn;
        if (spawn == null || !spawn.Armed)
        {
            return false;
        }

        if (spawn.Group != null)
        {
            if (!Commands.SpawnGroup(spawn.Group, spawn.GroupDifficulty, point, yaw ?? Camera.Yaw, GroupSpacing))
            {
                Notify("Group spawning works on the host for now");
            }
            return true;
        }

        Commands.Spawn(spawn.Role, spawn.Difficulty, spawn.Count, point, yaw ?? Camera.Yaw);
        return true;
    }

    private void IssueOrder(Vector3 point, bool run, bool forced)
    {
        if (OnlyWatchable())
        {
            return;
        }

        if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
        {
            TeleportSelection(point);
            return;
        }

        if (float.IsInfinity(point.x))
        {
            Notify("Can't move there");
            return;
        }

        int count = Commands.Move(Picker.Selection, point, run, forced);
        Notify(count == 0 ? "Select bots first" : $"{count} unit(s): {(forced ? "forced move" : run ? "run" : "move")}{Skipped}");
    }

    private bool CancelPlacing()
    {
        if (_returnArmed)
        {
            _returnArmed = false;
            Notify("Return point unchanged");
            return true;
        }

        if (_teleportArmed)
        {
            _teleportArmed = false;
            Notify("Teleport cancelled");
            return true;
        }

        SpawnPanel spawn = Hud?.Spawn;
        if (spawn == null || !spawn.Armed)
        {
            return false;
        }

        spawn.Disarm();
        return true;
    }

    public void LateTick()
    {
        if (!Active || _takeover.Active)
        {
            return;
        }
        if (_flight.Active && _camera != null)
        {
            _flight.Tick(_camera.transform, Time.unscaledTime);
            if (_flyingBack && _flight.Arrived)
            {
                _flight.Stop();
                _flyingBack = false;
                Log("zoom out finished");
            }
            return;
        }
        Camera.Apply();
    }

    private void TryEnter()
    {
        if (!Singleton<GameWorld>.Instantiated)
        {
            return;
        }

        Player player = Singleton<GameWorld>.Instance.MainPlayer;
        if (!IsValid(player))
        {
            Notify("Perun: no living character");
            return;
        }

        if (FikaInfo.Present && FikaInfo.IsHeadless)
        {
            return;
        }

        if (FikaInfo.Present && !FikaInfo.IsServer)
        {
            RequestGameMaster(player);
            return;
        }

        Enter(player);
    }

    private void RequestGameMaster(Player player)
    {
        string password = Plugin.GameMasterPassword.Value;
        IGameMasterLink link = PerunGameMaster.Link;
        if (string.IsNullOrEmpty(password))
        {
            Notify("Perun is for the Fika host only (with a headless host: set the game master password in F12)");
            return;
        }

        if (link == null)
        {
            Notify("Perun.Fika.dll is missing, so Perun can't reach the host");
            return;
        }

        if (!link.CanClaim)
        {
            Notify("The host hasn't confirmed Perun yet, or runs a Perun without game master support");
            return;
        }

        if (_claimPending && Time.time < _claimUntil)
        {
            return;
        }

        _claimPending = true;
        _claimUntil = Time.time + 10f;
        link.Claim(password, player.ProfileId);
        Notify("Asking the host to make you game master…");
    }

    public void OnClaimResult(bool granted, string message)
    {
        IGameMasterLink link = PerunGameMaster.Link;
        if (!_claimPending)
        {
            if (granted)
            {
                link?.Release();
            }
            return;
        }

        _claimPending = false;
        if (!granted)
        {
            Notify(string.IsNullOrEmpty(message) ? "The host said no" : message);
            return;
        }

        Player player = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance.MainPlayer : null;
        if (link == null || !IsValid(player))
        {
            link?.Release();
            return;
        }

        Commands.Remote = new RemoteSession(link);
        if (!Enter(player))
        {
            Commands.Remote = null;
            link.Release();
            return;
        }

        Notify("You are the game master");
    }

    public void OnRemoteLost(string reason)
    {
        _claimPending = false;
        if (Commands.Remote == null)
        {
            return;
        }

        Commands.Remote = null;
        if (Active)
        {
            Exit();
        }
        Notify($"Game master ended: {reason}");
    }

    private bool Enter(Player player)
    {
        UnityEngine.Camera camera = CameraManager.Exist ? CameraManager.Instance.Camera : null;
        if (camera == null)
        {
            Notify("Perun: no game camera");
            return false;
        }

        _player = player;
        _camera = camera;

        if (player.HandsController is Player.FirearmController firearm && firearm.IsAiming)
        {
            firearm.SetAim(false);
        }
        SwitchVision(player, false);

        _owner = player.GetComponentInChildren<GamePlayerOwner>();
        if (_owner != null)
        {
            _owner.enabled = false;
        }

        GodMode.Enable(player);
        AiIgnore.Enable(player);
        _parking.Park(player);
        _smoothie.Apply();
        _culling.Apply();

        Transform cameraTransform = camera.transform;
        _cameraParent = cameraTransform.parent;
        Vector3 start = cameraTransform.position;
        cameraTransform.SetParent(null, true);
        Camera.Begin(camera, _parking.SavedPosition, start, player.Rotation.x);
        ControlScreenEffects.Unhook(player);
        if (Plugin.Verbose.Value)
        {
            List<string> effects = new();
            foreach (MonoBehaviour component in camera.GetComponents<MonoBehaviour>())
            {
                if (component != null && component.enabled)
                {
                    effects.Add(component.GetType().Name);
                }
            }
            Log($"camera effects on: {string.Join(", ", effects)}");
        }
        Picker.Reset();

        _graceUntil = -1f;
        OwnsCamera = true;
        Active = true;
        ShowCursor(true);
        Log(Commands.IsRemote ? "entered RTS mode as remote game master" : "entered RTS mode");
        return true;
    }

    private void Exit()
    {
        if (_takeover.Active)
        {
            StopControl(null);
        }
        RtsVision.Off();
        _controlRequested = false;
        _teleportArmed = false;
        _returnArmed = false;
        _handsLoad = null;
        _handsFor = null;
        _flight.Stop();
        _flyingBack = false;
        if (_finishWhenArrived != null)
        {
            _finishWhenArrived = null;
            Possession.End();
        }
        _finalApproach = false;
        if (_settling != null)
        {
            _settling = null;
            Possession.End();
        }

        Vector3? dropAt = Plugin.DropAtCursor.Value && Picker.HasNavPoint ? Picker.NavPoint : null;

        if (WorldPause.Paused)
        {
            Commands.SetPaused(false);
        }

        Active = false;
        OwnsCamera = false;

        _parking.Restore(_player, dropAt);

        if (_camera != null)
        {
            _camera.transform.SetParent(_cameraParent, true);
        }

        if (_owner != null)
        {
            _owner.enabled = true;
        }

        _culling.Restore();
        _smoothie.Restore();
        ControlScreenEffects.Hook(_player);
        SwitchVision(_player, true);
        ShowCursor(false);
        Picker.Reset();

        _graceUntil = Time.time + GraceSeconds;
        _owner = null;
        _camera = null;
        _cameraParent = null;
        ReleaseRemote();
        Plugin.Log.LogInfo("Left RTS mode");
    }

    private static void ReleaseRemote()
    {
        if (Commands.Remote == null)
        {
            return;
        }

        Commands.Remote = null;
        PerunGameMaster.Link?.Release();
    }

    private void Abort(string reason)
    {
        RtsVision.Off();
        Active = false;
        OwnsCamera = false;
        _parking.Forget();
        GodMode.Disable();
        AiIgnore.Disable(false);
        _smoothie.Restore();
        _culling.Restore();
        Picker.Reset();
        if (_owner != null)
        {
            _owner.enabled = true;
        }
        _owner = null;
        _camera = null;
        _cameraParent = null;
        _player = null;
        _graceUntil = -1f;
        _exitRequested = false;
        _killSelfRequested = false;
        _killSelfUntil = 0f;
        _claimPending = false;
        _controlRequested = false;
        _teleportArmed = false;
        _returnArmed = false;
        _handsLoad = null;
        _handsFor = null;
        _flight.Stop();
        _flyingBack = false;
        if (_finishWhenArrived != null)
        {
            _finishWhenArrived = null;
            Possession.End();
        }
        _finalApproach = false;
        if (_settling != null)
        {
            _settling = null;
            Possession.End();
        }
        _takeover.End();
        Possession.End();
        ReleaseRemote();
        UnitRegistry.Clear();
        OrderService.Clear();
        WorldPause.ResetSilently();
        Hud?.Spawn.Disarm();
        Plugin.Log.LogWarning($"RTS mode stopped: {reason}");
    }

    private void SwitchVision(Player player, bool restore)
    {
        if (player == null)
        {
            return;
        }

        try
        {
            var nightVision = player.NightVisionObserver?.Component?.Togglable;
            var thermal = player.ThermalVisionObserver?.Component?.Togglable;

            if (!restore)
            {
                _nightVisionWasOn = nightVision != null && nightVision.On;
                _thermalWasOn = thermal != null && thermal.On;
                if (_nightVisionWasOn)
                {
                    nightVision.ForceToggle(false);
                }
                if (_thermalWasOn)
                {
                    thermal.ForceToggle(false);
                }
                return;
            }

            if (_nightVisionWasOn && nightVision != null)
            {
                nightVision.ForceToggle(true);
            }
            if (_thermalWasOn && thermal != null)
            {
                thermal.ForceToggle(true);
            }
            _nightVisionWasOn = false;
            _thermalWasOn = false;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Vision toggle failed: {ex.Message}");
        }
    }

    private static void ShowCursor(bool show)
    {
        CursorSwitcher.SetCursorLockMode(show, Screen.fullScreenMode);
        if (show && Plugin.EdgePan.Value)
        {
            Cursor.lockState = CursorLockMode.Confined;
        }
        Cursor.visible = show;
        if (show)
        {
            CursorSwitcher.SetCursor(ECursorType.Idle);
        }
    }

    private static void KeepCursor()
    {
        if (!Cursor.visible || Cursor.lockState == CursorLockMode.Locked)
        {
            ShowCursor(true);
        }
    }

    private static bool IsValid(Player player)
    {
        return player != null && Singleton<GameWorld>.Instantiated && player.HealthController != null && player.HealthController.IsAlive;
    }

    public bool SelectionWatchOnly => Picker.Selection.Count > 0 && CountProtected() == Picker.Selection.Count;

    private string Skipped => CountProtected() > 0 ? " · BTR skipped" : "";

    private int CountProtected()
    {
        int count = 0;
        IReadOnlyList<Player> selection = Picker.Selection;
        for (int i = 0; i < selection.Count; i++)
        {
            if (Bots.IsProtected(selection[i]))
            {
                count++;
            }
        }
        return count;
    }

    private bool OnlyWatchable()
    {
        if (!SelectionWatchOnly)
        {
            return false;
        }
        Notify("The BTR can only be watched (L locks the camera)");
        return true;
    }

    private void Notify(string text)
    {
        Message = text;
        MessageUntil = Time.time + 3f;
        Plugin.Log.LogInfo(text);
    }

    private static void Log(string text)
    {
        if (Plugin.Verbose.Value)
        {
            Plugin.Log.LogInfo(text);
        }
    }
}
