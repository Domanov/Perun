using System;
using EFT.CameraControl;
using EFT.InventoryLogic;
using UnityEngine;
using NightVisionEffect = BSG.CameraEffects.NightVision;

namespace Perun.Client.View;

internal static class RtsVision
{
    private static readonly (string name, Color color)[] NightModes =
    {
        ("green", new Color(0.32f, 1f, 0.35f)),
        ("white", new Color(0.85f, 0.92f, 1f)),
        ("amber", new Color(1f, 0.7f, 0.25f))
    };

    private static readonly (string name, ThermalVisionComponent.SelectablePalette palette)[] ThermalModes =
    {
        // With the settings used here the game's two grey ramps come out the other way round from their names:
        // its WhiteHot ramp draws warm bodies black.
        ("black hot", ThermalVisionComponent.SelectablePalette.WhiteHot),
        ("white hot", ThermalVisionComponent.SelectablePalette.BlackHot),
        ("fusion", ThermalVisionComponent.SelectablePalette.Fusion),
        ("rainbow", ThermalVisionComponent.SelectablePalette.Rainbow)
    };

    private static int _night = -1;
    private static int _thermal = -1;

    public static string Label { get; private set; } = "VISION  OFF";
    public static bool On => _night >= 0 || _thermal >= 0;

    public static void Cycle()
    {
        if (_night >= 0 && _night + 1 < NightModes.Length)
        {
            SetNight(_night + 1);
        }
        else if (_night >= 0)
        {
            SetNight(-1);
            SetThermal(0);
        }
        else if (_thermal >= 0 && _thermal + 1 < ThermalModes.Length)
        {
            SetThermal(_thermal + 1);
        }
        else if (_thermal >= 0)
        {
            SetThermal(-1);
        }
        else
        {
            SetNight(0);
        }
        Label = _night >= 0 ? $"NVG  {NightModes[_night].name.ToUpperInvariant()}"
            : _thermal >= 0 ? $"THERMAL  {ThermalModes[_thermal].name.ToUpperInvariant()}"
            : "VISION  OFF";
    }

    // BetterThermalNightVision applies its own palette whenever thermal switches on, replacing the mode picked here.
    public static void Tick()
    {
        ThermalVision vision = _thermal >= 0 && CameraManager.Exist ? CameraManager.Instance.ThermalVision : null;
        ThermalVisionUtilities utilities = vision != null ? vision.ThermalVisionUtilities : null;
        if (utilities != null && (utilities.CurrentRampPalette != ThermalModes[_thermal].palette || utilities.ValuesCoefs.RampShift != 0f))
        {
            SetPalette(vision, ThermalModes[_thermal].palette);
        }
    }

    private static void SetPalette(ThermalVision vision, ThermalVisionComponent.SelectablePalette palette)
    {
        ThermalVisionUtilities utilities = vision.ThermalVisionUtilities;
        utilities.CurrentRampPalette = palette;
        utilities.ValuesCoefs.MainTexColorCoef = 0.3f;
        utilities.ValuesCoefs.MinimumTemperatureValue = 0.25f;
        utilities.ValuesCoefs.RampShift = 0f;
        vision.SetMaterialProperties();
    }

    public static void Off()
    {
        if (_night >= 0)
        {
            SetNight(-1);
        }
        if (_thermal >= 0)
        {
            SetThermal(-1);
        }
        Label = "VISION  OFF";
    }

    private static void SetNight(int mode)
    {
        _night = mode;
        NightVisionEffect vision = CameraManager.Exist ? CameraManager.Instance.NightVision : null;
        if (vision == null)
        {
            return;
        }
        try
        {
            if (mode >= 0)
            {
                vision.Intensity = 2.3f;
                vision.NoiseIntensity = 0.02f;
                vision.NoiseScale = 5f;
                vision.Color = NightModes[mode].color;
                vision.SetMask(NightVisionComponent.EMask.Anvis);
                vision.MaskSize = 4f;
                vision.ApplySettings();
            }
            if (vision.On != mode >= 0)
            {
                vision.StartSwitch(mode >= 0);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Vision: night vision failed: {ex.Message}");
        }
    }

    private static void SetThermal(int mode)
    {
        _thermal = mode;
        ThermalVision vision = CameraManager.Exist ? CameraManager.Instance.ThermalVision : null;
        if (vision == null)
        {
            return;
        }
        try
        {
            if (mode >= 0)
            {
                SetPalette(vision, ThermalModes[mode].palette);
                vision.ThermalVisionUtilities.DepthFade = 0.03f;
                vision.IsNoisy = false;
                vision.IsFpsStuck = false;
                vision.IsGlitch = false;
                vision.IsMotionBlurred = false;
                vision.IsPixelated = false;
                vision.SetMask(NightVisionComponent.EMask.Thermal);
                vision.ThermalVisionUtilities.MaskDescription.MaskSize = 4f;
                vision.SetMaterialProperties();
            }
            if (vision.On != mode >= 0)
            {
                vision.StartSwitch(mode >= 0);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Vision: thermal failed: {ex.Message}");
        }
    }
}
