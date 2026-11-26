#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Perun.Client.Arsenal.Constants.LoadoutRun;

namespace Perun.Client.Arsenal;

internal sealed class LoadoutSteps<TSlot> where TSlot : notnull
{
    public LoadoutSteps(Func<ArsenalResult> blocked, Func<TSlot, Task<ArsenalResult>> takeOff, Func<TSlot, Task<ArsenalResult>> give, Func<TSlot, string?> restore)
    {
        Blocked = blocked;
        TakeOff = takeOff;
        Give = give;
        Restore = restore;
    }

    public Func<ArsenalResult> Blocked { get; }
    public Func<TSlot, Task<ArsenalResult>> TakeOff { get; }
    public Func<TSlot, Task<ArsenalResult>> Give { get; }
    public Func<TSlot, string?> Restore { get; }
}

internal sealed class LoadoutOutcome
{
    public ArsenalResult? Aborted { get; set; }
    public List<string> Failures { get; } = new();
    public bool RollbackFailed { get; set; }
}

// Gear is changed slot by slot and each slot is kept once its new item is in. Old items wait in holding until
// then, so every way out of the run, including an abort or an exception, must put back all slots still waiting.
internal static class LoadoutRun
{
    public static async Task<LoadoutOutcome> Run<TSlot>(IReadOnlyList<TSlot> takeOff, IReadOnlyList<TSlot> give, LoadoutSteps<TSlot> steps) where TSlot : notnull
    {
        LoadoutOutcome outcome = new();
        List<TSlot> pending = new();
        try
        {
            foreach (TSlot slot in takeOff)
            {
                (ArsenalResult result, _) = await Step(steps, () => steps.TakeOff(slot));
                if (!result.IsOk)
                {
                    outcome.Aborted = Abort(steps, pending, Prefixed(slot, result));
                    return outcome;
                }
                pending.Add(slot);
            }

            foreach (TSlot slot in give)
            {
                (ArsenalResult result, bool abort) = await Step(steps, () => steps.Give(slot));
                if (result.IsOk)
                {
                    pending.Remove(slot);
                    continue;
                }
                if (abort)
                {
                    outcome.Aborted = Abort(steps, pending, Prefixed(slot, result));
                    return outcome;
                }
                string? lost = pending.Remove(slot) ? Restored(steps, slot) : null;
                if (lost != null)
                {
                    outcome.RollbackFailed = true;
                    outcome.Failures.Add(string.Format(LabelSlotFailedNotRestored, slot, result.Message, lost));
                }
                else
                {
                    outcome.Failures.Add(string.Format(LabelSlotFailed, slot, result.Message));
                }
            }

            pending.Clear();
            return outcome;
        }
        finally
        {
            if (pending.Count > 0 && outcome.Aborted == null)
            {
                outcome.Aborted = Abort(steps, pending, ArsenalResult.Failed(LabelStoppedUnexpectedly));
            }
        }
    }

    private static async Task<(ArsenalResult result, bool abort)> Step<TSlot>(LoadoutSteps<TSlot> steps, Func<Task<ArsenalResult>> action) where TSlot : notnull
    {
        ArsenalResult blocked = steps.Blocked();
        if (!blocked.IsOk)
        {
            return (blocked, true);
        }
        ArsenalResult result;
        try
        {
            result = await action();
        }
        catch (OperationCanceledException)
        {
            return (ArsenalResult.Cancelled(), true);
        }
        catch (Exception ex)
        {
            return (ArsenalResult.Failed(ex.Message), true);
        }
        if (result.IsOk)
        {
            return (result, false);
        }
        ArsenalResult after = steps.Blocked();
        if (!after.IsOk)
        {
            return (after, true);
        }
        return (result, result.Status == ArsenalStatus.Cancelled);
    }

    private static ArsenalResult Prefixed<TSlot>(TSlot slot, ArsenalResult result) => new(result.Status, string.Format(LabelSlotFailed, slot, result.Message));

    private static string? Restored<TSlot>(LoadoutSteps<TSlot> steps, TSlot slot) where TSlot : notnull
    {
        try
        {
            return steps.Restore(slot);
        }
        catch (Exception ex)
        {
            return string.Format(LabelSlotError, slot, ex.Message);
        }
    }

    private static ArsenalResult Abort<TSlot>(LoadoutSteps<TSlot> steps, List<TSlot> pending, ArsenalResult cause) where TSlot : notnull
    {
        List<string> lost = new();
        int restored = 0;
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            string? problem = Restored(steps, pending[i]);
            if (problem != null)
            {
                lost.Add(problem);
            }
            else
            {
                restored++;
            }
        }
        pending.Clear();
        if (lost.Count > 0)
        {
            return ArsenalResult.RollbackFailed(string.Format(LabelNotRestored, cause.Message, string.Join(", ", lost)));
        }
        return restored == 0 ? cause : new ArsenalResult(cause.Status, string.Format(LabelSlotsKept, cause.Message, restored));
    }
}
