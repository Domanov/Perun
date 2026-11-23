using System.Text;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using Perun.Client.Units;
using UnityEngine;
using UnityEngine.AI;

namespace Perun.Client.Orders;

// The game's follower patrol tracks the boss's patrol route, not where a Perun order walks him, so while the leader
// has an order his followers escort him to it.
public class PerunEscortLayer : CustomLayer
{
    public const int LayerPriority = PerunOrderLayer.LayerPriority - 1;

    public PerunEscortLayer(BotOwner botOwner, int priority) : base(botOwner, priority)
    {
    }

    public static BotOwner LeaderOf(BotOwner bot)
    {
        BotFollower follower = bot?.BotFollower;
        if (follower == null || !follower.HaveBoss || follower.BossToFollow is not BotBoss boss)
        {
            return null;
        }
        BotOwner leader = boss.Owner;
        return leader != null && leader != bot && !leader.IsDead ? leader : null;
    }

    public override string GetName() => "PerunEscort";

    public override bool IsActive()
    {
        if (OrderService.TryGet(BotOwner, out _))
        {
            return false;
        }
        BotOwner leader = LeaderOf(BotOwner);
        return leader != null && OrderService.TryGet(leader, out MoveOrder order) && (order.Forced || !PerunOrderLayer.InCombat(BotOwner));
    }

    public override Action GetNextAction() => new(typeof(PerunEscortLogic), "Perun escort");

    public override bool IsCurrentActionEnding() => !IsActive();

    public override void BuildDebugText(StringBuilder stringBuilder)
    {
        BotOwner leader = LeaderOf(BotOwner);
        if (leader != null)
        {
            stringBuilder.AppendLine($"Perun escort of {leader.Profile?.Nickname}");
        }
    }
}

// One path per order, to the follower's place in a wedge at the leader's destination. Cohesion comes from pace, not
// from re-pathing: every new path restarts the game's mover and makes the follower stutter.
public class PerunEscortLogic : CustomLogic
{
    private const float Spacing = 1.8f;
    private const float Arrive = 1f;
    private const float AheadSlack = 1f;
    private const float BehindSlack = 6f;
    private const float SlowSpeed = 0.35f;
    private const float StuckSeconds = 3f;
    private const int MaxRepaths = 4;
    private const float EyeHeight = 1.4f;

    private Vector3 _destination;
    private Vector3 _target;
    private Vector3 _heading = Vector3.forward;
    private float _lag;
    private bool _planned;
    private bool _arrived;
    private float _bestDistance;
    private float _progressAt;
    private int _repaths;

    public PerunEscortLogic(BotOwner botOwner) : base(botOwner)
    {
    }

    public override void Start()
    {
        _planned = false;
    }

    public override void Stop()
    {
        BotOwner.Mover?.Sprint(false);
        BotOwner.SetTargetMoveSpeed(1f);
    }

    public override void Update(CustomLayer.ActionData data)
    {
        BotOwner leader = PerunEscortLayer.LeaderOf(BotOwner);
        if (leader == null || !OrderService.TryGet(leader, out MoveOrder order))
        {
            return;
        }

        if (!_planned || order.Destination != _destination)
        {
            Plan(leader, order.Destination);
        }

        float left = Flat(_target - BotOwner.Position);
        if (!_arrived && left < Arrive)
        {
            _arrived = true;
            BotOwner.Mover.Stop();
        }
        if (_arrived)
        {
            BotOwner.Mover.Sprint(false);
            BotOwner.Steering.LookToPoint(BotOwner.Position + _heading * 10f + Vector3.up * EyeHeight);
            return;
        }

        if (!Progressing(left))
        {
            return;
        }

        // Distance along the march from the leader; the follower's wedge row keeps it _lag metres behind him.
        Vector3 offset = BotOwner.Position - leader.Position;
        offset.y = 0f;
        float along = Vector3.Dot(offset, _heading);
        bool ahead = along > -_lag + AheadSlack;
        bool behind = along < -_lag - BehindSlack;
        BotOwner.SetPose(1f);
        BotOwner.SetTargetMoveSpeed(ahead ? SlowSpeed : 1f);
        BotOwner.Mover.Sprint(!ahead && (order.Run || order.Forced || behind));
        BotOwner.Steering.LookToMovingDirection();
    }

    private void Plan(BotOwner leader, Vector3 destination)
    {
        _destination = destination;
        Vector3 heading = destination - leader.Position;
        heading.y = 0f;
        if (heading.sqrMagnitude > 1f)
        {
            _heading = heading.normalized;
        }

        Vector3 offset = Formation.Wedge(Mathf.Max(BotOwner.BotFollower.Index, 0) + 1, Spacing, _heading);
        Vector3 slot = destination + offset;
        _target = NavMesh.SamplePosition(slot, out NavMeshHit hit, 2f, NavMesh.AllAreas) ? hit.position : destination;
        _lag = -Vector3.Dot(offset, _heading);

        _planned = true;
        _arrived = false;
        _bestDistance = float.MaxValue;
        _progressAt = Time.time;
        _repaths = 0;
        Go();
    }

    // A follower that can't reach its place waits there instead of re-pathing forever.
    private bool Progressing(float left)
    {
        float now = Time.time;
        if (left < _bestDistance - 0.5f)
        {
            _bestDistance = left;
            _progressAt = now;
            return true;
        }
        if (now - _progressAt <= StuckSeconds)
        {
            return true;
        }
        if (++_repaths > MaxRepaths)
        {
            Plugin.Log.LogWarning($"Escort: {BotOwner.Profile.Nickname} can't reach its place near {_destination}, waiting");
            _arrived = true;
            BotOwner.Mover.Stop();
            return false;
        }
        _progressAt = now;
        Go();
        return true;
    }

    private void Go() => BotOwner.GoToPoint(_target, true, Arrive * 0.8f, false, false);

    private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
}
