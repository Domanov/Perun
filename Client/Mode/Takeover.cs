using System;
using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.Animations;
using EFT.CameraControl;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Screens;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Perun.Client.Mode;

internal sealed class Takeover
{
    // A bot gets its own GamePlayerOwner; the raid context the game gives yours (player, insurance, date, location,
    // battle screen) lives in private fields and has to be copied across.
    private static readonly FieldInfo MyPlayerField = Reflect.Field(Feature.Control, typeof(GamePlayerOwner), "_myPlayer");
    private static readonly MethodInfo CreateOwner = FindCreate();
    private static readonly FieldInfo InsuranceField = Reflect.Field(Feature.Control, typeof(GamePlayerOwner), "_insurance");
    private static readonly FieldInfo DateTimeField = Reflect.Field(Feature.Control, typeof(GamePlayerOwner), "_gameDateTime");
    private static readonly FieldInfo LocationField = Reflect.Field(Feature.Control, typeof(GamePlayerOwner), "_location");
    private static readonly FieldInfo BattleScreenField = Reflect.Field(Feature.ControlScreenEffects, typeof(GamePlayerOwner), "BattleUIScreenController");
    private static readonly FieldInfo RemoveItemField = Reflect.Field(Feature.Control, typeof(ItemController), "_removeItemEvent");

    public static bool KeepCamera;

    private EftGamePlayerOwner _mainOwner;
    private Player _main;
    private EftGamePlayerOwner _botOwner;
    private Player _bot;
    private PlayerCameraController _mainCamera;
    private EProceduralAnimationMask _botMask;
    private bool _botWasAI;
    private const float WeaponSwitchDelay = 0.6f;

    private readonly WeaponSwitch _weapon = new();
    private GamePlayerOwner _reshowOwner;
    private int _reshowFrame;

    public void Tick()
    {
        _weapon.Tick();
        if (_reshowOwner == null || Time.frameCount < _reshowFrame)
        {
            return;
        }
        GamePlayerOwner owner = _reshowOwner;
        _reshowOwner = null;
        try
        {
            ShowScreen(owner);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Control: couldn't show your HUD again: {ex.Message}");
        }
    }

    public bool Active => _bot != null;

    public static Player Parked { get; private set; }

    public Player Bot => _bot;
    public EftGamePlayerOwner Owner => _botOwner;

    public string Begin(Player main, GamePlayerOwner mainOwner, Player bot)
    {
        if (Reflect.Lacks(Feature.Control))
        {
            return "this game version isn't supported for taking control";
        }

        if (mainOwner is not EftGamePlayerOwner eftOwner)
        {
            return "your character's controls weren't found";
        }

        if (bot.Profile?.Info?.Settings != null && bot.Profile.Info.Settings.UseSimpleAnimator)
        {
            return "infected can't be controlled";
        }

        GameUI gameUI = MonoBehaviourSingleton<GameUI>.Instantiated ? MonoBehaviourSingleton<GameUI>.Instance : null;
        if (gameUI == null || mainOwner.Session is not IEftSession session)
        {
            return "the raid interface isn't ready";
        }

        PlayerCameraController mainCamera = main.GetComponent<PlayerCameraController>();
        if (mainCamera == null)
        {
            return "your character's camera wasn't found";
        }

        _mainOwner = eftOwner;
        _main = main;
        Parked = main;
        main.Speaker?.Shut();
        _bot = bot;
        _mainCamera = mainCamera;

        try
        {
            if (!ControlHands.Ensure(bot, main))
            {
                End();
                return "no first-person arms could be found for that bot";
            }
            _botMask = bot.ProceduralWeaponAnimation.Mask;
            bot.ProceduralWeaponAnimation.Mask = main.ProceduralWeaponAnimation.Mask;
            _botWasAI = bot.AIData is AIData data && data.IsAI;
            if (bot.AIData is AIData aiData)
            {
                aiData.IsAI = false;
            }
            _weapon.Schedule(bot, WeaponSwitchDelay);
            _mainOwner.Mute(true);
            main.IsYourPlayer = false;
            bot.IsYourPlayer = true;
            bot.PointOfView = EPointOfView.FirstPerson;
            bot.ChangeSpeed(1f);
            DisableOptics(bot);

            object created = CreateOwner.MakeGenericMethod(mainOwner.GetType()).Invoke(null, new object[]
            {
                bot, mainOwner.InputTree, InsuranceField.GetValue(mainOwner), session, gameUI, DateTimeField.GetValue(mainOwner), LocationField.GetValue(mainOwner)
            });
            _botOwner = (EftGamePlayerOwner)created;

            ControlScreenEffects.ClearAll();
            KeepCamera = true;
            try
            {
                PlayerCameraController.Create(bot);
                ControlScreenEffects.MatchGlasses(bot);
            }
            finally
            {
                KeepCamera = false;
            }
            ControlAudio.FollowCamera();
            _botOwner.vmethod_0();
            ControlAudio.MatchEnvironment(bot);
            ControlAudio.StopTinnitus();
            ControlAudio.MatchHearing(bot);
            Compat.RealismCompat.Follow(bot);
            Compat.FovFixCompat.Follow(bot);
            return null;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Take control failed: {ex}");
            End();
            return "taking control failed (see log)";
        }
    }

