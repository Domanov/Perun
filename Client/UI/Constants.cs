namespace Perun.Client.UI;

internal static class Constants
{
    internal static class ArsenalPanel
    {
        internal const string ChipLoadouts = "LOADOUTS";
        internal const string ChipItems = "ITEMS";
        internal const string ChipCrate = "CRATE";
        internal const string ChipSaveCopied = "Save copied";
        internal const string ChipCopy = "Copy {0}";
        internal const string ChipCopySelectOne = "Copy (select one unit)";
        internal const string ChipPasteTo = "Paste to {0}";
        internal const string ChipApplyCount = "Apply ×{0}";
        internal const string ChipApply = "Apply";
        internal const string ChipGiveRandomKit = "Give {0} a random {1} kit";
        internal const string ChipPickRole = "Pick a role in UNITS first";
        internal const string ChipEmpty = "Empty";

        internal const string LabelPrimary = "Primary";
        internal const string LabelSecondary = "Secondary";
        internal const string LabelHolster = "Holster";
        internal const string LabelMelee = "Melee";
        internal const string LabelHeadwear = "Headwear";
        internal const string LabelEarpiece = "Earpiece";
        internal const string LabelFaceCover = "Face cover";
        internal const string LabelEyewear = "Eyewear";
        internal const string LabelArmband = "Armband";
        internal const string LabelBodyArmor = "Body armor";
        internal const string LabelRig = "Rig";
        internal const string LabelBackpack = "Backpack";
        internal const string LabelPockets = "Pockets";
        internal const string LabelSecure = "Secure";
        internal const string LabelArsenalHeader = "ARSENAL";
        internal const string LabelSinglePlayerOnly = "The arsenal works in single player for now.";
        internal const string LabelSavedLoadouts = "SAVED LOADOUTS";
        internal const string LabelNothingSavedYet = "Nothing saved yet. Copy a unit's loadout, name it and save it.";
        internal const string LabelRandomKitFrom = "RANDOM KIT FROM A ROLE";
        internal const string LabelWorking = "<color=#ffc847>Working… {0}</color>";
        internal const string LabelCrateHint = "Click items to put them in the crate. Take control of a bot ({0}) and press {1} to open its inventory with the crate beside it, then drag and drop. Anything dropped back in the crate leaves the bot.";
        internal const string LabelCrateHeader = "ARSENAL CRATE · {0} item(s)";
        internal const string LabelOneUnit = "1 unit";
        internal const string LabelUnits = "{0} units";
        internal const string LabelLoadoutsSelectUnits = "LOADOUTS · select units";
        internal const string LabelLoadoutsSelected = "LOADOUTS · {0} selected";
        internal const string LabelCopiedSlots = "Copied: {0} ({1} slots)";
        internal const string LabelNothingCopiedYet = "Nothing copied yet";
        internal const string LabelUnitLoadout = "{0}'s loadout";
        internal const string LabelCouldNotReadGear = "<color=#ff8a7a>Couldn't read that unit's gear</color>";
        internal const string LabelCopiedLoadout = "<color=#8be38b>Copied {0}'s loadout</color>";
        internal const string LabelSaved = "<color=#8be38b>Saved \"{0}\"</color>";
        internal const string LabelDeleted = "Deleted \"{0}\"";
        internal const string LabelKit = "{0} kit";
        internal const string LabelAskingKits = "asking the server for {0} kits";
        internal const string LabelNoKits = "the server sent no kits for that role";
        internal const string LabelFailedSeeLog = "it failed (see log)";
        internal const string LabelAppliedNotRestored = "<b><color=#ff5a4a>Applied {0} on {1}/{2} unit(s). Gear not put back: {3}</color></b>";
        internal const string LabelAppliedOn = "{0} on {1}/{2} unit(s)";
        internal const string LabelApplied = "<color=#8be38b>Applied {0}</color>";
        internal const string LabelAppliedWithProblems = "<color=#ff8a7a>Applied {0}. {1}</color>";
        internal const string LabelSelectOneUnit = "Select one unit (a bot, or click a player) to edit its gear.";
        internal const string LabelWorkingPlain = "<color=#ffc847>Working…</color>";
        internal const string LabelSearchItems = "Search items…";
        internal const string LabelNothingMatches = "Nothing matches.";
        internal const string LabelCrateEmptied = "Crate emptied";
        internal const string LabelAddToCrate = "ADD TO THE CRATE";
        internal const string LabelCannotCreate = "<color=#ff8a7a>Can't create {0}</color>";
        internal const string LabelAddedToCrate = "Added {0} to the crate";
        internal const string LabelArsenalUnitHeader = "ARSENAL · {0}";
        internal const string LabelEmptySlot = "<color=#5d6873>empty</color>";
        internal const string LabelAddToInventory = "ADD TO INVENTORY";
        internal const string LabelPutIn = "PUT IN {0}  <color=#7f8a95>(click it again for inventory)</color>";
        internal const string LabelGave = "Gave {0}";
        internal const string LabelCleared = "Cleared {0}";

