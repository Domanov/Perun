using System.Collections.Generic;
using EFT.CameraControl;
using Koenigz.PerfectCulling;
using Koenigz.PerfectCulling.EFT;
using UnityEngine;

namespace Perun.Client.Compat;

internal sealed class GameCulling
{
    private readonly List<DisablerCullingObjectBase> _enabledObjects = new();
    private int _shownGroups;
    private readonly List<PerfectCullingCrossSceneGroup> _disabledSceneGroups = new();
    private bool? _occlusion;
    private bool _applied;

    // The game culls level chunks around your character. From the RTS camera you look at places far from it, so the
    // chunks are shown for the duration of RTS mode and put back as they were when it ends.
    public void Apply()
    {
        Restore();
        _applied = true;
        CameraManager cameraManager = CameraManager.Instance;
        _occlusion = cameraManager._occlusionCullingEnabled;
        cameraManager.SetOcclusionCullingEnabled(false);

        _allZones.Clear();
        _cursor = 0;
        _shownGroups = 0;
        foreach (DisablerCullingObjectBase culling in Object.FindObjectsOfType<DisablerCullingObjectBase>())
        {
            _allZones.Add(culling);
            if (!culling.HasEntered)
            {
                culling.SetComponentsEnabled(true);
                _enabledObjects.Add(culling);
            }
        }

        PerfectCullingAdaptiveGrid grid = Object.FindObjectOfType<PerfectCullingAdaptiveGrid>();
        if (grid != null && grid.RuntimeGroupMapping != null)
        {
            foreach (PerfectCullingCrossSceneGroup sceneGroup in grid.RuntimeGroupMapping)
            {
                foreach (PerfectCullingBakeGroup bakeGroup in sceneGroup.bakeGroups)
                {
                    if (!bakeGroup.IsEnabled)
                    {
                        bakeGroup.IsEnabled = true;
                        _shownGroups++;
                    }
                }

                if (sceneGroup.enabled)
                {
                    sceneGroup.enabled = false;
                    _disabledSceneGroups.Add(sceneGroup);
                }
            }
        }

        SmoothieControl.ResyncLodCulling();
        if (Plugin.Verbose.Value)
        {
            Plugin.Log.LogInfo($"Culling: {_enabledObjects.Count} zone objects, {_shownGroups} bake groups shown");
        }
    }

    private int _cursor;
    private readonly List<DisablerCullingObjectBase> _allZones = new();

    public void Reinforce(int budget)
    {
        if (!_applied)
        {
            return;
        }

        if (CameraManager.Exist && CameraManager.Instance._occlusionCullingEnabled == true)
        {
            CameraManager.Instance.SetOcclusionCullingEnabled(false);
        }

        for (int i = 0; i < _disabledSceneGroups.Count && budget > 0; i++)
        {
            PerfectCullingCrossSceneGroup sceneGroup = _disabledSceneGroups[i];
            if (sceneGroup != null && sceneGroup.enabled)
            {
                sceneGroup.enabled = false;
            }
        }

        int count = _allZones.Count;
        for (int n = 0; n < budget && count > 0; n++)
        {
            if (_cursor >= count)
            {
                _cursor = 0;
            }
            DisablerCullingObjectBase culling = _allZones[_cursor++];
            if (culling != null && !culling.HasEntered)
            {
                culling.SetComponentsEnabled(true);
            }
        }
    }

    public void Restore()
    {
        if (!_applied)
        {
            return;
        }

        _applied = false;
        foreach (PerfectCullingCrossSceneGroup sceneGroup in _disabledSceneGroups)
        {
            if (sceneGroup != null)
            {
                sceneGroup.enabled = true;
            }
        }

        // The shown chunks are left on for the culler to hide: it corrects chunk states only when it next recomputes
        // visibility, which may not happen soon after leaving (same culling cell), so chunks switched off here could
        // leave buildings missing at your character's spot.
        foreach (DisablerCullingObjectBase culling in _enabledObjects)
        {
            if (culling != null && !culling.HasEntered)
            {
                culling.SetComponentsEnabled(false);
            }
        }

        if (CameraManager.Exist)
        {
            CameraManager.Instance.SetOcclusionCullingEnabled(_occlusion);
        }

        _enabledObjects.Clear();
        _allZones.Clear();
        _disabledSceneGroups.Clear();
        SmoothieControl.ResyncLodCulling();
    }
}
