using EFT;
using UnityEngine;

namespace Perun.Client.Mode;

internal sealed class BodyParking
{
    private const float ParkDepth = 75f;

    private Vector3 _savedPosition;
    private Vector2 _savedRotation;
    private Vector3 _parkPosition;
    private bool _parked;

    public Vector3 SavedPosition => _savedPosition;

    public void Park(Player player)
    {
        _savedPosition = player.Position;
        _savedRotation = player.Rotation;
        _parkPosition = _savedPosition + Vector3.down * ParkDepth;
        _parked = true;
        player.MovementContext.FreefallTime = 0f;
        player.Teleport(_parkPosition);
    }

    public void MoveReturn(Vector3 point)
    {
        _savedPosition = point;
        _parkPosition = point + Vector3.down * ParkDepth;
    }

    public void Hold(Player player)
    {
        if (!_parked)
        {
            return;
        }

        player.MovementContext.FreefallTime = 0f;
        if ((player.Position - _parkPosition).sqrMagnitude > 0.0625f)
        {
            player.Teleport(_parkPosition);
        }
    }

    public void Restore(Player player, Vector3? dropAt)
    {
        if (!_parked)
        {
            return;
        }

        _parked = false;
        MovementContext movement = player.MovementContext;
        movement.FreefallTime = 0f;
        player.Teleport(dropAt ?? _savedPosition);
        movement.IsGrounded = true;
        movement.FreefallTime = 0f;
        player.Rotation = _savedRotation;
    }

    public void Forget()
    {
        _parked = false;
    }
}
