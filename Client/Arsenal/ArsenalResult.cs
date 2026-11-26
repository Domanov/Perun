using static Perun.Client.Arsenal.Constants.ArsenalResult;

#nullable enable
namespace Perun.Client.Arsenal;

internal enum ArsenalStatus
{
    Ok,
    Busy,
    Invalid,
    Incompatible,
    NoRoom,
    Cancelled,
    Failed,
    RollbackFailed
}

internal readonly struct ArsenalResult
{
    public static readonly ArsenalResult Ok = new(ArsenalStatus.Ok, "");

    public ArsenalResult(ArsenalStatus status, string message)
    {
        Status = status;
        Message = message;
    }

    public ArsenalStatus Status { get; }

    public string Message { get; }

    public bool IsOk => Status == ArsenalStatus.Ok;

    public bool CanRetry => Status == ArsenalStatus.Busy;

    public static ArsenalResult Busy(string message) => new(ArsenalStatus.Busy, message);

    public static ArsenalResult Invalid(string message) => new(ArsenalStatus.Invalid, message);

    public static ArsenalResult Incompatible(string message) => new(ArsenalStatus.Incompatible, message);

    public static ArsenalResult NoRoom(string message) => new(ArsenalStatus.NoRoom, message);

    public static ArsenalResult Cancelled() => new(ArsenalStatus.Cancelled, LabelRaidEnded);

    public static ArsenalResult Failed(string message) => new(ArsenalStatus.Failed, message);

    public static ArsenalResult RollbackFailed(string message) => new(ArsenalStatus.RollbackFailed, message);

    public override string ToString() => IsOk ? DumpOk : $"{Message} ({Status})";
}