        internal const string ControlSearch = "PerunArsenalSearch";

        internal const string ControlName = "PerunLoadoutName";
    }

    internal static class BotKinds
    {
        internal const string MatchInfected = "infected";
        internal const string MatchSectant = "sectant";
        internal const string MatchSectantPriestEvent = "sectactPriestEvent";
        internal const string MatchFollower = "follower";
        internal const string MatchTagillaHelperAgro = "tagillaHelperAgro";
        internal const string MatchBoss = "boss";
    }

    internal static class EntityPanel
    {
        internal const string LabelPlayers = "Players";
        internal const string LabelUsec = "USEC";
        internal const string LabelBear = "BEAR";
        internal const string LabelScavs = "Scavs";
        internal const string LabelRaidersRogues = "Raiders & rogues";
        internal const string LabelBosses = "Bosses";
        internal const string LabelFollowers = "Followers";
        internal const string LabelCultists = "Cultists";
        internal const string LabelInfected = "Infected";
        internal const string LabelFactions = "Factions";
        internal const string LabelOther = "Other";
        internal const string LabelMine = "★ Mine";
        internal const string LabelNoPerunUnits = "No Perun units yet.";
        internal const string LabelNobodyHere = "Nobody here.";
        internal const string LabelHint = "Click select · Shift add · double-click fly to · Shift+click group selects all";
        internal const string LabelEntities = "ENTITIES  <color=#9fb4c8>{0}</color>";
        internal const string LabelYou = "  <color=#7f8a95>(you)</color>";

        internal const string MarkupColorOpen = "<color=";
    }

    internal static class GroupEditor
    {
        internal const string ChipAuto = "AUTO";
        internal const string ChipManual = "MANUAL";
        internal const string ChipDelete = "Delete";
        internal const string ChipCancel = "Cancel";
        internal const string ChipSave = "Save";
        internal const string ChipDiscard = "Discard";
        internal const string ChipSaveAs = "Save as";
        internal const string ChipClose = "Close";
        internal const string ChipPlaceGroup = "PLACE GROUP";

        internal const string LabelEasy = "Easy";
        internal const string LabelNormal = "Normal";
        internal const string LabelHard = "Hard";
        internal const string LabelImpossible = "Impossible";
        internal const string LabelGroupName = "Group name…";
        internal const string LabelLeader = "Leader";
        internal const string LabelAddTypesHint = "<color=#9fb4c8>Add types from the list on the right.</color>";
        internal const string LabelAddType = "ADD TYPE";
        internal const string LabelSearchTypes = "Search types…";
        internal const string LabelNothingMatches = "Nothing matches.";
        internal const string LabelHostileBlocked = "<color=#7f8a95>Hostile types are blocked by the setting.</color>";
        internal const string LabelNewGroup = "NEW GROUP";
        internal const string LabelGroupEditorUnsaved = "GROUP EDITOR · {0} <color=#7f8a95>· not saved</color>";
        internal const string LabelGroupEditor = "GROUP EDITOR · {0}";
        internal const string LabelMembersMax = "MEMBERS ({0} / max {1})";
        internal const string LabelHostileMembers = "⚠ {0} member type(s) can be hostile to {1}. Inside one group they fight as allies; other groups see each by its own role.";
        internal const string LabelOverUnitLimit = "⚠ {0} members, but only {1} Perun units can be alive at once; the rest won't spawn.";
        internal const string LabelConfirmDelete = "Delete \"{0}\"?";
        internal const string LabelUnsavedChanges = "Unsaved changes.";