    private static void UnhookOwner(Player player, GamePlayerOwner owner)
    {
        object controller = player?.InventoryController;
        if (controller == null || RemoveItemField == null || RemoveItemField.GetValue(controller) is not Delegate handlers)
        {
            return;
        }

        Delegate remaining = handlers;
        int removed = 0;
        foreach (Delegate handler in handlers.GetInvocationList())
        {
            object target = handler.Target;
            if (target == null)
            {
                continue;
            }
            foreach (FieldInfo field in target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (ReferenceEquals(field.GetValue(target), owner))
                {
                    remaining = Delegate.Remove(remaining, handler);
                    removed++;
                    break;
                }
            }
        }
        if (removed > 0)
        {
            RemoveItemField.SetValue(controller, remaining);
        }
    }

    private static void DestroyCamera(Player player)
    {
        if (!player.gameObject.TryGetComponent(out PlayerCameraController camera))
        {
            return;
        }
        try
        {
            // DestroyImmediate on purpose: PlayerCameraController.OnDestroy releases whatever controller is the
            // current singleton and resets the audio listener. Deferred to the end of the frame, it would release
            // the main controller created right after this and leave the listener under the map (no sound).
            Object.DestroyImmediate(camera);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Control: camera cleanup on {player.Profile?.Nickname}: {ex.Message}");
        }
    }

    private static void DisableOptics(Player player)
    {
        if (player == null)
        {
            return;
        }
        foreach (OpticSight sight in player.GetComponentsInChildren<OpticSight>(true))
        {
            if (sight != null && sight.enabled)
            {
                sight.enabled = false;
            }
        }
    }

    public IReadOnlyList<string> End()
    {
        List<string> failed = new();
        bool critical = false;

        void Step(string what, bool essential, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Give back control: could not {what}: {ex.Message}");
                failed.Add(what);
                critical |= essential;
            }
        }

        Player bot = _bot;
        Player main = _main;
        EftGamePlayerOwner botOwner = _botOwner;
        EftGamePlayerOwner mainOwner = _mainOwner;
        _bot = null;
        _main = null;
        Parked = null;
        _botOwner = null;
        _mainOwner = null;

        Step("hand 'your player' back", true, () =>
        {
            if (bot != null)
            {
                bot.IsYourPlayer = false;
            }
            if (main != null)
            {
                main.IsYourPlayer = true;
            }
        });

