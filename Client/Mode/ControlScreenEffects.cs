using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;

namespace Perun.Client.Mode;

internal static class ControlScreenEffects
{
    private static readonly FieldInfo EffectsPlayerField = Reflect.Field(Feature.ControlScreenEffects, typeof(EffectsController), "player");

    // In RTS mode the camera belongs to nobody, so no body's effects may reach it. The game hooks a controller to a
    // player when that player's camera controller is created and never unhooks it, so Perun does both by hand.
    public static void Hook(Player player)
    {
        if (player?.HealthController == null || EffectsPlayerField == null)
        {
            return;
        }
        foreach (EffectsController effects in UnityEngine.Object.FindObjectsOfType<EffectsController>(true))
        {
            if (effects == null)
            {
                continue;
            }
            Unsubscribe(effects, player);
            var health = player.HealthController;
            health.EffectStartedEvent += effects.OnEffectStarted;
            health.EffectRemovedEvent += effects.OnEffectRemoved;
            player.OnDamageReceived += effects.OnPlayerDamaged;
            health.ApplyDamageEvent += effects.OnHealthApplyDamage;
            health.DiedEvent += effects.OnHealthDied;
            health.BurnEyesEvent += effects.OnHealthBurnEyes;
            health.StimulatorBuffEvent += effects.OnStimulatorBuff;
            player.OnGlassesChanged += effects.OnGlassesChanged;
            EffectsPlayerField.SetValue(effects, player);
            Show(effects, player);
        }
        MatchGlasses(player);
    }

    private static readonly FieldInfo DeathFadeField = Reflect.Field(Feature.ControlScreenEffects, typeof(EffectsController), "_deathFade");
    private static readonly FieldInfo BlurField = Reflect.Field(Feature.ControlScreenEffects, typeof(EffectsController), "_blur");

    public static void ClearDeath()
    {
        foreach (EffectsController effects in UnityEngine.Object.FindObjectsOfType<EffectsController>(true))
        {
            if (effects == null)
            {
                continue;
            }
            if (DeathFadeField?.GetValue(effects) is DeathFade fade && fade != null)
            {
                fade.DisableEffect();
                fade.enabled = false;
            }
            if (BlurField?.GetValue(effects) is FastBlur blur && blur != null)
            {
                blur.Reset();
                blur.enabled = false;
            }
        }
    }

    public static void Unhook(Player player)
    {
        if (player?.HealthController == null || EffectsPlayerField == null)
        {
            return;
        }
        foreach (EffectsController effects in UnityEngine.Object.FindObjectsOfType<EffectsController>(true))
        {
            if (effects == null)
            {
                continue;
            }
            Unsubscribe(effects, player);
            if (EffectsPlayerField.GetValue(effects) == null)
            {
                EffectsPlayerField.SetValue(effects, player);
            }
            Clear(effects);
        }
        MatchGlasses(null);
    }

    private static void Unsubscribe(EffectsController effects, Player player)
    {
        var health = player.HealthController;
        health.EffectStartedEvent -= effects.OnEffectStarted;
        health.EffectRemovedEvent -= effects.OnEffectRemoved;
        player.OnDamageReceived -= effects.OnPlayerDamaged;
        health.ApplyDamageEvent -= effects.OnHealthApplyDamage;
        health.DiedEvent -= effects.OnHealthDied;
        health.BurnEyesEvent -= effects.OnHealthBurnEyes;
        health.StimulatorBuffEvent -= effects.OnStimulatorBuff;
        player.OnGlassesChanged -= effects.OnGlassesChanged;
    }

    private static void Show(EffectsController effects, Player player)
    {
        if (player?.HealthController == null || AccumulatorsField?.GetValue(effects) is not IList accumulators)
        {
            return;
        }
        foreach (var effect in player.HealthController.GetAllActiveEffects())
        {
            foreach (EffectsController.EffectAccumulator accumulator in accumulators)
            {
                if (accumulator != null && !accumulator.ActiveEffects.Contains(effect))
                {
                    accumulator.AddEffect(effect);
                }
            }
        }
    }

    private static readonly FieldInfo AccumulatorsField = Reflect.Field(Feature.ControlScreenEffects, typeof(EffectsController), "_effectAccumulators");

    public static void ClearAll()
    {
        foreach (EffectsController effects in UnityEngine.Object.FindObjectsOfType<EffectsController>(true))
        {
            Clear(effects);
        }
    }

    private static readonly FieldInfo BloodField = Reflect.Field(Feature.ControlScreenEffects, typeof(EffectsController), "_bloodOnScreen");
    private static readonly FieldInfo EyeBurnField = Reflect.Field(Feature.ControlScreenEffects, typeof(EffectsController), "_eyeBurn");

    // The camera effects (pain, painkillers, contusion, frostbite, ...) are shared by whoever the camera follows. An
    // effect that is removed only fades out over the next seconds in Update, and switching it off right away left its
    // last values on the camera, so the next body inherited them. The fade is run to its end at once instead.
    private const int FadeSteps = 1200;

    private static void Clear(EffectsController effects)
    {
        if (effects == null || AccumulatorsField?.GetValue(effects) is not IList accumulators)
        {
            return;
        }
        int cleared = 0;
        foreach (EffectsController.EffectAccumulator accumulator in accumulators)
        {
            if (accumulator == null)
            {
                continue;
            }
            try
            {
                foreach (var effect in accumulator.ActiveEffects.ToArray())
                {
                    accumulator.DeleteEffect(effect);
                    cleared++;
                }
                if (accumulator.GetType().GetField("_stimulatorBuffs", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(accumulator) is IList buffs && buffs.Count > 0)
                {
                    buffs.Clear();
                    cleared++;
                }
                for (int step = 0; step < FadeSteps && accumulator.Enabled; step++)
                {
                    accumulator.UpdateAmount();
                }
                if (accumulator.Enabled && accumulator.ActiveEffects.Count == 0)
                {
                    accumulator.Toggle(false);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Control: couldn't clear a screen effect ({accumulator.GetType().Name}): {ex.Message}");
            }
        }
        if (BloodField?.GetValue(effects) is BloodOnScreen blood && blood != null)
        {
            blood.FadeDrops();
        }
        if (EyeBurnField?.GetValue(effects) is EyeBurn eyeBurn && eyeBurn != null)
        {
            eyeBurn.ClearEyeBurnRT();
        }
        if (cleared > 0)
        {
            Plugin.Log.LogInfo($"Control: cleared {cleared} screen effect(s) from {effects.name}");
        }
    }

    public static void MatchGlasses(Player player)
    {
        bool wearing = false;
        Visors glasses = null;
        if (player != null)
        {
            wearing = player.TryFindGlasses(out glasses);
        }
        foreach (EffectsController effects in UnityEngine.Object.FindObjectsOfType<EffectsController>(true))
        {
            if (effects != null)
            {
                effects.OnGlassesChanged(glasses, wearing);
            }
        }
    }
}
