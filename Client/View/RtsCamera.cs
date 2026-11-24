using System;
using UnityEngine;
using UnityEngine.AI;

namespace Perun.Client.View;

internal sealed class RtsCamera
{
    private const float MinPitch = 12f;
    private const float MaxPitch = 88f;
    private const float FloorStep = 3f;
    private const float MaxHeightSpeed = 8f;

    private Camera _camera;
    private Vector3 _pivot;
    private Vector3 _targetPivot;
    private float _yaw;
    private float _targetYaw;
    private float _pitch;
    private float _targetPitch;
    private float _distance;
    private float _targetDistance;
    private float _collisionDistance;
    private Vector3 _lastMouse;
    private bool _dragging;
    private int _solidMask;
    private int _terrainMask;
    private Vector3 _sampledAt = new(float.NaN, 0f, 0f);
    private float _sampledHeight;

    public Vector3 Pivot => _targetPivot;

    public float Yaw => _targetYaw;

    // Gets the wheel first; returning true keeps it from zooming or tilting.
    public Func<float, bool> WheelHandler { get; set; }

    private const float MaxInputStep = 0.3f;

    public EFT.Player Following { get; private set; }

    public void Follow(EFT.Player target)
    {
        Following = target;
        if (target != null)
        {
            _targetPivot = target.Position;
        }
    }

    public void Begin(Camera camera, Vector3 focus, Vector3 cameraPosition, float yaw)
    {
        _camera = camera;
        _solidMask = LayerMask.GetMask("Terrain", "HighPolyCollider", "LowPolyCollider");
        _terrainMask = LayerMask.GetMask("Terrain");

        _targetPivot = focus;
        _targetYaw = yaw;
        _targetPitch = 55f;
        _targetDistance = Mathf.Clamp(30f, Plugin.MinDistance.Value, Plugin.MaxDistance.Value);

        _pivot = focus;
        _yaw = yaw;
        Vector3 offset = cameraPosition - focus;
        _distance = Mathf.Clamp(offset.magnitude, 1f, Plugin.MaxDistance.Value);
        _collisionDistance = _distance;
        _pitch = _distance > 0.5f ? Mathf.Clamp(Mathf.Asin(Mathf.Clamp(offset.y / _distance, -1f, 1f)) * Mathf.Rad2Deg, MinPitch, MaxPitch) : _targetPitch;
        _dragging = false;
        _sampledAt = new Vector3(float.NaN, 0f, 0f);
    }

    public void Rebind(Camera camera) => _camera = camera;

    public void Focus(Vector3 point)
    {
        _targetPivot = point;
    }

    public void JumpTo(Vector3 point, float yaw)
    {
        Following = null;
        _pivot = _targetPivot = point;
        _yaw = _targetYaw = yaw;
    }

    public void Tick()
    {
        if (_camera == null)
        {
            return;
        }

        // Capped only against one long hitch: a tighter cap slows panning and Q/E turning whenever the game runs below it
        // (a wide zoomed-out view can drop under 10 fps).
        float dt = Mathf.Min(Time.unscaledDeltaTime, MaxInputStep);
        Quaternion heading = Quaternion.Euler(0f, _targetYaw, 0f);
        Vector3 forward = heading * Vector3.forward;
        Vector3 right = heading * Vector3.right;

        bool keys = !Perun.Client.UI.PerunHud.InputBlocked;
        Vector2 move = Vector2.zero;
        if (keys)
        {
            if (Input.GetKey(KeyCode.W)) move.y += 1f;
            if (Input.GetKey(KeyCode.S)) move.y -= 1f;
            if (Input.GetKey(KeyCode.D)) move.x += 1f;
            if (Input.GetKey(KeyCode.A)) move.x -= 1f;
        }

        if (Plugin.EdgePan.Value && Application.isFocused)
        {
            Vector3 mouse = Input.mousePosition;
            const float edge = 6f;
            bool inWindow = mouse.x >= 0f && mouse.y >= 0f && mouse.x <= Screen.width && mouse.y <= Screen.height;
            if (inWindow)
            {
                if (mouse.x <= edge) move.x -= 1f;
                if (mouse.x >= Screen.width - edge) move.x += 1f;
                if (mouse.y <= edge) move.y -= 1f;
                if (mouse.y >= Screen.height - edge) move.y += 1f;
            }
        }

        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        if (Following != null)
        {
            if (move.sqrMagnitude > 0.01f || Following.HealthController == null || !Following.HealthController.IsAlive)
            {
                Following = null;
            }
        }

        bool fast = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float speed = (6f + _targetDistance * 0.9f) * Plugin.PanSpeed.Value * (fast ? 3f : 1f);
        _targetPivot += (forward * move.y + right * move.x) * (speed * dt);

        float rotate = 0f;
        if (keys && Input.GetKey(KeyCode.Q)) rotate -= 1f;
        if (keys && Input.GetKey(KeyCode.E)) rotate += 1f;
        _targetYaw += rotate * 90f * Plugin.RotateSpeed.Value * dt;

        Vector3 mousePosition = Input.mousePosition;
        bool overUi = Perun.Client.UI.PerunHud.PointerBlocked;
        if (Input.GetMouseButtonDown(2) && !overUi)
        {
            _dragging = true;
            _lastMouse = mousePosition;
        }
        if (!Input.GetMouseButton(2))
        {
            _dragging = false;
        }
        if (_dragging)
        {
            Vector3 delta = mousePosition - _lastMouse;
            _lastMouse = mousePosition;
            _targetYaw += delta.x * 0.25f * Plugin.RotateSpeed.Value;
            _targetPitch = Mathf.Clamp(_targetPitch - delta.y * 0.2f * Plugin.RotateSpeed.Value, MinPitch, MaxPitch);
        }

        // Some mice report several notches in one frame for a quick flick; at most one per frame keeps each zoom step the
        // same size.
        float wheel = Mathf.Clamp(Input.mouseScrollDelta.y, -1f, 1f);
        if (Mathf.Abs(wheel) > 0.01f && !overUi && (WheelHandler == null || !WheelHandler(wheel)))
        {
            if (Input.GetKey(Plugin.TiltKey.Value))
            {
                _targetPitch = Mathf.Clamp(_targetPitch - wheel * 3f * Plugin.RotateSpeed.Value, MinPitch, MaxPitch);
            }
            else
            {
                _targetDistance *= Mathf.Pow(0.88f, wheel * Plugin.ZoomSpeed.Value);
            }
        }
        _targetDistance = Mathf.Clamp(_targetDistance, Plugin.MinDistance.Value, Plugin.MaxDistance.Value);

        if (keys && Input.GetKeyDown(KeyCode.R))
        {
            _targetPivot.y += FloorStep;
        }
        if (keys && Input.GetKeyDown(KeyCode.F) && !Input.GetKey(KeyCode.LeftControl))
        {
            _targetPivot.y -= FloorStep;
        }

        if (Following != null)
        {
            _targetPivot = Following.Position;
            return;
        }

        _targetPivot.y = WalkableHeight(_targetPivot);
    }

