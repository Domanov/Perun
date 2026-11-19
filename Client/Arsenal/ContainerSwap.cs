#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Perun.Client.Arsenal;

internal sealed class SwapSteps<T>
{
    public SwapSteps(Func<ArsenalResult> blocked, Func<T, Task<ArsenalResult>> hold, Func<T, Task<ArsenalResult>> place, Func<T, Task<ArsenalResult>> takeOut, Func<T, string?> restore, Func<T, string> name)
    {
        Blocked = blocked;
        Hold = hold;
        Place = place;
        TakeOut = takeOut;
        Restore = restore;
        Name = name;
    }

    public Func<ArsenalResult> Blocked { get; }
    public Func<T, Task<ArsenalResult>> Hold { get; }
    public Func<T, Task<ArsenalResult>> Place { get; }
    public Func<T, Task<ArsenalResult>> TakeOut { get; }
    public Func<T, string?> Restore { get; }
    public Func<T, string> Name { get; }
}

// Replaces a container's contents. The old contents are only set aside until the new ones are in, so any
// failure other than "this item doesn't fit" puts the container back the way it was. Items that don't fit are skipped.
internal static class ContainerSwap
{
    public static async Task<ArsenalResult> Run<T>(IReadOnlyList<T> old, IReadOnlyList<T> fresh, SwapSteps<T> steps)
    {
        List<T> held = new();
        foreach (T item in old)
        {
            ArsenalResult result = await Guarded(steps, () => steps.Hold(item));
            if (!result.IsOk)
            {
                return Undo(steps, held, new List<T>(), result);
            }
            held.Add(item);
        }

        List<T> placed = new();
        List<string> skipped = new();
        foreach (T item in fresh)
        {
            ArsenalResult result = await Guarded(steps, () => steps.Place(item));
            if (result.IsOk)
            {
                placed.Add(item);
                continue;
            }
            if (!Skippable(result))
            {
                return await UndoAll(steps, held, placed, result);
            }
            skipped.Add(steps.Name(item));
        }

        return skipped.Count == 0 ? ArsenalResult.Ok : ArsenalResult.NoRoom($"{skipped.Count} item(s) didn't fit and were left out: {string.Join(", ", skipped)}");
    }

    private static bool Skippable(ArsenalResult result) => result.Status is ArsenalStatus.NoRoom or ArsenalStatus.Incompatible;

    private static async Task<ArsenalResult> Guarded<T>(SwapSteps<T> steps, Func<Task<ArsenalResult>> action)
    {
        ArsenalResult blocked = steps.Blocked();
        if (!blocked.IsOk)
        {
            return blocked;
        }
        try
        {
            return await action();
        }
        catch (OperationCanceledException)
        {
            return ArsenalResult.Cancelled();
        }
        catch (Exception ex)
        {
            return ArsenalResult.Failed(ex.Message);
        }
    }

    private static async Task<ArsenalResult> UndoAll<T>(SwapSteps<T> steps, List<T> held, List<T> placed, ArsenalResult cause)
    {
        List<T> stuck = new();
        for (int i = placed.Count - 1; i >= 0; i--)
        {
            ArsenalResult result;
            try
            {
                result = await steps.TakeOut(placed[i]);
            }
            catch (Exception ex)
            {
                result = ArsenalResult.Failed(ex.Message);
            }
            if (!result.IsOk)
            {
                stuck.Add(placed[i]);
            }
        }
        return Undo(steps, held, stuck, cause);
    }

    private static ArsenalResult Undo<T>(SwapSteps<T> steps, List<T> held, List<T> stuck, ArsenalResult cause)
    {
        List<string> lost = new();
        for (int i = held.Count - 1; i >= 0; i--)
        {
            string? problem;
            try
            {
                problem = steps.Restore(held[i]);
            }
            catch (Exception ex)
            {
                problem = $"{steps.Name(held[i])} ({ex.Message})";
            }
            if (problem != null)
            {
                lost.Add(problem);
            }
        }
        foreach (T item in stuck)
        {
            lost.Add($"{steps.Name(item)} (new, stayed in the container)");
        }
        if (lost.Count == 0)
        {
            return new ArsenalResult(cause.Status, $"{cause.Message}; contents left as they were");
        }
        return ArsenalResult.RollbackFailed($"{cause.Message}; contents not fully restored: {string.Join(", ", lost)}");
    }
}
