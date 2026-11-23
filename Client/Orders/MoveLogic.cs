using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;
using UnityEngine.AI;

namespace Perun.Client.Orders;

public class PerunMoveLogic : CustomLogic
{
    private const float ArriveDistance = 1.2f;
    private const float RepathSeconds = 2f;
    private const float StuckSeconds = 8f;
    private const int MaxRepaths = 6;

    private float _nextPathAt;

    public PerunMoveLogic(BotOwner botOwner) : base(botOwner)
    {
    }

    public override void Start()
    {
        _nextPathAt = 0f;
    }

    public override void Stop()
    {
        BotOwner.Mover?.Sprint(false);
    }

    public override void Update(CustomLayer.ActionData data)
    {
        if (!OrderService.TryGet(BotOwner, out MoveOrder order))
        {
            return;
        }

        Vector3 position = BotOwner.Position;
        Vector3 flat = order.Destination - position;
        float distance = new Vector2(flat.x, flat.z).magnitude;
        if (distance < ArriveDistance && Mathf.Abs(flat.y) < 2f)
        {
            OrderService.Complete(BotOwner);
            BotOwner.Mover.Sprint(false);
            BotOwner.Mover.Stop();
            return;
        }

        float now = Time.time;
        if (distance < order.BestDistance - 0.5f)
        {
            order.BestDistance = distance;
            order.LastProgressAt = now;
        }
        else if (now - order.LastProgressAt > StuckSeconds)
        {
            order.LastProgressAt = now;
            order.Repaths++;
            _nextPathAt = 0f;
            TraceStuck(order, distance);
        }

        if (order.Repaths > MaxRepaths)
        {
            Plugin.Log.LogWarning($"Order: {BotOwner.Profile.Nickname} can't reach {order.Destination}, giving up");
            OrderService.Complete(BotOwner);
            BotOwner.Mover.Stop();
            return;
        }

        BotOwner.SetPose(1f);
        BotOwner.SetTargetMoveSpeed(1f);
        BotOwner.Mover.Sprint(order.Run || order.Forced);
        BotOwner.Steering.LookToMovingDirection();

        if (now >= _nextPathAt)
        {
            _nextPathAt = now + RepathSeconds;
            NavMeshPathStatus status = BotOwner.GoToPoint(order.Destination, true, ArriveDistance * 0.8f, false, false);
            if (status == NavMeshPathStatus.PathInvalid)
            {
                order.Repaths++;
            }
        }
    }

    // Verbose only: what holds a bot in place while its order runs (combat pauses, a blocked mover, no path).
    private void TraceStuck(MoveOrder order, float distance)
    {
        if (!Plugin.Verbose.Value)
        {
            return;
        }
        BotMover mover = BotOwner.Mover;
        Plugin.Log.LogInfo(
            $"Order trace [{BotOwner.Profile.Nickname}]: {(order.Forced ? "forced" : order.Run ? "run" : "move")} {distance:F1} m left, try {order.Repaths}; " +
            $"layer {BrainManager.GetActiveLayerName(BotOwner)} / {BrainManager.GetActiveLogic(BotOwner)?.GetType().Name}; " +
            $"enemy {(BotOwner.Memory.GoalEnemy?.Person?.Profile?.Nickname ?? "none")}; " +
            $"mover moving {mover.IsMoving}, path {mover.HasPathAndNoComplete}, pause {mover.Pause} ({mover.RemainPause:F1} s), blocked {mover.Blocked}, speed {mover.DestMoveSpeed:F2}, sprint {mover.Sprinting}; " +
            $"pose {BotOwner.GetPlayer.MovementContext.PoseLevel:F2}, standby {BotOwner.StandBy?.StandByType}, leader {BotOwner.Boss?.IamBoss == true}, follows {BotOwner.BotFollower?.BossToFollow != null}");
    }
}
