using System;
using System.Reflection;
using EFT;
using HarmonyLib;
using UnityEngine;

namespace Perun.Client.Compat;

internal static class Animators
{
    private static readonly FieldInfo PlayerAnimators = Mode.Reflect.Field(Mode.Feature.SmoothieControl, typeof(Player), "_animators");
    private static readonly Type WrapperType = Mode.Reflect.Type(Mode.Feature.SmoothieControl, "AnimationSystem.UnityAnimatorWrapper");
    private static readonly FieldInfo WrapperAnimator = WrapperType != null ? Mode.Reflect.Field(Mode.Feature.SmoothieControl, WrapperType, "_animator") : null;

    public static Array All(Player player) => player != null && PlayerAnimators != null ? PlayerAnimators.GetValue(player) as Array : null;

    public static object Arms(Player player)
    {
        Array animators = All(player);
        return animators != null && animators.Length > 1 ? animators.GetValue(1) : null;
    }

    public static Animator Unity(object wrapper) => wrapper != null && WrapperAnimator != null && WrapperType.IsInstanceOfType(wrapper) ? WrapperAnimator.GetValue(wrapper) as Animator : null;

    public static Animator ArmsUnity(Player player) => Unity(Arms(player));
}
