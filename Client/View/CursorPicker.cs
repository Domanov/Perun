using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using UnityEngine;
using UnityEngine.AI;

namespace Perun.Client.View;

internal sealed class CursorPicker
{
    private const float PickRadius = 28f;
    private const float DragThreshold = 6f;

    private readonly List<Player> _selection = new();
    private readonly HashSet<Player> _selected = new();
    private int _groundMask;
    private Vector3 _lastMouse = new(float.NaN, 0f, 0f);
    private Vector3 _lastCameraPosition;
    private Quaternion _lastCameraRotation;
    private bool _leftDown;
    private Vector2 _dragStart;
    private bool _pressOnNavMesh;

    public bool HasGround { get; private set; }
    public Vector3 Ground { get; private set; }
    public bool HasNavPoint { get; private set; }
    public Vector3 NavPoint { get; private set; }
    public Player Hovered { get; private set; }
    public IReadOnlyList<Player> Selection => _selection;
    public Player Selected => _selection.Count > 0 ? _selection[0] : null;
    public bool Dragging { get; private set; }
    public Rect DragRect { get; private set; }

    // A drag while placing keeps the press point and turns the placement towards the cursor.
    public bool Turning { get; private set; }
    public Vector3 TurnAnchor { get; private set; }
    public float TurnYaw { get; private set; }

    public Func<Vector3, float?, bool> PlaceHandler { get; set; }
    public Func<bool> IsPlacing { get; set; }

    public Func<Vector2, bool> ScreenClickHandler { get; set; }
    public Func<bool> CancelHandler { get; set; }
    public Action<Vector3, bool, bool> OrderHandler { get; set; }

    public bool IsSelected(Player player) => player != null && _selected.Contains(player);

    public void Reset()
    {
        HasGround = false;
        HasNavPoint = false;
        Hovered = null;
        ClearSelection();
        _lastMouse = new Vector3(float.NaN, 0f, 0f);
        _leftDown = false;
        Dragging = false;
        Turning = false;
    }

    public void Tick(Camera camera)
    {
        if (camera == null)
        {
            return;
        }

        if (_groundMask == 0)
        {
            _groundMask = LayerMask.GetMask("Terrain", "HighPolyCollider", "LowPolyCollider");
        }

        PruneSelection();

        Vector3 mouse = Input.mousePosition;
        Vector2 cursor = new(mouse.x, Screen.height - mouse.y);
        UpdateGround(camera, mouse);

        bool overUi = Perun.Client.UI.PerunHud.PointerBlocked;
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        Hovered = overUi ? null : FindHovered(camera, cursor);

        if (Input.GetMouseButtonDown(0) && !overUi)
        {
            _leftDown = true;
            _dragStart = cursor;
            Dragging = false;
            Turning = false;
            _pressOnNavMesh = HasNavPoint;
            TurnAnchor = NavPoint;
        }

        if (_leftDown)
        {
            if (!Dragging && !Turning && (cursor - _dragStart).sqrMagnitude > DragThreshold * DragThreshold)
            {
                if (IsPlacing?.Invoke() != true)
                {
                    Dragging = true;
                }
                else if (_pressOnNavMesh)
                {
                    Turning = true;
                }
            }

            if (Turning && HasGround)
            {
                Vector3 direction = Ground - TurnAnchor;
                if (direction.x * direction.x + direction.z * direction.z > 0.25f)
                {
                    TurnYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                }
            }

            if (Dragging)
            {
                DragRect = Rect.MinMaxRect(Mathf.Min(_dragStart.x, cursor.x), Mathf.Min(_dragStart.y, cursor.y), Mathf.Max(_dragStart.x, cursor.x), Mathf.Max(_dragStart.y, cursor.y));
            }

            if (Input.GetMouseButtonUp(0))
            {
                _leftDown = false;
                if (Dragging)
                {
                    Dragging = false;
                    BoxSelect(camera, DragRect, shift);
                }
                else if (Turning)
                {
                    Turning = false;
                    PlaceHandler?.Invoke(TurnAnchor, TurnYaw);
                }
                else if (ScreenClickHandler == null || !ScreenClickHandler(_dragStart))
                {
                    Click(shift);
                }
            }
        }

        if (Input.GetMouseButtonDown(1) && !overUi)
        {
            if (CancelHandler != null && CancelHandler())
            {
                return;
            }

            if (_selection.Count > 0)
            {
                OrderHandler?.Invoke(HasNavPoint ? NavPoint : Vector3.positiveInfinity, shift, ctrl);
            }
        }
    }

