using System;
using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.UI.BattleTimer;
using HarmonyLib;
using Perun.Client.Compat;
using UnityEngine;

namespace Perun.Client.Mode;

internal static class WorldPause
{
    private static readonly FieldInfo TimerPanelDate = Reflect.Field(Feature.PausedClock, typeof(TimerPanel), "_dateTime");
    private static readonly Dictionary<BotOwner, Vector3> Frozen = new();
    private static readonly List<BotOwner> Gone = new();

    private static DateTime _pausedAt;
    private static float _savedTimeFactor = -1f;
    private static GameWorld _world;
    private static GamePlayerOwner _frozenOwner;
    private static bool _protectedLocal;

    public static bool Paused { get; private set; }

    public static Action<bool> Broadcast { get; set; }

    public static float ResumeFactor => _savedTimeFactor;

    public static void SetResumeFactor(float factor)
    {
        if (Paused)
        {
            _savedTimeFactor = Mathf.Max(0f, factor);
        }
    }

    public static bool IsAuthority => !FikaInfo.Present || FikaInfo.IsServer;

    public static void Set(bool paused, bool fromAuthority)
    {
        if (paused == Paused || !Singleton<GameWorld>.Instantiated)
        {
            return;
        }

        Paused = paused;
        _world = Singleton<GameWorld>.Instance;

        if (paused)
        {
            OnPause();
        }
        else
        {
            OnResume();
        }

        if (fromAuthority)
        {
            Broadcast?.Invoke(paused);
            World.WorldEnvironment.AnnounceTime();
        }

        Plugin.Log.LogInfo(paused ? "World paused" : "World resumed");
    }

    public static void Tick()
    {
        if (Paused && (!Singleton<GameWorld>.Instantiated || Singleton<GameWorld>.Instance != _world))
        {
            ResetSilently();
            return;
        }

        if (Paused && Frozen.Count > 0)
        {
            KeepFrozen();
        }
    }

    private static void KeepFrozen()
    {
        Gone.Clear();
        foreach (KeyValuePair<BotOwner, Vector3> entry in Frozen)
        {
            BotOwner bot = entry.Key;
            Player player = bot != null ? bot.GetPlayer : null;
            if (player == null || player.HealthController == null || !player.HealthController.IsAlive)
            {
                Gone.Add(bot);
                continue;
            }

            player.Move(Vector2.zero);
            bot.ShootData?.EndShoot();
            if ((player.Position - entry.Value).sqrMagnitude > 0.01f)
            {
                player.Teleport(entry.Value);
            }
        }

        foreach (BotOwner bot in Gone)
        {
            Frozen.Remove(bot);
        }
    }

    public static void ResetSilently()
    {
        Paused = false;
        Frozen.Clear();
        _savedTimeFactor = -1f;
        _frozenOwner = null;
        if (_protectedLocal)
        {
            _protectedLocal = false;
            GodMode.Disable();
        }
        _world = null;
    }

    public static bool ShouldSkipBot(BotOwner bot)
    {
        if (!Paused || bot == null || bot.BotState != EBotState.Active)
        {
            return false;
        }

        if (!Frozen.ContainsKey(bot))
        {
            Frozen[bot] = bot.Position;
            Hold(bot);
        }
        return true;
    }

    private static void OnPause()
    {
        _pausedAt = DateTime.UtcNow;
        Frozen.Clear();

        GameDateTime clock = _world.GameDateTime;
        if (clock != null)
        {
            _savedTimeFactor = clock.TimeFactor;
            clock.Reset(DateTimeExtensions.UtcNow, clock.Calculate(), 0f, true);
        }

        FreezeLocalPlayer();
    }

    private static void OnResume()
    {
        TimeSpan paused = DateTime.UtcNow - _pausedAt;

        GameDateTime clock = _world.GameDateTime;
        if (clock != null && _savedTimeFactor >= 0f)
        {
            clock.Reset(DateTimeExtensions.UtcNow, clock.Calculate(), _savedTimeFactor, true);
        }
        _savedTimeFactor = -1f;

        ShiftRaidTimer(paused);
        Frozen.Clear();
        UnfreezeLocalPlayer();
    }

    private static void Hold(BotOwner bot)
    {
        try
        {
            bot.StopMove();
            bot.ShootData?.EndShoot();
            Player player = bot.GetPlayer;
            if (player != null)
            {
                player.EnableSprint(false);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Pause: could not stop {bot.Profile?.Nickname}: {ex.Message}");
        }
    }

    private static void FreezeLocalPlayer()
    {
        if (PerunMode.OwnsCamera)
        {
            return;
        }

        Player player = _world.MainPlayer;
        if (player == null || player.HealthController == null || !player.HealthController.IsAlive)
        {
            return;
        }

        _frozenOwner = player.GetComponentInChildren<GamePlayerOwner>();
        if (_frozenOwner != null)
        {
            _frozenOwner.enabled = false;
        }

        player.Move(Vector2.zero);
        player.EnableSprint(false);

        if (!GodMode.Active)
        {
            GodMode.Enable(player);
            _protectedLocal = true;
        }
    }

    private static void UnfreezeLocalPlayer()
    {
        if (_frozenOwner != null)
        {
            _frozenOwner.enabled = true;
            _frozenOwner = null;
        }

        if (_protectedLocal)
        {
            _protectedLocal = false;
            GodMode.Disable();
        }
    }

    private static void ShiftRaidTimer(TimeSpan paused)
    {
        if (paused <= TimeSpan.Zero)
        {
            return;
        }

        GameTimer timer = Singleton<AbstractGame>.Instantiated ? Singleton<AbstractGame>.Instance.GameTimer : null;
        if (timer != null)
        {
            if (timer._startDateTime.HasValue)
            {
                timer._startDateTime = timer._startDateTime.Value + paused;
            }
            if (timer._escapeDateTime.HasValue)
            {
                timer._escapeDateTime = timer._escapeDateTime.Value + paused;
            }
        }

        if (TimerPanelDate == null)
        {
            return;
        }

        foreach (TimerPanel panel in UnityEngine.Object.FindObjectsOfType<TimerPanel>(true))
        {
            if (panel == null)
            {
                continue;
            }

            if (TimerPanelDate.GetValue(panel) is DateTime date && date != default)
            {
                TimerPanelDate.SetValue(panel, date + paused);
            }
        }
    }
}
