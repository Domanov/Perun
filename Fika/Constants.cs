namespace Perun.Fika;

internal static class Constants
{
    internal static class BridgeHost
    {
        internal const string ConfigSectionDebug = "Debug build";

        internal const string ConfigKeyForgeHost = "Send forged host messages";
        internal const string ConfigKeyMalformedRequests = "Send malformed game master requests";

        internal const string ConfigDescriptionForgeHost = "Sends a Hello claiming protocol 6, an unsigned pause and a pause with a wrong token to every other player, the way a modified client would. Debug builds only.";
        internal const string ConfigDescriptionMalformedRequests = "Sends a NaN spawn, a BTR gunner spawn, a 500-bot spawn and an unknown order to the host. Debug builds only.";
    }

    internal static class DeathCamera
    {
        internal const string FieldFreeCamScript = "_freeCamScript";
        internal const string FieldExtractText = "_extractText";
        internal const string FieldFreecamUI = "_freecamUI";
    }

    internal static class FikaBridge
    {
        internal const string LabelNotConnected = "Perun hasn't finished connecting yet, try again";
        internal const string LabelNoPassword = "The headless host has no game master password set";
        internal const string LabelHostOnly = "Only the host can use Perun";
        internal const string LabelPasswordLocked = "Too many wrong passwords, wait {0} s";
        internal const string LabelWrongPassword = "Wrong game master password";
        internal const string LabelAlreadyGameMaster = "{0} is already the game master";
        internal const string LabelReasonDisconnected = "disconnected";
        internal const string LabelReasonLeftRts = "left RTS mode";
        internal const string LabelReasonRaidEnded = "raid ended";
        internal const string LabelReasonHostLost = "the connection to the host ended";

        internal const string DumpPause = "pause";
        internal const string DumpWeather = "weather";
        internal const string DumpTime = "time";
        internal const string DumpAnswer = "game master answer";
        internal const string DumpStatus = "status";
        internal const string DumpTeleport = "teleport";
        internal const string DumpHeal = "heal";
    }
}
