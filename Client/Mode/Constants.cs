namespace Perun.Client.Mode;

internal static class Constants
{
    internal static class ControlAudio
    {
        internal const string FieldTinnitusEnd = "float_2";
        internal const string FieldNestedStepSoundSource = "NestedStepSoundSource";
        internal const string FieldAudioRoomStorage = "_audioRoomStorage";
        internal const string FieldListenerIndoorRooms = "list_3";
        internal const string FieldListenerOutdoorRooms = "list_4";
        internal const string FieldPlayerCurrentIndoorRooms = "_playerCurrentIndoorRooms";
        internal const string FieldPlayerCurrentOutdoorRooms = "_playerCurrentOutdoorRooms";
    }

    internal static class ControlScreenEffects
    {
        internal const string FieldPlayer = "player";
        internal const string FieldDeathFade = "_deathFade";
        internal const string FieldBlur = "_blur";
        internal const string FieldEffectAccumulators = "_effectAccumulators";
        internal const string FieldBloodOnScreen = "_bloodOnScreen";
        internal const string FieldEyeBurn = "_eyeBurn";
        internal const string FieldStimulatorBuffs = "_stimulatorBuffs";
    }

    internal static class PerunMode
    {
        internal const string LabelForcedMove = "forced move";
        internal const string LabelRun = "run";
        internal const string LabelMove = "move";
        internal const string LabelLeavesGroup = "{0} leaves its group";
        internal const string LabelStays = "{0} stays: {1}";
        internal const string LabelOnlyBotsLinked = "Only bots can be linked";
        internal const string LabelCantLink = "Can't link: {0}";
        internal const string LabelFollows = "{0} follows {1}";
        internal const string LabelInterfaceHidden = "Interface hidden · H shows it again";
        internal const string LabelPickReturn = "Click where your character should return · right-click cancels";
        internal const string LabelSelectUnitsFirst = "Select units first";
        internal const string LabelPickTeleport = "Click where to teleport the selection · right-click cancels";
        internal const string LabelCantTeleport = "Can't teleport there";
        internal const string LabelReturnHere = "Your character will return here";
        internal const string LabelNothingToTeleport = "Nothing to teleport";
        internal const string LabelTeleported = "Teleported {0} unit(s){1}";
        internal const string LabelControlUnsupported = "Taking control doesn't work with this game version (see the log)";
        internal const string LabelFikaControlHostOnly = "Taking control in Fika: only the host, with \"Allow take control in Fika\" on (F12)";
        internal const string LabelControlUnavailable = "Taking control of a bot isn't available here";
        internal const string LabelResumeFirst = "Resume the world first (P)";
        internal const string LabelSelectOneBot = "Select one bot first";
        internal const string LabelOrbitAsleep = "ORBIT has {0} asleep; it wakes when the camera gets close, then try again";
        internal const string LabelLoadingArms = "Loading {0}'s arms…";
        internal const string LabelCantControl = "That bot can't be controlled";
        internal const string LabelBusy = "{0} is busy, try again";
        internal const string LabelCantTakeControl = "Can't take control: {0}";
        internal const string LabelYouControl = "You control {0} · {1} gives it back";
        internal const string LabelCrateSinglePlayer = "The arsenal crate works in single player for now";
        internal const string LabelCantOpenCrate = "Can't open the arsenal crate: {0}";
        internal const string LabelCrateEmpty = "The arsenal crate is empty: fill it in the ARSENAL tab (CRATE view)";
        internal const string LabelCrateFailed = "Can't open the arsenal crate (see log)";
        internal const string LabelControlEndedDied = "Control ended: the bot died";
        internal const string LabelControlEndedPaused = "Control ended: the world was paused";
        internal const string LabelGiveBackFailed = "Giving the bot back failed ({0}); back to your character";
        internal const string LabelBackToAi = "{0} is back to its AI";
        internal const string LabelBackToAiFailed = "{0} is back to its AI, but {1} cleanup step(s) failed (see the log)";
        internal const string LabelNobodyToHeal = "Nobody to heal";
        internal const string LabelHealed = "Healed {0} unit(s){1}";
        internal const string LabelHalted = "Halted {0} unit(s){1}";
        internal const string LabelConfirmKillSelf = "Kill your own character? Press Kill again to confirm · it ends your raid";
        internal const string LabelCantRemoveSelf = "Your character can't be removed";
        internal const string LabelCantReturnThere = "Your character can't return there";
        internal const string LabelHostTooOld = "The host's Perun is too old to spawn groups for a game master";
        internal const string LabelCantMoveThere = "Can't move there";
        internal const string LabelSelectBotsFirst = "Select bots first";
        internal const string LabelOrdered = "{0} unit(s): {1}{2}";
        internal const string LabelReturnUnchanged = "Return point unchanged";
        internal const string LabelTeleportCancelled = "Teleport cancelled";
        internal const string LabelNoLivingCharacter = "Perun: no living character";
        internal const string LabelHostOnly = "Perun is for the Fika host only (with a headless host: set the game master password in F12)";
        internal const string LabelBridgeMissing = "Perun.Fika.dll is missing, so Perun can't reach the host";
        internal const string LabelHostNotConfirmed = "The host hasn't confirmed Perun yet, or runs a Perun without game master support";
        internal const string LabelAskingGameMaster = "Asking the host to make you game master…";
        internal const string LabelHostSaidNo = "The host said no";
        internal const string LabelYouAreGameMaster = "You are the game master";
        internal const string LabelGameMasterEnded = "Game master ended: {0}";
        internal const string LabelNoGameCamera = "Perun: no game camera";
        internal const string LabelWaitSpectator = "Wait until Fika's spectator camera is on, then try again";
        internal const string LabelBtrSkipped = " · BTR skipped";
        internal const string LabelBtrWatchOnly = "The BTR can only be watched (L locks the camera)";
    }