        Step("stop the bot's controls", true, () =>
        {
            if (botOwner != null)
            {
                botOwner.vmethod_1();
                UnhookOwner(bot, botOwner);
                // DestroyImmediate on purpose: GamePlayerOwner.CleanupOnDestroy sets the static _myPlayer to null.
                // Deferred, it would run after "give your character its controls back" restores it to your character.
                Object.DestroyImmediate(botOwner);
            }
            if (main != null && Singleton<BetterAudio>.Instantiated)
            {
                Singleton<BetterAudio>.Instance.UnsubscribeProtagonist();
                Singleton<BetterAudio>.Instance.SetProtagonist(main);
            }
        });

        Step("remove the bot's camera", true, () =>
        {
            if (bot != null)
            {
                bot.HasGamePlayerOwner = false;
                ControlScreenEffects.Unhook(bot);
                DestroyCamera(bot);
            }
            if (_mainCamera != null)
            {
                Singleton<PlayerCameraController>.Create(_mainCamera);
            }
            ControlAudio.FollowCamera();
            if (main != null)
            {
                if (CameraManager.Exist)
                {
                    CameraManager.Instance.SetPlayer(main);
                }
                ControlScreenEffects.Unhook(main);
            }
        });

        Step("clear the bot's death from the screen", false, () =>
        {
            if (bot != null && (bot.HealthController == null || !bot.HealthController.IsAlive))
            {
                ControlScreenEffects.ClearDeath();
            }
        });

        _weapon.Cancel();
        Step("give the bot back to its AI", false, () =>
        {
            DisableOptics(bot);
            if (bot != null && bot.AIData is AIData aiData && _botWasAI)
            {
                aiData.IsAI = true;
                _weapon.Start(bot);
            }
            if (bot != null && bot.ProceduralWeaponAnimation != null)
            {
                bot.ProceduralWeaponAnimation.Mask = _botMask;
            }
            _botWasAI = false;
        });

        Step("return the bot to third person", false, () =>
        {
            if (bot != null && bot.HealthController != null && bot.HealthController.IsAlive)
            {
                bot.PointOfView = EPointOfView.ThirdPerson;
            }
            else if (bot != null && bot.PlayerBody != null)
            {
                bot.PlayerBody.UpdatePlayerRenders(EPointOfView.ThirdPerson, bot.Profile?.Info?.Side ?? EPlayerSide.Savage);
            }
        });

        Step("give your character its controls back", true, () =>
        {
            if (main != null)
            {
                MyPlayerField?.SetValue(null, main);
                if (MonoBehaviourSingleton<GameUI>.Instantiated)
                {
                    MonoBehaviourSingleton<GameUI>.Instance.UsingPanel.Init(main.HealthController, main.InventoryController);
                }
            }
            if (mainOwner != null)
            {
                mainOwner.Mute(false);
                _reshowOwner = mainOwner;
                _reshowFrame = Time.frameCount + 5;
            }
        });

        Step("match the sound to your character's surroundings", false, () =>
        {
            Parked = null;
            Compat.RealismCompat.Follow(main);
            Compat.FovFixCompat.Follow(main);
            ControlAudio.MatchEnvironment(main);
            ControlAudio.MatchHearing(main);
            ControlAudio.StopTinnitus();
            ControlAudio.ResumeTinnitus(main);
        });

        _mainCamera = null;
        LastEndCritical = critical;
        return failed;
    }

    public bool LastEndCritical { get; private set; }

    private static void ShowScreen(GamePlayerOwner owner)
    {
        object controller = BattleScreenField?.GetValue(owner);
        MethodInfo show = controller != null ? AccessTools.Method(controller.GetType(), "ShowScreen", new[] { typeof(EScreenState) }) : null;
        if (show != null)
        {
            show.Invoke(controller, new object[] { EScreenState.Root });
        }
    }

    private static MethodInfo FindCreate()
    {
        foreach (MethodInfo method in typeof(EftGamePlayerOwner).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name == nameof(EftGamePlayerOwner.Create) && method.IsGenericMethodDefinition && method.GetParameters().Length == 7)
            {
                return method;
            }
        }
        Reflect.Require(Feature.Control, false, "EftGamePlayerOwner.Create<T>");
        return null;
    }
}