    public void ClearSelection()
    {
        _selection.Clear();
        _selected.Clear();
    }

    public void SelectOnly(Player player)
    {
        ClearSelection();
        Add(player);
    }

    public void Toggle(Player player)
    {
        if (_selected.Contains(player))
        {
            Deselect(player);
        }
        else
        {
            Add(player);
        }
    }

    public void Add(Player player)
    {
        if (player != null)
        {
            Select(player);
        }
    }

    private void Select(Player player)
    {
        if (_selected.Add(player))
        {
            _selection.Add(player);
        }
    }

    private void Deselect(Player player)
    {
        if (_selected.Remove(player))
        {
            _selection.Remove(player);
        }
    }

    private void PruneSelection()
    {
        for (int i = _selection.Count - 1; i >= 0; i--)
        {
            Player player = _selection[i];
            if (player == null || player.HealthController == null || !player.HealthController.IsAlive)
            {
                _selection.RemoveAt(i);
                _selected.Remove(player);
            }
        }
    }

    private void UpdateGround(Camera camera, Vector3 mouse)
    {
        Transform cameraTransform = camera.transform;
        Vector3 cameraPosition = cameraTransform.position;
        Quaternion cameraRotation = cameraTransform.rotation;
        if (mouse == _lastMouse && cameraPosition == _lastCameraPosition && cameraRotation == _lastCameraRotation)
        {
            return;
        }

        _lastMouse = mouse;
        _lastCameraPosition = cameraPosition;
        _lastCameraRotation = cameraRotation;

        Ray ray = camera.ViewportPointToRay(new Vector3(mouse.x / Screen.width, mouse.y / Screen.height, 0f));
        HasGround = Physics.Raycast(ray, out RaycastHit hit, 2000f, _groundMask, QueryTriggerInteraction.Ignore);
        Ground = HasGround ? hit.point : Vector3.zero;
        NavMeshHit navHit = default;
        HasNavPoint = HasGround && NavMesh.SamplePosition(hit.point, out navHit, 2f, NavMesh.AllAreas);
        NavPoint = HasNavPoint ? navHit.position : Vector3.zero;
    }

    private void Click(bool shift)
    {
        if (HasNavPoint && PlaceHandler != null && PlaceHandler(NavPoint, null))
        {
            return;
        }

        if (Hovered != null)
        {
            if (shift)
            {
                Toggle(Hovered);
            }
            else
            {
                SelectOnly(Hovered);
            }
            return;
        }

        if (!shift)
        {
            ClearSelection();
        }
    }

    private void BoxSelect(Camera camera, Rect rect, bool add)
    {
        if (!add)
        {
            ClearSelection();
        }

        if (!Singleton<GameWorld>.Instantiated)
        {
            return;
        }

        List<Player> players = Singleton<GameWorld>.Instance.AllAlivePlayersList;
        for (int i = 0; i < players.Count; i++)
        {
            Player player = players[i];
            if (player == null || player.IsYourPlayer || !Units.Bots.IsBot(player) || _selected.Contains(player))
            {
                continue;
            }

            if (ToScreen(camera, player.Position + Vector3.up * 0.9f, out Vector2 screen) && rect.Contains(screen))
            {
                Select(player);
            }
        }
    }

    public static bool ToScreen(Camera camera, Vector3 world, out Vector2 screen)
    {
        Vector3 viewport = camera.WorldToViewportPoint(world);
        screen = new Vector2(viewport.x * Screen.width, (1f - viewport.y) * Screen.height);
        return viewport.z > 0f;
    }

    private static Player FindHovered(Camera camera, Vector2 cursor)
    {
        if (!Singleton<GameWorld>.Instantiated)
        {
            return null;
        }

        Player best = null;
        float bestDistance = PickRadius * PickRadius;

        List<Player> players = Singleton<GameWorld>.Instance.AllAlivePlayersList;
        for (int p = 0; p < players.Count; p++)
        {
            Player player = players[p];
            if (player == null || player.IsYourPlayer)
            {
                continue;
            }

            Vector3 position = player.Position;
            for (int i = 0; i < 3; i++)
            {
                if (!ToScreen(camera, position + Vector3.up * (0.3f + i * 0.6f), out Vector2 screen))
                {
                    break;
                }

                float distance = (screen - cursor).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = player;
                }
            }
        }

        return best;
    }
}
