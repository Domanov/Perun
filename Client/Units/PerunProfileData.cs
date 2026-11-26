using System.Collections.Generic;
using EFT;

namespace Perun.Client.Units;

internal sealed class PerunProfileData : IGetProfileData
{
    private const string DumpPerun = "Perun";

    private readonly GetProfileDataParams _inner;

    public PerunProfileData(GetProfileDataParams inner)
    {
        _inner = inner;
    }

    public EPlayerSide? Side => _inner.Side;

    public BotSpawnParams SpawnParams
    {
        get => _inner.SpawnParams;
        set => _inner.SpawnParams = value;
    }

    public bool KeepZoneOnSpawn => _inner.KeepZoneOnSpawn;

    public bool TryGetRole(out WildSpawnType role, out BotDifficulty difficulty) => _inner.TryGetRole(out role, out difficulty);

    public Profile ChooseProfile(List<Profile> profiles2Select, bool withDelete)
    {
        Profile exact = null;
        Profile sameRole = null;
        foreach (Profile profile in profiles2Select)
        {
            if (profile?.Info?.Settings == null || profile.Info.Settings.Role != _inner.Role)
            {
                continue;
            }

            if (profile.Info.Side == _inner.Side && profile.Info.Settings.BotDifficulty == _inner.BotDifficulty)
            {
                exact = profile;
                break;
            }

            sameRole ??= profile;
        }

        Profile chosen = exact ?? sameRole;
        if (chosen != null && withDelete)
        {
            profiles2Select.Remove(chosen);
        }
        return chosen;
    }

    public CountTypeBotWave[] PrepareToLoadBackend(int count) => _inner.PrepareToLoadBackend(count);

    public bool IsValidSpawnType(WildSpawnType wildSpawnType) => _inner.IsValidSpawnType(wildSpawnType);

    public string GetDebugLocalName() => _inner.GetDebugLocalName();

    public string GetDebugData() => DumpPerun + _inner.GetDebugData();

    public bool ShallChooseByData() => _inner.ShallChooseByData();

    public bool IsBossOrFollowerByTime() => _inner.IsBossOrFollowerByTime();

    public bool IsZeroWave() => _inner.IsZeroWave();

    public bool IsBossOrFollower() => _inner.IsBossOrFollower();

    public bool IsSpawnOnStart() => _inner.IsSpawnOnStart();

    public bool CanAtZoneByType(BotZone botZone, BotZonesLeaveController botsControllerZonesLeaveController) => _inner.CanAtZoneByType(botZone, botsControllerZonesLeaveController);
}