        internal const string ControlName = "PerunGroupName";

        internal const string ControlSearch = "PerunGroupAddSearch";
    }

    internal static class GroupsPanel
    {
        internal const string LabelAsSet = "As set";
        internal const string LabelEasy = "Easy";
        internal const string LabelNormal = "Normal";
        internal const string LabelHard = "Hard";
        internal const string LabelImpossibleShort = "Imposs.";
        internal const string LabelGroupHeader = "GROUP";
        internal const string LabelSearchGroups = "Search groups…";
        internal const string LabelNoGroups = "No groups: the server sent no boss data and nothing is saved.";
        internal const string LabelNothingMatches = "Nothing matches.";
        internal const string LabelDifficulty = "DIFFICULTY";
        internal const string LabelCopySuffix = " (copy)";
        internal const string LabelSptPresets = "SPT PRESETS";
        internal const string LabelCustom = "CUSTOM";
        internal const string LabelAlone = "alone";
        internal const string LabelMoreKinds = "+ {0} kinds";
        internal const string LabelSpawning = " · spawning {0}";
        internal const string LabelPerunUnits = "Perun units {0}/{1}{2}";
        internal const string LabelPlaceFromEditor = "<color=#9fb4c8>Place the group from the editor, or close it to place from here.</color>";
        internal const string LabelLinkHint = "<color=#ffc847>Drag from a bot onto another: the second one leads the first. Drop it on the ground: it leaves its group</color>  <color=#9fb4c8>· LINK LEADERS again stops</color>";
        internal const string LabelPickAGroup = "<color=#9fb4c8>Pick a group, then click the map to place it.</color>";
        internal const string LabelClickTheMap = "<color=#ffc847>Click the map to place {0} ({1})</color>  <color=#9fb4c8>· RMB stops</color>";
        internal const string LabelPlaceGroupHint = "<color=#9fb4c8>PLACE GROUP to place it on the map.</color>";
        internal const string LabelHostLoads = "<color=#9fb4c8>The host loads the members when you place the group</color>";
        internal const string LabelReadyToDrop = "<color=#8be38b>● Ready to drop: {0}</color> · spawns instantly";
        internal const string LabelPreparingInAdvance = "<color=#ffc847>◐ Preparing in advance… {0}/{1} ready</color>";
        internal const string LabelOverUnitLimit = "\n<color=#ff8080>Only {0} more units fit (limit {1})</color>";
        internal const string LabelNothingSelected = "<color=#7f8a95>Nothing selected.</color>";
        internal const string LabelCustomLeader = "\n<color=#7f8a95>Custom · leader picked by hand</color>";
        internal const string LabelCustomLine = "\n<color=#7f8a95>Custom</color>";
        internal const string LabelSptSource = "SPT";

        internal const string ChipPlaceGroup = "PLACE GROUP";
        internal const string ChipEdit = "Edit";
        internal const string ChipEditCopy = "Edit copy";
        internal const string ChipNewGroup = "New group";
        internal const string ChipLinkLeaders = "LINK LEADERS";

        internal const string MarkupColorOpen = "<color=";

        internal const string ControlSearch = "PerunGroupSearch";
    }

    internal static class PerunHud
    {
        internal const string ChipReturn = "RETURN  ({0})";
        internal const string ChipControl = "Control ({0})";
        internal const string ChipResume = "▶  RESUME  (P)";
        internal const string ChipPause = "❚❚  PAUSE  (P)";
        internal const string ChipHide = "HIDE  (H)";
        internal const string ChipHelp = "HELP  (F1)";
        internal const string ChipFlyTo = "Fly to";
        internal const string ChipLockCam = "Lock cam";
        internal const string ChipHeal = "Heal";
        internal const string ChipTeleport = "Teleport";
        internal const string ChipHalt = "Halt";
        internal const string ChipDeselect = "Deselect";
        internal const string ChipRemove = "Remove";
        internal const string ChipKill = "Kill";
        internal const string ChipUnits = "UNITS  (B)";
        internal const string ChipEnvironment = "ENV  (T)";
        internal const string ChipArsenal = "ARSENAL  (I)";
        internal const string ChipCloseHelp = "Close  (F1)";