    internal static class Reflect
    {
        internal const string LabelNoControl = "taking control of bots is switched off";
        internal const string LabelEffectsCarryOver = "pain, bleeding and death effects and ear ringing may carry over between you and a controlled bot";
        internal const string LabelSoundsMuffled = "a controlled bot may hear its own steps and other sounds muffled, as if from where your character stood";
        internal const string LabelTimerRuns = "the raid timer keeps running during a world pause";
        internal const string LabelHandsFreeze = "with Smoothie installed, a controlled bot's hands may freeze";
        internal const string LabelTeleportWalkBack = "teleported bots may walk back towards their old spot";
    }

    internal static class Takeover
    {
        internal const string FieldMyPlayer = "_myPlayer";
        internal const string FieldInsurance = "_insurance";
        internal const string FieldGameDateTime = "_gameDateTime";
        internal const string FieldLocation = "_location";
        internal const string FieldBattleUiScreenController = "BattleUIScreenController";
        internal const string FieldRemoveItemEvent = "_removeItemEvent";
        internal const string FieldAction = "_action";

        internal const string MethodShowScreen = "ShowScreen";
        internal const string MethodOwnerCreate = "EftGamePlayerOwner.Create<T>";

        internal const string LabelUnsupportedVersion = "this game version isn't supported for taking control";
        internal const string LabelNoControls = "your character's controls weren't found";
        internal const string LabelInfected = "infected can't be controlled";
        internal const string LabelInterfaceNotReady = "the raid interface isn't ready";
        internal const string LabelNoCamera = "your character's camera wasn't found";
        internal const string LabelNoArms = "no first-person arms could be found for that bot";
        internal const string LabelTakeFailed = "taking control failed (see log)";
        internal const string LabelStepYourPlayer = "hand 'your player' back";
        internal const string LabelStepStopControls = "stop the bot's controls";
        internal const string LabelStepRemoveCamera = "remove the bot's camera";
        internal const string LabelStepClearDeath = "clear the bot's death from the screen";
        internal const string LabelStepGiveBack = "give the bot back to its AI";
        internal const string LabelStepThirdPerson = "return the bot to third person";
        internal const string LabelStepHideHud = "hide the raid HUD again";
        internal const string LabelStepControlsBack = "give your character its controls back";
        internal const string LabelStepSound = "match the sound to your character's surroundings";

        internal const string DumpNewHands = "new hands while controlled";
    }

    internal static class WeaponSwitch
    {
        internal const string MatchFika = "Fika";

        internal const string DumpLowering = "lowering";
        internal const string DumpDrawing = "drawing";
    }

    internal static class WorldPause
    {
        internal const string FieldDateTime = "_dateTime";
    }
}
