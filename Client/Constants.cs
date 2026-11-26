namespace Perun.Client;

internal static class Constants
{
    internal static class Plugin
    {
        internal const string GuidSptCore = "com.SPT.core";
        internal const string GuidBigBrain = "xyz.drakia.bigbrain";
        internal const string GuidFika = "com.fika.core";
        internal const string GuidSmoothie = "com.tominocz.smoothie";
        internal const string GuidMoreBots = "com.morebotsapi.tacticaltoaster";
        internal const string GuidSain = "me.sol.sain";

        internal const string ConfigSectionGeneral = "1. General";
        internal const string ConfigSectionCamera = "2. Camera";
        internal const string ConfigSectionDisplay = "3. Display";
        internal const string ConfigSectionFikaGameMaster = "5. Fika game master";
        internal const string ConfigSectionDebug = "4. Debug";

        internal const string ConfigKeyToggleRTSMode = "Toggle RTS mode";
        internal const string ConfigKeyTakeControlKey = "Take control key";
        internal const string ConfigKeyArsenalCrateKey = "Arsenal crate key";
        internal const string ConfigKeyReturnAtCursor = "Return at cursor";
        internal const string ConfigKeyPanSpeed = "Pan speed";
        internal const string ConfigKeyRotateSpeed = "Rotate speed";
        internal const string ConfigKeyZoomSpeed = "Zoom speed";
        internal const string ConfigKeyClosestZoom = "Closest zoom (m)";
        internal const string ConfigKeyFarthestZoom = "Farthest zoom (m)";
        internal const string ConfigKeyTiltKey = "Tilt key";
        internal const string ConfigKeyEdgeScrolling = "Edge scrolling";
        internal const string ConfigKeyAvoidWalls = "Avoid walls";
        internal const string ConfigKeyMaxSpawnedUnits = "Max spawned units";
        internal const string ConfigKeyPrepareBotsInAdvance = "Prepare bots in advance";
        internal const string ConfigKeyRefuseHostileGroupMembers = "Refuse hostile group members";
        internal const string ConfigKeyUnitLabels = "Unit labels";
        internal const string ConfigKeyLabelDistance = "Label distance (m)";
        internal const string ConfigKeySelectionColour = "Selection colour";
        internal const string ConfigKeyGameMasterPassword = "Game master password";
        internal const string ConfigKeyAllowTakeControlInFika = "Allow take control in Fika (experimental)";
        internal const string ConfigKeyVerboseLogging = "Verbose logging";
        internal const string ConfigKeyColourPlayer = "Colour: players";
        internal const string ConfigKeyColourUsec = "Colour: USEC PMCs";
        internal const string ConfigKeyColourBear = "Colour: BEAR PMCs";
        internal const string ConfigKeyColourScav = "Colour: scavs";
        internal const string ConfigKeyColourRaider = "Colour: raiders & rogues";
        internal const string ConfigKeyColourBoss = "Colour: bosses";
        internal const string ConfigKeyColourFollower = "Colour: followers";
        internal const string ConfigKeyColourCultist = "Colour: cultists";
        internal const string ConfigKeyColourInfected = "Colour: infected";
        internal const string ConfigKeyColourCustom = "Colour: MoreBots factions";
        internal const string ConfigKeyColourOther = "Colour: other bots";

        internal const string ConfigDescriptionToggleRTSMode = "Switches between your character and the RTS view. In Fika only the host can use it.";
        internal const string ConfigDescriptionTakeControlKey = "In RTS mode: take full control of the selected bot with the game's own controls, HUD and inventory. The RTS toggle key gives it back. Single player only for now.";
        internal const string ConfigDescriptionArsenalCrateKey = "While you control a bot: opens its inventory with the arsenal crate beside it. Fill the crate in the ARSENAL tab (CRATE view). Single player only for now.";
        internal const string ConfigDescriptionReturnAtCursor = "On leaving RTS mode, put your character at the point under the cursor instead of where you left it.";
        internal const string ConfigDescriptionPanSpeed = "WASD / edge pan speed. Scales with zoom; hold Shift for faster.";
        internal const string ConfigDescriptionRotateSpeed = "Middle mouse drag and Q/E rotation speed.";
        internal const string ConfigDescriptionZoomSpeed = "Mouse wheel zoom speed.";
        internal const string ConfigDescriptionClosestZoom = "Smallest camera distance from the point it looks at.";
        internal const string ConfigDescriptionFarthestZoom = "Largest camera distance from the point it looks at.";
        internal const string ConfigDescriptionTiltKey = "Hold this and use the mouse wheel to change the camera angle instead of zooming.";
        internal const string ConfigDescriptionEdgeScrolling = "Move the view when the cursor touches the screen edge. Keeps the cursor inside the game window in RTS mode.";
        internal const string ConfigDescriptionAvoidWalls = "Pull the camera in front of walls and roofs between it and the point it looks at. Off: the camera passes through buildings.";
        internal const string ConfigDescriptionMaxSpawnedUnits = "How many bots placed with Perun can be alive at once.";
        internal const string ConfigDescriptionPrepareBotsInAdvance = "When you pick a bot type in the spawn panel, ask the server for its profiles and load its gear right away, so the click spawns them almost instantly.";
        internal const string ConfigDescriptionRefuseHostileGroupMembers = "In the group editor, don't offer bot types that are hostile to the group's leader. Off: they can be added, with a warning; inside one group they fight as allies.";
        internal const string ConfigDescriptionUnitLabels = "Names and roles above characters in RTS mode.";
        internal const string ConfigDescriptionLabelDistance = "Labels are drawn for characters closer than this to the camera.";
        internal const string ConfigDescriptionSelectionColour = "Brackets around selected units and the selection box.";
        internal const string ConfigDescriptionGameMasterPassword = "Only for raids with a Fika headless host. On the headless host: players who enter this password can become the game master (one at a time). On your own PC: the password to send to the headless host when you press the RTS key. Leave empty to turn it off.";
        internal const string ConfigDescriptionAllowTakeControlInFika = "Lets the Fika host take control of a bot (G) like in single player. Not for a headless host's game master. Experimental: report anything odd other players see.";
        internal const string ConfigDescriptionVerboseLogging = "Log every step of entering and leaving RTS mode, plus the game, plugin list and Perun settings once, and each raid's map and Fika role. Arsenal changes log every step with the unit's hands and animator state, and keep watching the unit's hands for 20 s afterwards.";
        internal const string ConfigDescriptionLabelColour = "Label colour in the RTS view.";

        internal const string ColorPlayer = "#E6E6FF";
        internal const string ColorUsec = "#6FB3FF";
        internal const string ColorBear = "#FF7A6F";
        internal const string ColorScav = "#FFD36F";
        internal const string ColorRaider = "#C792EA";
        internal const string ColorBoss = "#FF3B3B";
        internal const string ColorFollower = "#FF9B54";
        internal const string ColorCultist = "#B39DDB";
        internal const string ColorInfected = "#8BE38B";
        internal const string ColorCustom = "#4DD0E1";
        internal const string ColorOther = "#FFFFFF";
    }
}
