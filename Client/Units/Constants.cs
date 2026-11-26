namespace Perun.Client.Units;

internal static class Constants
{
    internal static class BossPresets
    {
        internal const string NameReshala = "Reshala";
        internal const string NameShturman = "Shturman";
        internal const string NameGlukhar = "Glukhar";
        internal const string NameSanitar = "Sanitar";
        internal const string NameKilla = "Killa";
        internal const string NameTagilla = "Tagilla";
        internal const string NameGoons = "Goons";
        internal const string NameZryachiy = "Zryachiy";
        internal const string NameKaban = "Kaban";
        internal const string NameKabanSnipers = "Kaban's snipers";
        internal const string NameKollontay = "Kollontay";
        internal const string NamePartisan = "Partisan";
        internal const string NameKillaLabyrinth = "Killa (Labyrinth)";
        internal const string NameTagillaLabyrinth = "Tagilla (Labyrinth)";
        internal const string NameLabyrinthShadows = "Labyrinth shadows";
        internal const string NameRaiders = "Raiders";
        internal const string NameRogues = "Rogues";
        internal const string NameCultists = "Cultists";
        internal const string NamePeacemakers = "Peacemakers";
        internal const string NameCrazyScavs = "Crazy scavs";
        internal const string NameBloodhounds = "Bloodhounds";
        internal const string NameMapCustoms = "Customs";
        internal const string NameMapFactory = "Factory";
        internal const string NameMapNightFactory = "Night Factory";
        internal const string NameMapInterchange = "Interchange";
        internal const string NameMapLabs = "Labs";
        internal const string NameMapLighthouse = "Lighthouse";
        internal const string NameMapReserve = "Reserve";
        internal const string NameMapShoreline = "Shoreline";
        internal const string NameMapWoods = "Woods";
        internal const string NameMapStreets = "Streets";
        internal const string NameMapGroundZero = "Ground Zero";
        internal const string NameMapLabyrinth = "Labyrinth";

        internal const string LocationCustoms = "bigmap";
        internal const string LocationFactoryDay = "factory4_day";
        internal const string LocationFactoryNight = "factory4_night";
        internal const string LocationInterchange = "interchange";
        internal const string LocationLabs = "laboratory";
        internal const string LocationLighthouse = "lighthouse";
        internal const string LocationReserve = "rezervbase";
        internal const string LocationShoreline = "shoreline";
        internal const string LocationWoods = "woods";
        internal const string LocationStreets = "tarkovstreets";
        internal const string LocationGroundZero = "sandbox";
        internal const string LocationGroundZeroHigh = "sandbox_high";
        internal const string LocationLabyrinth = "labyrinth";

        internal const string KeyFactionPrefix = "faction|";
    }

    internal static class CustomGroups
    {
        internal const string FileGroups = "com.dmnv.perun.groups.json";
        internal const string FileBadSuffix = ".bad";

        internal const string LabelNeedMember = "Add at least one member";
        internal const string LabelTooManyGroups = "Too many saved groups ({0}); delete one first";
        internal const string LabelNeedName = "Give the group a name";
        internal const string LabelNameTooLong = "Name is longer than {0} characters";
        internal const string LabelNameTaken = "A group named \"{0}\" already exists";
        internal const string LabelGroupSkipped = "Group \"{0}\": unnamed or duplicate, skipped";
        internal const string LabelRoleSkipped = "Group \"{0}\": unknown role \"{1}\" skipped";
        internal const string LabelNoMembersSkipped = "Group \"{0}\": no members left, skipped";
        internal const string LabelCouldNotWrite = "Couldn't write {0}: {1}";
    }

    internal static class GroupLeadership
    {
        internal const string LabelNotLivingBots = "both units must be living bots";
        internal const string LabelNotActive = "both bots must be active";
        internal const string LabelNotLivingActiveBot = "it must be a living, active bot";
        internal const string LabelAlreadyAlone = "it is already on its own";
        internal const string LabelNoBotSystem = "no bot system in this raid";
        internal const string LabelLeaderDied = "leader died";
        internal const string LabelLeaderLeft = "leader left";

        internal const string FieldOnFollowerStatusChange = "OnFollowerStatusChange";
        internal const string FieldHaveGoodCovers = "HaveGoodCovers";
        internal const string FieldFollowersPositions = "_followersPositions";

        internal const string MatchBossWithStayFollowers = "BossWithStayFollowersPosition";
    }

    internal static class GroupPlan
    {
        internal const string MatchBoss = "boss";
    }

    internal static class HostilityRules
    {
        internal const string ColorAlly = "#8be38b";
        internal const string ColorNeutral = "#9fb4c8";
        internal const string ColorWarns = "#ffd36f";
        internal const string ColorChance = "#ffa04d";
        internal const string ColorEnemy = "#ff8080";

        internal const string LabelAlly = "ally";
        internal const string LabelNeutral = "neutral";
        internal const string LabelWarns = "warns";
        internal const string LabelHostileChance = "hostile ";
        internal const string LabelHostile = "hostile";

        internal const string MarkupColorOpen = "<color=";
        internal const string MarkupTagEnd = ">";
        internal const string MarkupColorClose = "</color>";
    }

    internal static class PerunProfileData
    {
        internal const string DumpPerun = "Perun";
    }

    internal static class RemoteSession
    {
        internal const string LabelAskedSpawn = "Asked the host for {0} ×{1}…";
        internal const string LabelAskedGroup = "Asked the host for {0}…";
        internal const string LabelGroupTooLarge = "{0} can't be sent to the host (at most {1} member types)";
    }

    internal static class SpawnService
    {
        internal const string NameSpawnPoint = "Perun";
        internal const string NameSpawnPointPrefix = "perun_";

        internal const string FieldLastAggressor = "LastAggressor";

        internal const string LabelNoBotSystem = "No bot system in this raid";
        internal const string LabelNoBotZone = "No bot zone on this map";
        internal const string LabelOnlyBotsRemoved = "Only bots can be removed";
        internal const string LabelUnitLimit = "Unit limit reached ({0})";
        internal const string LabelPreparing = "Preparing {0} ×{1}";
        internal const string LabelCount = "{0} ×{1}";
        internal const string LabelSpawning = "Spawning {0}";
        internal const string LabelSpawningProgress = "Spawning {0}…";
        internal const string LabelLoadingFromServer = "Loading {0} ×{1} from the server…";
        internal const string LabelNoProfile = "The server sent no {0} profile";
        internal const string LabelNoLeaderProfile = "The server sent no {0} profile for the leader";
        internal const string LabelCreating = "Creating {0}…";
        internal const string LabelSpawnedCount = "Spawned {0} {1}/{2}";
        internal const string LabelSpawnedPartly = "Only {0}/{1} {2} spawned (see log)";
        internal const string LabelGroupSpawnedPartly = "Only {0}/{1} of {2} spawned (see log)";
        internal const string LabelGroupSpawned = "Spawned {0} ({1})";
        internal const string LabelLeaderMissing = "{0}: the leader didn't spawn (see log)";
        internal const string LabelGroupLabel = "{0} ({1})";
        internal const string LabelSpawnFailed = "Spawn failed: {0}";
        internal const string LabelGroupSpawnFailed = "Group spawn failed: {0}";
        internal const string LabelRemoved = "Removed {0}";
        internal const string LabelKilled = "Killed {0}";

        internal const string DumpNone = "none";

        internal const string NameSpawnId = "perun";
    }

    internal static class Teleporting
    {
        internal const string FieldPrevSuccessLinkedFrom = "_prevSuccessLinkedFrom";
    }
}