        internal const string LabelHelpHint = "<color=#7f8a95>F1 help · H hide · {0} return</color>";
        internal const string LabelTitle = "PERUN";
        internal const string LabelControls = "CONTROLS";
        internal const string LabelABot = "a bot";
        internal const string LabelControlBar = "<color=#ffc847><b>PERUN</b></color>  controlling {0}  ·  <b>{1}</b> gives it back  ·  <b>{2}</b> arsenal crate";
        internal const string LabelMoreQueued = "+{0} more queued";
        internal const string LabelQueued = "<color=#9fb4c8>queued</color>";
        internal const string LabelFetchingProfilesFrom = "<color=#ffc847>fetching profiles from the server…</color>";
        internal const string LabelLoadingGear = "<color=#ffc847>loading gear</color> {0}%";
        internal const string LabelSpawning = "<color=#8be38b>spawning</color> {0}/{1}";
        internal const string LabelDone = "done";
        internal const string LabelInAdvance = "<color=#c8d2dc>{0}</color> <color=#7f8a95>(in advance)</color>";
        internal const string LabelReady = "+ {0} ×{1}  <color=#8be38b>ready</color>";
        internal const string LabelNeedsLoadingFirst = "+ {0} ×{1}  <color=#ffc847>needs loading first</color>";
        internal const string LabelGroupReady = "+ {0} ({1}) · {2:0.0} m  <color=#8be38b>ready</color>";
        internal const string LabelGroupNeedsLoading = "+ {0} ({1}) · {2:0.0} m  <color=#ffc847>needs loading first</color>";
        internal const string LabelCursorHeader = "<color=#7f8a95>CURSOR</color>  ";
        internal const string LabelNoNavmesh = "  <color=#ff8080>no navmesh</color>";
        internal const string LabelSelectedHeader = "     <color=#7f8a95>SELECTED</color>  ";
        internal const string LabelUnitsSuffix = " units";
        internal const string LabelCameraLockedOn = "     <color=#ffd36f>camera locked on ";
        internal const string LabelLockRelease = " · L or WASD releases</color>";
        internal const string LabelPlacingGroupClick = "<color=#ffc847>Placing group: click · drag to turn · Ctrl+wheel spread · RMB cancels</color>";
        internal const string LabelPlacingClickThe = "<color=#ffc847>Placing: click the map · drag to turn · RMB cancels</color>";
        internal const string LabelTimeHeader = "<color=#7f8a95>TIME</color>  ";
        internal const string LabelUnitsHeader = "<color=#7f8a95>UNITS</color>  ";
        internal const string LabelSpawningTail = " spawning</color>";
        internal const string LabelWorldPaused = "     <color=#ffc847><b>WORLD PAUSED</b></color>";
        internal const string LabelPlayer = "Player";
        internal const string LabelHealthSuffix = " health";
        internal const string LabelForcedMove = "Forced move";
        internal const string LabelRunning = "Running";
        internal const string LabelMoving = "Moving";
        internal const string LabelMetersToGo = " m to go";
        internal const string LabelNoOrders = "\nNo orders";
        internal const string LabelUnitsSelected = " UNITS SELECTED";
        internal const string LabelYoursSuffix = " yours · ";
        internal const string LabelWithOrdersHint = " with orders\nRMB on the map to move them";
        internal const string LabelYouReturnHere = "you return here";
        internal const string LabelReturnHere = "return here";
        internal const string LabelYouSuffix = " (you)";
        internal const string LabelPlayerSuffix = " (player)";

        internal const string MarkupColorOpen = "<color=";

