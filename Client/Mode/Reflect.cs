using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using static Perun.Client.Mode.Constants.Reflect;

namespace Perun.Client.Mode;

internal enum Feature
{
    Control,
    ControlScreenEffects,
    ControlAudio,
    PausedClock,
    SmoothieControl,
    TeleportHold
}

// Game internals are read through FieldInfo/MethodInfo instead of typed accessors
// (AccessTools.FieldRefAccess, MethodDelegate). Slower and unchecked at compile time, but the private
// types involved are not visible to the compiler here, and none of these calls is on a per-frame path.
// Every lookup is registered under the feature that needs it, so Check() can report and switch off what a game update removed.
internal static class Reflect
{
    private static readonly Dictionary<Feature, List<string>> Missing = new();

    private static readonly Dictionary<Feature, string> Effects = new()
    {
        [Feature.Control] = LabelNoControl,
        [Feature.ControlScreenEffects] = LabelEffectsCarryOver,
        [Feature.ControlAudio] = LabelSoundsMuffled,
        [Feature.PausedClock] = LabelTimerRuns,
        [Feature.SmoothieControl] = LabelHandsFreeze,
        [Feature.TeleportHold] = LabelTeleportWalkBack
    };

    public static bool Lacks(Feature feature) => Missing.ContainsKey(feature);

    public static FieldInfo Field(Feature feature, Type type, string name)
    {
        FieldInfo field = type != null ? AccessTools.Field(type, name) : null;
        Require(feature, field != null, $"{type?.Name ?? "?"}.{name}");
        return field;
    }

    public static Type Type(Feature feature, string name)
    {
        Type type = AccessTools.TypeByName(name);
        Require(feature, type != null, name);
        return type;
    }

    public static void Require(Feature feature, bool present, string what)
    {
        if (present)
        {
            return;
        }
        if (!Missing.TryGetValue(feature, out List<string> list))
        {
            Missing[feature] = list = new List<string>();
        }
        list.Add(what);
    }

    public static void Check(params Type[] users)
    {
        foreach (Type user in users)
        {
            try
            {
                RuntimeHelpers.RunClassConstructor(user.TypeHandle);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Couldn't check {user.Name}: {ex.GetBaseException().Message}");
            }
        }
        foreach (KeyValuePair<Feature, List<string>> entry in Missing)
        {
            Plugin.Log.LogWarning($"This game version lacks {string.Join(", ", entry.Value)}: {Effects[entry.Key]}");
        }
    }
}
