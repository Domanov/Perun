using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Perun.Client.Mode;
using Perun.Client.Patches;
using Perun.Client.UI;
using UnityEngine;

namespace Perun.Client;

[BepInPlugin(Guid, Name, Version)]
[BepInDependency("com.SPT.core", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("xyz.drakia.bigbrain", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("com.fika.core", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.tominocz.smoothie", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.morebotsapi.tacticaltoaster", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("me.sol.sain", BepInDependency.DependencyFlags.SoftDependency)]
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
        const string general = "1. General";
        ToggleKey = Config.Bind(general, "Toggle RTS mode", new KeyboardShortcut(KeyCode.Y),
            new ConfigDescription("Switches between your character and the RTS view. In Fika only the host can use it.", null, new ConfigurationManagerAttributes { Order = 100 }));
        ControlKey = Config.Bind(general, "Take control key", KeyCode.G,
            new ConfigDescription("In RTS mode: take full control of the selected bot with the game's own controls, HUD and inventory. The RTS toggle key gives it back. Single player only for now.", null, new ConfigurationManagerAttributes { Order = 95 }));
        CrateKey = Config.Bind(general, "Arsenal crate key", KeyCode.K,
            new ConfigDescription("While you control a bot: opens its inventory with the arsenal crate beside it. Fill the crate in the ARSENAL tab (CRATE view). Single player only for now.", null, new ConfigurationManagerAttributes { Order = 94 }));
        DropAtCursor = Config.Bind(general, "Return at cursor", false,
            new ConfigDescription("On leaving RTS mode, put your character at the point under the cursor instead of where you left it.", null, new ConfigurationManagerAttributes { Order = 90 }));

        const string camera = "2. Camera";
        PanSpeed = Config.Bind(camera, "Pan speed", 1f,
            new ConfigDescription("WASD / edge pan speed. Scales with zoom; hold Shift for faster.", new AcceptableValueRange<float>(0.2f, 5f), new ConfigurationManagerAttributes { Order = 100 }));
        RotateSpeed = Config.Bind(camera, "Rotate speed", 1f,
            new ConfigDescription("Middle mouse drag and Q/E rotation speed.", new AcceptableValueRange<float>(0.2f, 5f), new ConfigurationManagerAttributes { Order = 90 }));
        ZoomSpeed = Config.Bind(camera, "Zoom speed", 1f,
            new ConfigDescription("Mouse wheel zoom speed.", new AcceptableValueRange<float>(0.2f, 5f), new ConfigurationManagerAttributes { Order = 80 }));
        MinDistance = Config.Bind(camera, "Closest zoom (m)", 4f,
            new ConfigDescription("Smallest camera distance from the point it looks at.", new AcceptableValueRange<float>(1f, 50f), new ConfigurationManagerAttributes { Order = 70 }));
        MaxDistance = Config.Bind(camera, "Farthest zoom (m)", 180f,
            new ConfigDescription("Largest camera distance from the point it looks at.", new AcceptableValueRange<float>(50f, 600f), new ConfigurationManagerAttributes { Order = 60 }));
        TiltKey = Config.Bind(camera, "Tilt key", KeyCode.LeftAlt,
            new ConfigDescription("Hold this and use the mouse wheel to change the camera angle instead of zooming.", null, new ConfigurationManagerAttributes { Order = 55 }));
        EdgePan = Config.Bind(camera, "Edge scrolling", true,
            new ConfigDescription("Move the view when the cursor touches the screen edge. Keeps the cursor inside the game window in RTS mode.", null, new ConfigurationManagerAttributes { Order = 50 }));
        CameraCollision = Config.Bind(camera, "Avoid walls", false,
            new ConfigDescription("Pull the camera in front of walls and roofs between it and the point it looks at. Off: the camera passes through buildings.", null, new ConfigurationManagerAttributes { Order = 40 }));

        MaxUnits = Config.Bind(general, "Max spawned units", 40,
            new ConfigDescription("How many bots placed with Perun can be alive at once.", new AcceptableValueRange<int>(1, 200), new ConfigurationManagerAttributes { Order = 70 }));

        PrewarmProfiles = Config.Bind(general, "Prepare bots in advance", true,
            new ConfigDescription("When you pick a bot type in the spawn panel, ask the server for its profiles and load its gear right away, so the click spawns them almost instantly.", null, new ConfigurationManagerAttributes { Order = 60 }));

        BlockHostileMembers = Config.Bind(general, "Refuse hostile group members", false,
            new ConfigDescription("In the group editor, don't offer bot types that are hostile to the group's leader. Off: they can be added, with a warning; inside one group they fight as allies.", null, new ConfigurationManagerAttributes { Order = 55 }));

        const string display = "3. Display";
        ShowLabels = Config.Bind(display, "Unit labels", true,
            new ConfigDescription("Names and roles above characters in RTS mode.", null, new ConfigurationManagerAttributes { Order = 100 }));
        LabelDistance = Config.Bind(display, "Label distance (m)", 300f,
            new ConfigDescription("Labels are drawn for characters closer than this to the camera.", new AcceptableValueRange<float>(25f, 1000f), new ConfigurationManagerAttributes { Order = 90 }));

        SelectionColor = Config.Bind(display, "Selection colour", new Color(0.24f, 1f, 0.43f, 1f),
            new ConfigDescription("Brackets around selected units and the selection box.", null, new ConfigurationManagerAttributes { Order = 85 }));

        (BotKind kind, string name, string hex)[] colors =
        {
            (BotKind.Player, "Colour: players", "#E6E6FF"),
            (BotKind.Usec, "Colour: USEC PMCs", "#6FB3FF"),
            (BotKind.Bear, "Colour: BEAR PMCs", "#FF7A6F"),
            (BotKind.Scav, "Colour: scavs", "#FFD36F"),
            (BotKind.Raider, "Colour: raiders & rogues", "#C792EA"),
            (BotKind.Boss, "Colour: bosses", "#FF3B3B"),
            (BotKind.Follower, "Colour: followers", "#FF9B54"),
            (BotKind.Cultist, "Colour: cultists", "#B39DDB"),
            (BotKind.Infected, "Colour: infected", "#8BE38B"),
            (BotKind.Custom, "Colour: MoreBots factions", "#4DD0E1"),
            (BotKind.Other, "Colour: other bots", "#FFFFFF")
        };
        int colorOrder = 80;
        foreach ((BotKind kind, string name, string hex) in colors)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            ConfigEntry<Color> entry = Config.Bind(display, name, color,
                new ConfigDescription("Label colour in the RTS view.", null, new ConfigurationManagerAttributes { Order = colorOrder--, IsAdvanced = true }));
            entry.SettingChanged += (_, _) => BotKinds.InvalidateColors();
            KindColors[kind] = entry;
        }

        const string fika = "5. Fika game master";
        GameMasterPassword = Config.Bind(fika, "Game master password", "",
            new ConfigDescription("Only for raids with a Fika headless host. On the headless host: players who enter this password can become the game master (one at a time). On your own PC: the password to send to the headless host when you press the RTS key. Leave empty to turn it off.", null, new ConfigurationManagerAttributes { Order = 100 }));
        FikaControl = Config.Bind(fika, "Allow take control in Fika (experimental)", false,
            new ConfigDescription("Lets the Fika host take control of a bot (G) like in single player. Not for a headless host's game master. Experimental: report anything odd other players see.", null, new ConfigurationManagerAttributes { Order = 90 }));

        const string debug = "4. Debug";
        Verbose = Config.Bind(debug, "Verbose logging", false,
            new ConfigDescription("Log every step of entering and leaving RTS mode, plus the game, plugin list and Perun settings once, and each raid's map and Fika role. Arsenal changes log every step with the unit's hands and animator state, and keep watching the unit's hands for 20 s afterwards.", null, new ConfigurationManagerAttributes { Order = 100 }));
    }
}

internal sealed class ConfigurationManagerAttributes
{
    public int? Order;
    public bool? IsAdvanced;
}
