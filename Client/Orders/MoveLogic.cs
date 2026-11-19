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
}
