using System;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;

namespace Perun.Client.Mode;

internal sealed class WeaponSwitch
{
    private enum Stage
    {
        Idle,
        Scheduled,
        Lowering,
        Drawing
    }

    private const float Timeout = 1.5f;

    private Stage _stage;
    private Player _player;
    private float _deadline;
    private readonly SwitchRun _runs = new();
    private int _run;
    private bool _nudged;

    public void Schedule(Player player, float delay)
    {
        _run = _runs.Start();
        _nudged = false;
        _player = player;
        _stage = Stage.Scheduled;
        _deadline = Time.time + delay;
    }

    public void Start(Player player)
    {
        _run = _runs.Start();
        _nudged = false;
        _player = player;
        Begin();
    }

    public void Cancel()
    {
        _runs.Cancel();
        _player = null;
        _stage = Stage.Idle;
    }

    public void Tick()
    {
        if (_stage == Stage.Idle)
        {
            return;
        }
        if (!Alive(_player))
        {
            Cancel();
            return;
        }
        if (Time.time < _deadline)
        {
            return;
        }
        if (_stage == Stage.Scheduled)
        {
            Begin();
            return;
        }

        string operation = _player.HandsController is Player.FirearmController firearm ? firearm.CurrentOperation?.GetType().Name : "-";
        string stage = _stage == Stage.Lowering ? "lowering" : "drawing";
        if (!_nudged && _player.HandsController != null)
        {
            // The hide and draw animations end on an animator event. A bot whose hands animator was culled or
            // throttled never sends it, so finish the step the way the game does for bots on a simplified skeleton.
            _nudged = true;
            _deadline = Time.time + Timeout;
            Plugin.Log.LogInfo($"Control: {_player.Profile?.Nickname}'s weapon {stage} didn't finish (operation {operation}), finishing it");
            try
            {
                _player.HandsController.FastForwardCurrentState();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Control: couldn't finish {_player.Profile?.Nickname}'s weapon {stage}: {ex.Message}");
            }
            return;
        }
        Plugin.Log.LogWarning($"Control: {_player.Profile?.Nickname}'s weapon switch is stuck {stage} (hands {_player.HandsController?.GetType().Name}, operation {operation})");
        Cancel();
    }

    // Fika's bots already have player-type hands, but their weapon was set up for a third-person bot, so it's drawn
    // again when you take control. Giving the bot back leaves it alone: switching there could leave it empty-handed.
    private static bool NeedsSwitch(Player player, Player.FirearmController firearm)
    {
        if ((firearm.GetType().Namespace ?? "").StartsWith("Fika"))
        {
            return !player.IsAI;
        }
        return firearm is AIFirearmController != player.IsAI;
    }

    private static bool Alive(Player player) => player != null && player.HealthController != null && player.HealthController.IsAlive;

    private void Begin()
    {
        Player player = _player;
        if (!Alive(player) || player.HandsController is not Player.FirearmController firearm || firearm.Item == null || !NeedsSwitch(player, firearm))
        {
            Cancel();
            return;
        }

        int run = _run;
        Weapon weapon = firearm.Item;
        string name = player.Profile?.Nickname;
        _stage = Stage.Lowering;
        _deadline = Time.time + Timeout;
        try
        {
            player.SetEmptyHands(emptied =>
            {
                if (emptied.Failed)
                {
                    Plugin.Log.LogWarning($"Control: lowering {name}'s weapon failed: {emptied.Error}");
                    Finish(run);
                    return;
                }
                if (!_runs.IsCurrent(run) || !Alive(player))
                {
                    Finish(run);
                    return;
                }
                _stage = Stage.Drawing;
                _deadline = Time.time + Timeout;
                _nudged = false;
                player.SetInHands(weapon, drawn =>
                {
                    if (drawn.Failed)
                    {
                        Plugin.Log.LogWarning($"Control: drawing {name}'s weapon again failed: {drawn.Error}");
                    }
                    Finish(run);
                });
            });
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Control: couldn't switch {name}'s weapon: {ex.Message}");
            Finish(run);
        }
    }

    private void Finish(int run)
    {
        if (_runs.IsCurrent(run))
        {
            Cancel();
        }
    }
}
