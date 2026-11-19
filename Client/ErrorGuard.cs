using System;
using System.Collections.Generic;
using UnityEngine;

namespace Perun.Client;

internal static class ErrorGuard
{
    private const float RepeatLogSeconds = 10f;

    private sealed class Entry
    {
        public int Repeats;
        public float NextLogAt;
    }

    private static readonly Dictionary<string, Entry> Seen = new();

    public static void Report(string where, Exception ex)
    {
        string key = where + "|" + ex.GetType().FullName + "|" + ex.Message;
        float now = Time.realtimeSinceStartup;
        if (!Seen.TryGetValue(key, out Entry entry))
        {
            Seen[key] = new Entry { NextLogAt = now + RepeatLogSeconds };
            Plugin.Log.LogError($"{where} failed: {ex}");
            return;
        }

        entry.Repeats++;
        if (now >= entry.NextLogAt)
        {
            Plugin.Log.LogError($"{where} failed again {entry.Repeats} time(s): {ex.Message}");
            entry.Repeats = 0;
            entry.NextLogAt = now + RepeatLogSeconds;
        }
    }
}