        internal const string LabelPausedHost = "<size=20><b>PAUSED</b></size>  <color=#9fb4c8>P to resume</color>";
        internal const string LabelPausedClient = "<size=20><b>PAUSED</b></size>  <color=#9fb4c8>by the game master</color>";
        internal const string LabelCantPlace = "<color=#ff8080>can't place here</color>";
        internal const string LabelHelp =
            "<b><color=#ffc847>CAMERA</color></b>\n" +
            "WASD pan · Shift faster · Q/E or middle-drag rotate · wheel zoom · {0}+wheel tilt · R/F floor up/down\n" +
            "Home back to your body · click the magenta dot to select yourself (Heal, Kill with a confirm), then the map to change where you return · L lock camera on the selected unit · Ctrl+F fly to the selection\n\n" +
            "<b><color=#ffc847>UNITS</color></b>\n" +
            "LMB select · drag a box · Shift adds · click empty ground clears\n" +
            "RMB move · Shift+RMB run · Ctrl+RMB forced move (ignores enemies) · X halt\n" +
            "Del remove · Ctrl+Del kill · Heal on the card (click a player to select them) · Alt+right-click teleport (or Teleport on the card, then click)\n\n" +
            "<b><color=#ffc847>EDITOR</color></b>\n" +
            "B units browser · T environment · I arsenal (gear of the selected unit) · N vision (night green, white, amber, then thermal white hot, black hot, fusion, rainbow, off) · P pause the world · H hide the interface · F1 this help · {1} leave\n\n" +
            "<b><color=#ffc847>TAKE CONTROL</color></b>\n" +
            "Select one bot and press {2} (or Control on the card) to play it with the game's own controls, HUD and inventory · {1} gives it back";
    }

    internal static class RoleCatalog
    {
        internal const string LabelScavs = "Scavs";
        internal const string LabelPmc = "PMC";
        internal const string LabelRaiders = "Raiders";
        internal const string LabelBosses = "Bosses";
        internal const string LabelFollowers = "Followers";
        internal const string LabelCultists = "Cultists";
        internal const string LabelInfected = "Infected";
        internal const string LabelOther = "Other";

        internal const string MatchBoss = "boss";
        internal const string MatchFollower = "follower";
        internal const string MatchTagillaHelperAgro = "tagillaHelperAgro";
        internal const string MatchSectant = "sectant";
        internal const string MatchSectantPriestEvent = "sectactPriestEvent";
        internal const string MatchInfected = "infected";

        internal const string MarkupCategoryOpen = "  <color=#7f8a95>";
        internal const string MarkupColorClose = "</color>";
    }

    internal static class SpawnPanel
    {
        internal const string LabelEasy = "Easy";
        internal const string LabelNormal = "Normal";
        internal const string LabelHard = "Hard";
        internal const string LabelImpossible = "Impossible";
        internal const string LabelDifficulty = "Difficulty";
        internal const string LabelFaction = "FACTION";
        internal const string LabelSearchAllUnits = "Search all units…";
        internal const string LabelNothingMatches = "Nothing matches.";
        internal const string LabelCount = "COUNT";
        internal const string LabelSpawning = " · spawning {0}";
        internal const string LabelPerunUnits = "Perun units {0}/{1}{2}";
        internal const string LabelDifficultyHeader = "DIFFICULTY";
        internal const string LabelPickAUnit = "<color=#9fb4c8>Pick a unit, then click the map to place it.</color>";
        internal const string LabelClickTheMap = "<color=#ffc847>Click the map to place {0} ×{1}</color>  <color=#9fb4c8>· RMB stops</color>";
        internal const string LabelReadyToDrop = "<color=#8be38b>● Ready to drop: {0}</color> · spawns instantly";
        internal const string LabelPreparingInAdvance = "<color=#ffc847>◐ Preparing in advance… {0}/{1} ready</color>";
        internal const string LabelReadyTheRest = "<color=#ffc847>● {0}/{1} ready</color> · the rest loads when you place them";
        internal const string LabelBadgesAttitude = "\n<color=#7f8a95>Badges: attitude toward {0}</color>";
        internal const string LabelOnlyCount = "  <color=#ffd36f>(only {0})</color>";

