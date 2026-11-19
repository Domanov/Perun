using System;
using EFT;
using UnityEngine;

namespace Perun.Client.Units;

internal enum LoadStage
{
    Waiting,
    Server,
    Gear,
    Creating,
    Done
}

internal sealed class LoadJob : IProgress<InitLevelProgress>, IJobView
{
    private const float BundleShare = 0.85f;

    public LoadJob(string title, bool spawn, int total)
    {
        Title = title;
        IsSpawn = spawn;
        Total = total;
        Started = Time.realtimeSinceStartup;
    }

    public string Title { get; }
    public bool IsSpawn { get; }
    public int Total { get; }
    public float Started { get; }
    public LoadStage Stage { get; private set; } = LoadStage.Waiting;
    public float Progress { get; private set; } = -1f;
    public int Created { get; private set; }

    public float Elapsed => Time.realtimeSinceStartup - Started;

    public void Enter(LoadStage stage)
    {
        Stage = stage;
        Progress = stage switch
        {
            LoadStage.Gear => 0f,
            LoadStage.Creating => Total > 0 ? (float)Created / Total : 0f,
            LoadStage.Done => 1f,
            _ => -1f
        };
    }

    public void Spawned()
    {
        Created++;
        if (Stage == LoadStage.Creating && Total > 0)
        {
            Progress = Mathf.Clamp01((float)Created / Total);
        }
    }

    public void Report(InitLevelProgress value)
    {
        if (Stage != LoadStage.Gear)
        {
            return;
        }

        float part = Mathf.Clamp01(value.Progress);
        float overall = value.Stage == InitLevelStage.CreatingPools ? BundleShare + (1f - BundleShare) * part : BundleShare * part;
        if (overall > Progress)
        {
            Progress = overall;
        }
    }
}
