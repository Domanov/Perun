using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Perun.Client.Mode;
using Perun.Client.Patches;
using Perun.Client.UI;
using UnityEngine;
using static Perun.Client.Constants.Plugin;

namespace Perun.Client;

[BepInPlugin(Guid, Name, Version)]
[BepInDependency(GuidSptCore, BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency(GuidBigBrain, BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency(GuidFika, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(GuidSmoothie, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(GuidMoreBots, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(GuidSain, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "com.dmnv.perun";

    public const string Name = "DMNV-Perun";

    public const string Version = "0.13.0";

    internal static ManualLogSource Log;

    internal static ConfigEntry<KeyboardShortcut> ToggleKey;
    internal static ConfigEntry<bool> DropAtCursor;

    internal static ConfigEntry<float> PanSpeed;
    internal static ConfigEntry<float> RotateSpeed;
    internal static ConfigEntry<float> ZoomSpeed;
    internal static ConfigEntry<float> MinDistance;
    internal static ConfigEntry<float> MaxDistance;
    internal static ConfigEntry<bool> EdgePan;
    internal static ConfigEntry<bool> CameraCollision;
    internal static ConfigEntry<KeyCode> TiltKey;
    internal static ConfigEntry<KeyCode> ControlKey;
    internal static ConfigEntry<KeyCode> CrateKey;

    internal static ConfigEntry<int> MaxUnits;
    internal static ConfigEntry<bool> PrewarmProfiles;
    internal static ConfigEntry<bool> BlockHostileMembers;
    internal static ConfigEntry<bool> ShowLabels;
    internal static readonly System.Collections.Generic.Dictionary<BotKind, ConfigEntry<Color>> KindColors = new();
    internal static ConfigEntry<Color> SelectionColor;
    internal static ConfigEntry<float> LabelDistance;

    internal static ConfigEntry<bool> Verbose;
    internal static ConfigEntry<string> GameMasterPassword;
    internal static ConfigEntry<bool> FikaControl;

    internal static PerunMode Mode { get; private set; }

    private PerunMode _mode;
    private PerunHud _hud;
    private PerunGui _gui;

    private void Awake()
    {
        Log = Logger;
        BindConfig();

        new PlayerCameraLateUpdatePatch().Enable();
        new AddEnemyPatch().Enable();
        AsBotPatch.Enable();
        new ParkedBreathPatch().Enable();
        new ParkedVoicePatch().Enable();
        new ParkedHitPatch().Enable();
        new BotUpdatePausePatch().Enable();
        new BotFixedUpdatePausePatch().Enable();
        new BotBrainPausePatch().Enable();
        new RaidEndTimerPausePatch().Enable();
        new BotShootPausePatch().Enable();
        new PossessedWeaponDirectionPatch().Enable();
        new PossessedMoverSnapPatch().Enable();
        new PossessedMalfunctionPatch().Enable();
        new PossessedInputScopePatch().Enable();
        new PossessedRotatePatch().Enable();
        new PossessedMovePatch().Enable();
        new PossessedPosePatch().Enable();
        new PossessedPronePatch().Enable();
        new PossessedSprintPatch().Enable();
        new PossessedJumpPatch().Enable();
        new PossessedAimPatch().Enable();
        new PossessedTriggerPatch().Enable();
        new KeepCameraPatch().Enable();
        new KeepCameraVisorPatch().Enable();
        Compat.SainCompat.Enable();
        Compat.BigBrainCompat.Enable();
        Compat.MoreBotsCompat.Enable();
        Compat.SmoothieControl.Enable();
        HealthPatches.Enable();
        DrakiaXYZ.BigBrain.Brains.BrainManager.AddCustomLayer(typeof(Orders.PerunOrderLayer), Orders.PerunOrderLayer.Brains, Orders.PerunOrderLayer.LayerPriority);
        DrakiaXYZ.BigBrain.Brains.BrainManager.AddCustomLayer(typeof(Orders.PerunEscortLayer), Orders.PerunOrderLayer.Brains, Orders.PerunEscortLayer.LayerPriority);

        _mode = new PerunMode();
        Mode = _mode;
        _hud = new PerunHud(_mode);
        _mode.Hud = _hud;
        _gui = gameObject.AddComponent<PerunGui>();
        _gui.Hud = _hud;
        UpdateGui();

        Reflect.Check(typeof(Takeover), typeof(ControlScreenEffects), typeof(ControlAudio), typeof(WorldPause), typeof(Compat.Animators), typeof(Units.Teleporting));
        Log.LogInfo($"{Name} {Version} loaded");
    }

    // The other mods' types exist once every plugin has woken up, which Start comes after.
    private void Start()
    {
        Compat.OrbitCompat.Enable();
        Compat.FovFixCompat.Enable();
        Compat.RealismCompat.Enable();
        Compat.FikaDeathGuard.Enable();
        Compat.FikaBridgeLoader.Load(System.IO.Path.GetDirectoryName(Info.Location), gameObject, Config);
    }

    private void Update()
    {
        try
        {
            RaidScope.Tick();
            Diagnostics.Tick(Config);
            WorldPause.Tick();
            Orders.OrderService.Tick();
            Units.GroupLeadership.Tick();
            _mode.Tick();
            View.RtsVision.Tick();
        }
        catch (System.Exception ex)
        {
            ErrorGuard.Report("Update", ex);
        }
        UpdateGui();
    }

    private void UpdateGui()
    {
        bool layout = _mode.Active && !_hud.Hidden && !_mode.Controlling;
        bool draw = layout || WorldPause.Paused || _hud.HasMessage;
        if (!layout)
        {
            PerunHud.ReleaseInput();
        }
        if (_gui.enabled != draw)
        {
            _gui.enabled = draw;
        }
        if (_gui.useGUILayout != layout)
        {
            _gui.useGUILayout = layout;
        }
    }

    private void LateUpdate()
    {
        try
        {
            _mode.LateTick();
        }
        catch (System.Exception ex)
        {
            ErrorGuard.Report("Camera", ex);
        }
    }

    private void BindConfig()
    {
        ToggleKey = Config.Bind(ConfigSectionGeneral, ConfigKeyToggleRTSMode, new KeyboardShortcut(KeyCode.Y),
            new ConfigDescription(ConfigDescriptionToggleRTSMode, null, new ConfigurationManagerAttributes { Order = 100 }));
        ControlKey = Config.Bind(ConfigSectionGeneral, ConfigKeyTakeControlKey, KeyCode.G,
            new ConfigDescription(ConfigDescriptionTakeControlKey, null, new ConfigurationManagerAttributes { Order = 95 }));
        CrateKey = Config.Bind(ConfigSectionGeneral, ConfigKeyArsenalCrateKey, KeyCode.K,
            new ConfigDescription(ConfigDescriptionArsenalCrateKey, null, new ConfigurationManagerAttributes { Order = 94 }));
        DropAtCursor = Config.Bind(ConfigSectionGeneral, ConfigKeyReturnAtCursor, false,
            new ConfigDescription(ConfigDescriptionReturnAtCursor, null, new ConfigurationManagerAttributes { Order = 90 }));

        PanSpeed = Config.Bind(ConfigSectionCamera, ConfigKeyPanSpeed, 1f,
            new ConfigDescription(ConfigDescriptionPanSpeed, new AcceptableValueRange<float>(0.2f, 5f), new ConfigurationManagerAttributes { Order = 100 }));
        RotateSpeed = Config.Bind(ConfigSectionCamera, ConfigKeyRotateSpeed, 1f,
            new ConfigDescription(ConfigDescriptionRotateSpeed, new AcceptableValueRange<float>(0.2f, 5f), new ConfigurationManagerAttributes { Order = 90 }));
        ZoomSpeed = Config.Bind(ConfigSectionCamera, ConfigKeyZoomSpeed, 1f,
            new ConfigDescription(ConfigDescriptionZoomSpeed, new AcceptableValueRange<float>(0.2f, 5f), new ConfigurationManagerAttributes { Order = 80 }));
        MinDistance = Config.Bind(ConfigSectionCamera, ConfigKeyClosestZoom, 4f,
            new ConfigDescription(ConfigDescriptionClosestZoom, new AcceptableValueRange<float>(1f, 50f), new ConfigurationManagerAttributes { Order = 70 }));
        MaxDistance = Config.Bind(ConfigSectionCamera, ConfigKeyFarthestZoom, 180f,
            new ConfigDescription(ConfigDescriptionFarthestZoom, new AcceptableValueRange<float>(50f, 600f), new ConfigurationManagerAttributes { Order = 60 }));
        TiltKey = Config.Bind(ConfigSectionCamera, ConfigKeyTiltKey, KeyCode.LeftAlt,
            new ConfigDescription(ConfigDescriptionTiltKey, null, new ConfigurationManagerAttributes { Order = 55 }));
        EdgePan = Config.Bind(ConfigSectionCamera, ConfigKeyEdgeScrolling, true,
            new ConfigDescription(ConfigDescriptionEdgeScrolling, null, new ConfigurationManagerAttributes { Order = 50 }));
        CameraCollision = Config.Bind(ConfigSectionCamera, ConfigKeyAvoidWalls, false,
            new ConfigDescription(ConfigDescriptionAvoidWalls, null, new ConfigurationManagerAttributes { Order = 40 }));

        MaxUnits = Config.Bind(ConfigSectionGeneral, ConfigKeyMaxSpawnedUnits, 40,
            new ConfigDescription(ConfigDescriptionMaxSpawnedUnits, new AcceptableValueRange<int>(1, 200), new ConfigurationManagerAttributes { Order = 70 }));

        PrewarmProfiles = Config.Bind(ConfigSectionGeneral, ConfigKeyPrepareBotsInAdvance, true,
            new ConfigDescription(ConfigDescriptionPrepareBotsInAdvance, null, new ConfigurationManagerAttributes { Order = 60 }));

        BlockHostileMembers = Config.Bind(ConfigSectionGeneral, ConfigKeyRefuseHostileGroupMembers, false,
            new ConfigDescription(ConfigDescriptionRefuseHostileGroupMembers, null, new ConfigurationManagerAttributes { Order = 55 }));

        ShowLabels = Config.Bind(ConfigSectionDisplay, ConfigKeyUnitLabels, true,
            new ConfigDescription(ConfigDescriptionUnitLabels, null, new ConfigurationManagerAttributes { Order = 100 }));
        LabelDistance = Config.Bind(ConfigSectionDisplay, ConfigKeyLabelDistance, 300f,
            new ConfigDescription(ConfigDescriptionLabelDistance, new AcceptableValueRange<float>(25f, 1000f), new ConfigurationManagerAttributes { Order = 90 }));

        SelectionColor = Config.Bind(ConfigSectionDisplay, ConfigKeySelectionColour, new Color(0.24f, 1f, 0.43f, 1f),
            new ConfigDescription(ConfigDescriptionSelectionColour, null, new ConfigurationManagerAttributes { Order = 85 }));

        (BotKind kind, string name, string hex)[] colors =
        {
            (BotKind.Player, ConfigKeyColourPlayer, ColorPlayer),
            (BotKind.Usec, ConfigKeyColourUsec, ColorUsec),
            (BotKind.Bear, ConfigKeyColourBear, ColorBear),
            (BotKind.Scav, ConfigKeyColourScav, ColorScav),
            (BotKind.Raider, ConfigKeyColourRaider, ColorRaider),
            (BotKind.Boss, ConfigKeyColourBoss, ColorBoss),
            (BotKind.Follower, ConfigKeyColourFollower, ColorFollower),
            (BotKind.Cultist, ConfigKeyColourCultist, ColorCultist),
            (BotKind.Infected, ConfigKeyColourInfected, ColorInfected),
            (BotKind.Custom, ConfigKeyColourCustom, ColorCustom),
            (BotKind.Other, ConfigKeyColourOther, ColorOther)
        };
        int colorOrder = 80;
        foreach ((BotKind kind, string name, string hex) in colors)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            ConfigEntry<Color> entry = Config.Bind(ConfigSectionDisplay, name, color,
                new ConfigDescription(ConfigDescriptionLabelColour, null, new ConfigurationManagerAttributes { Order = colorOrder--, IsAdvanced = true }));
            entry.SettingChanged += (_, _) => BotKinds.InvalidateColors();
            KindColors[kind] = entry;
        }

        GameMasterPassword = Config.Bind(ConfigSectionFikaGameMaster, ConfigKeyGameMasterPassword, "",
            new ConfigDescription(ConfigDescriptionGameMasterPassword, null, new ConfigurationManagerAttributes { Order = 100 }));
        FikaControl = Config.Bind(ConfigSectionFikaGameMaster, ConfigKeyAllowTakeControlInFika, false,
            new ConfigDescription(ConfigDescriptionAllowTakeControlInFika, null, new ConfigurationManagerAttributes { Order = 90 }));

        Verbose = Config.Bind(ConfigSectionDebug, ConfigKeyVerboseLogging, false,
            new ConfigDescription(ConfigDescriptionVerboseLogging, null, new ConfigurationManagerAttributes { Order = 100 }));
    }
}

internal sealed class ConfigurationManagerAttributes
{
    public int? Order;
    public bool? IsAdvanced;
}
