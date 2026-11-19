#nullable enable
using System;
using UnityEngine;

namespace Perun.Client.View;

internal sealed class CameraFlight
{
    public const float Duration = 1f;

    private Vector3 _fromPosition;
    private Quaternion _fromRotation;
    private Func<Pose>? _target;
    private float _start;
    private float _duration = Duration;

    public bool Active => _target != null;

    public bool Arrived { get; private set; }

    public void Start(Transform camera, Func<Pose> target, float now, float duration = Duration)
    {
        _duration = duration;
        _fromPosition = camera.position;
        _fromRotation = camera.rotation;
        _target = target;
        _start = now;
        Arrived = false;
    }

    public void Start(Transform camera, Pose from, Func<Pose> target, float now, float duration = Duration)
    {
        camera.SetPositionAndRotation(from.position, from.rotation);
        Start(camera, target, now, duration);
    }

    public void Tick(Transform camera, float now)
    {
        if (_target == null)
        {
            return;
        }
        float progress = Progress(now - _start, _duration);
        Arrived = progress >= 1f;
        Pose to = _target();
        float t = Ease(progress);
        camera.SetPositionAndRotation(Vector3.Lerp(_fromPosition, to.position, t), Quaternion.Slerp(_fromRotation, to.rotation, t));
    }

    public void Stop()
    {
        _target = null;
        Arrived = false;
    }

    public static float Progress(float elapsed, float duration) => duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);

    public static float Ease(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
}
