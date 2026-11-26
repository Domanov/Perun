namespace Perun.Client.Arsenal;

internal static class Constants
{
    internal static class ArsenalCatalog
    {
        internal const string LabelWeapons = "Weapons";
        internal const string LabelMelee = "Melee";
        internal const string LabelGrenades = "Grenades";
        internal const string LabelMagazines = "Magazines";
        internal const string LabelAmmo = "Ammo";
        internal const string LabelArmor = "Armor";
        internal const string LabelHeadgear = "Headgear";
        internal const string LabelRigs = "Rigs";
        internal const string LabelBackpacks = "Backpacks";
        internal const string LabelMedical = "Medical";
        internal const string LabelFood = "Food";
        internal const string LabelKeys = "Keys";
        internal const string LabelMods = "Mods";
        internal const string LabelOther = "Other";
    }

    internal static class ArsenalCrate
    {
        internal const string ObjectCrateGrid = "perunArsenalCrate";
        internal const string ObjectCrateController = "Perun arsenal crate";

        internal const string LabelUnknownItem = "unknown item";
        internal const string LabelNoItemFactory = "no item factory yet";
        internal const string LabelNoControlledUnit = "no controlled unit";
        internal const string LabelCouldNotAdd = "couldn't add it: ";
    }

    internal static class ArsenalOps
    {
        internal const string LabelUnitGone = "that unit is gone";
        internal const string LabelNoInventory = "that unit has no inventory";
        internal const string LabelGiveBackFirst = "give the bot back first";
        internal const string LabelUnknownItem = "unknown item";
        internal const string LabelWrongSlot = "{0} doesn't go in that slot";
        internal const string LabelNotRestored = "{0}; couldn't put back {1}";
        internal const string LabelFailedSeeLog = "it failed (see log)";
        internal const string LabelCouldNotTakeOut = "couldn't take out {0}";
        internal const string LabelCouldNotTakeOutError = "couldn't take out {0}: {1}";
        internal const string LabelItemNowhere = "that item isn't anywhere";
        internal const string LabelNoSuchSlot = "no such slot";
        internal const string LabelUnknownError = "unknown error";
        internal const string LabelDroppedNextToUnit = "{0} (dropped next to the unit)";
        internal const string LabelLost = "{0} (lost)";
        internal const string LabelHandsBusy = "the unit's hands are busy";
        internal const string LabelUnitNoSuchSlot = "that unit has no such slot";
        internal const string LabelNoRoomForItem = "no slot or free space for it";
        internal const string LabelCouldNotStage = "couldn't stage the item: {0}";
        internal const string LabelDoesNotFit = "it doesn't fit: {0}";
        internal const string LabelCouldNotPlace = "couldn't place it: {0}";
        internal const string LabelOnlySecureContainer = "only a bot's secure container can be swapped";
        internal const string LabelCouldNotPutIn = "couldn't put in {0}: {1}";
        internal const string LabelNoContainer = "no container";
        internal const string LabelNoRoomInContainers = "no room for it in {0} or the unit's other containers";
        internal const string LabelNothingToRemove = "nothing to remove";
        internal const string LabelCantRemove = "can't remove it: {0}";
        internal const string LabelUnitBusy = "the unit is busy, try again ({0})";

        internal const string ObjectHoldController = "Perun arsenal hold";
        internal const string ObjectArsenalController = "Perun arsenal";

        internal const string DumpOriginalSpot = "its original spot";
        internal const string DumpFreeSpot = "a free spot";
        internal const string DumpOverflow = "another container (overflow)";
    }

    internal static class ArsenalResult
    {
        internal const string LabelRaidEnded = "the raid ended";

        internal const string DumpOk = "ok";
    }

    internal static class ContainerSwap
    {
        internal const string LabelItemsLeftOut = "{0} item(s) didn't fit and were left out: {1}";
        internal const string LabelItemError = "{0} ({1})";
        internal const string LabelNewItemStayed = "{0} (new, stayed in the container)";
        internal const string LabelContentsKept = "{0}; contents left as they were";
        internal const string LabelContentsNotRestored = "{0}; contents not fully restored: {1}";
    }

    internal static class LoadoutRun
    {
        internal const string LabelStoppedUnexpectedly = "stopped unexpectedly";
        internal const string LabelSlotFailed = "{0}: {1}";
        internal const string LabelSlotFailedNotRestored = "{0}: {1}; couldn't put back {2}";
        internal const string LabelSlotError = "{0} ({1})";
        internal const string LabelNotRestored = "{0}; couldn't put back {1}";
        internal const string LabelSlotsKept = "{0}; {1} slot(s) left as they were";
    }

    internal static class Loadouts
    {
        internal const string FileLoadouts = "com.dmnv.perun.loadouts.json";

        internal const string JsonX = "x";
        internal const string JsonY = "y";
        internal const string JsonRotation = "r";

        internal const string LabelNoLoadout = "no loadout";
        internal const string LabelContainerGone = "the container is gone";
        internal const string LabelProgressGear = "{0}: loading gear";
        internal const string LabelProgressTakeOff = "{0}: taking off {1}";
        internal const string LabelProgressStep = "{0}: {1}";
        internal const string LabelProgressAmmo = "{0}: ammo";
        internal const string LabelProgressDraw = "{0}: drawing a weapon";
        internal const string LabelFailure = "{0}: {1}";
        internal const string LabelItemGone = "{0}: its item no longer exists, left as it was";
        internal const string LabelSkippedUnknown = "skipped unknown items {0}";
        internal const string LabelProblems = "{0} problems, first {1}";
    }
}