    public void Apply()
    {
        if (_camera == null)
        {
            return;
        }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        float t = 1f - Mathf.Exp(-12f * dt);

        Vector3 pivot = Vector3.Lerp(_pivot, _targetPivot, t);
        float heightStep = (_targetPivot.y - _pivot.y) * (1f - Mathf.Exp(-4f * dt));
        pivot.y = _pivot.y + Mathf.Clamp(heightStep, -MaxHeightSpeed * dt, MaxHeightSpeed * dt);
        _pivot = pivot;
        _yaw = Mathf.LerpAngle(_yaw, _targetYaw, t);
        _pitch = Mathf.Lerp(_pitch, _targetPitch, t);
        _distance = Mathf.Lerp(_distance, _targetDistance, t);

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 look = _pivot + Vector3.up * 1.2f;
        Vector3 back = -(rotation * Vector3.forward);

        float allowed = _distance;
        if (Plugin.CameraCollision.Value && Physics.SphereCast(look, 0.35f, back, out RaycastHit hit, _distance, _solidMask, QueryTriggerInteraction.Ignore))
        {
            allowed = Mathf.Max(hit.distance - 0.2f, Plugin.MinDistance.Value * 0.5f);
        }

        float rate = allowed < _collisionDistance ? 20f : 2.5f;
        _collisionDistance = Mathf.Lerp(_collisionDistance, allowed, 1f - Mathf.Exp(-rate * dt));
        _collisionDistance = Mathf.Min(_collisionDistance, _distance);

        Vector3 position = look + back * _collisionDistance;

        if (Physics.Raycast(new Vector3(position.x, position.y + 200f, position.z), Vector3.down, out RaycastHit ground, 400f, _terrainMask, QueryTriggerInteraction.Ignore) && position.y < ground.point.y + 1f)
        {
            position.y = ground.point.y + 1f;
        }

        _camera.transform.SetPositionAndRotation(position, rotation);
    }

    public Pose CurrentPose()
    {
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 look = _pivot + Vector3.up * 1.2f;
        Vector3 position = look - rotation * Vector3.forward * Mathf.Min(_collisionDistance, _distance);
        return new Pose(position, rotation);
    }

    private float WalkableHeight(Vector3 point)
    {
        if (Mathf.Abs(point.x - _sampledAt.x) < 0.05f && Mathf.Abs(point.z - _sampledAt.z) < 0.05f && Mathf.Abs(point.y - _sampledAt.y) < 0.01f)
        {
            return _sampledHeight;
        }

        _sampledHeight = SampleHeight(point);
        _sampledAt = new Vector3(point.x, _sampledHeight, point.z);
        return _sampledHeight;
    }

    private float SampleHeight(Vector3 point)
    {
        if (NavMesh.SamplePosition(point, out NavMeshHit nav, FloorStep * 2f, NavMesh.AllAreas))
        {
            return nav.position.y;
        }

        Vector3 origin = new(point.x, point.y + 1.5f, point.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200f, _solidMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }

        return point.y;
    }
}