        internal const string ChipBots = "BOTS";
        internal const string ChipGroups = "GROUPS";

        internal const string ControlSearch = "PerunSearch";
    }

    internal static class WeatherPanel
    {
        internal const string LabelWindE = "E";
        internal const string LabelWindN = "N";
        internal const string LabelWindW = "W";
        internal const string LabelWindS = "S";
        internal const string LabelDawn = "Dawn";
        internal const string LabelMorning = "Morning";
        internal const string LabelNoon = "Noon";
        internal const string LabelDusk = "Dusk";
        internal const string LabelNight = "Night";
        internal const string LabelDeepNight = "Deep night";
        internal const string LabelWindSe = "SE";
        internal const string LabelWindSw = "SW";
        internal const string LabelWindNw = "NW";
        internal const string LabelWindNe = "NE";
        internal const string LabelSavedWeather = "SAVED WEATHER";
        internal const string LabelNothingSaved = "Nothing saved yet. Save the weather you like, then bring it back any time, in any raid.";
        internal const string LabelStorm = "Storm";
        internal const string LabelRain = "Rain";
        internal const string LabelFog = "Fog";
        internal const string LabelCloudy = "Cloudy";
        internal const string LabelClear = "Clear";
        internal const string LabelTime = "TIME";
        internal const string LabelNoRaidClock = "No raid clock.";
        internal const string LabelClockNow = "Now <b>{0}</b> · speed {1}";
        internal const string LabelClockHint = "Speed 0 stops the clock. Raid default is ×7.";
        internal const string LabelWeather = "WEATHER";
        internal const string LabelNoWeatherOn = "No weather on this map.";
        internal const string LabelCustomWeatherOn = "Custom weather: ON";
        internal const string LabelCustomWeatherOff = "Custom weather: OFF (natural forecast)";
        internal const string LabelNatural = "Natural";
        internal const string LabelClouds = "Clouds";
        internal const string LabelLightning = "Lightning";
        internal const string LabelWind = "Wind";
        internal const string LabelFrom = "From";
        internal const string LabelTemp = "Temp";

        internal const string ChipSaveCustom = "Save the custom weather";
        internal const string ChipSaveCurrent = "Save the current weather";
        internal const string ChipApply = "Apply";
        internal const string ChipApplyTime = "Apply + time";
        internal const string ChipSet = "Set";

        internal const string LocationCustoms = "bigmap";
        internal const string LocationFactoryDay = "factory4_day";
        internal const string LocationFactoryNight = "factory4_night";
        internal const string LocationInterchange = "interchange";
        internal const string LocationLabs = "laboratory";
        internal const string LocationLighthouse = "lighthouse";
        internal const string LocationReserve = "rezervbase";
        internal const string LocationShoreline = "shoreline";
        internal const string LocationStreets = "tarkovstreets";
        internal const string LocationWoods = "woods";
        internal const string LocationGroundZero = "sandbox";
        internal const string LocationGroundZeroHigh = "sandbox_high";
        internal const string LocationLabyrinth = "labyrinth";

        internal const string NameMapCustoms = "Customs";
        internal const string NameMapFactory = "Factory";
        internal const string NameMapNightFactory = "Night Factory";
        internal const string NameMapInterchange = "Interchange";
        internal const string NameMapLabs = "Labs";
        internal const string NameMapLighthouse = "Lighthouse";
        internal const string NameMapReserve = "Reserve";
        internal const string NameMapShoreline = "Shoreline";
        internal const string NameMapStreets = "Streets";
        internal const string NameMapWoods = "Woods";
        internal const string NameMapGroundZero = "Ground Zero";
        internal const string NameMapLabyrinth = "Labyrinth";
        internal const string NameMapRaid = "Raid";
    }

    internal static class WeatherSnapshots
    {
        internal const string FileWeather = "com.dmnv.perun.weather.json";

        internal const string LabelSummary = "Clouds {0:0}% · Fog {1:0}% · Rain {2:0}% · Lightning {3:0}%\nWind {4:0}% from {5} · {6:0} °C · time {7}";
    }
}
