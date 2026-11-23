using System;
using System.Collections.Generic;
using EFT;
using HarmonyLib;
using UnityEngine;

namespace Perun.Client.Units;

// Followers are attached by force, like BotBoss.OfferSelf minus its role-suitability check. The game only promotes a
// follower of the dead boss's own role, so Perun picks the successor for its groups.
internal static class GroupLeadership
{
    private const float AttachTimeout = 30f;

    internal sealed class Handle
    {
        public BotOwner Leader;
        public string Name;
        public List<BotOwner> Orphans;
    }

    private sealed class PendingAttach
    {
        public Handle Group;
        public BotOwner Member;
        public float Until;
    }

    private static readonly AccessTools.FieldRef<BotBoss, Action<BotOwner, FollowerStatusChange>> StatusChange =
        AccessTools.FieldRefAccess<BotBoss, Action<BotOwner, FollowerStatusChange>>("OnFollowerStatusChange");

    private static readonly List<Handle> Dead = new();
    private static readonly List<PendingAttach> Attaches = new();

    public static Handle Track(BotOwner leader, string name)
    {
        Handle handle = new() { Leader = leader, Name = name };
        Subscribe(handle);
        return handle;
    }

    // The spawn callback runs right after PreActivate; the bot's tactic and patrol data only exist once it is Active.
    public static void QueueAttach(Handle group, BotOwner member)
    {
        if (group != null && member != null)
        {
            Attaches.Add(new PendingAttach { Group = group, Member = member, Until = Time.time + AttachTimeout });
        }
    }

    public static void Tick()
    {
        if (Attaches.Count > 0)
        {
            for (int i = Attaches.Count - 1; i >= 0; i--)
            {
                PendingAttach attach = Attaches[i];
                BotOwner leader = attach.Group.Leader;
                if (!Alive(attach.Member) || !Alive(leader))
                {
                    Attaches.RemoveAt(i);
                    continue;
                }
                if (attach.Member.BotState != EBotState.Active || leader.BotState != EBotState.Active)
                {
                    if (Time.time > attach.Until)
                    {
                        Attaches.RemoveAt(i);
                        Plugin.Log.LogWarning($"Group {attach.Group.Name}: {attach.Member.Profile?.Nickname} never became active, not attached");
                    }
                    continue;
                }
                Attaches.RemoveAt(i);
                try
                {
                    Attach(leader, attach.Member);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Group {attach.Group.Name}: attaching {attach.Member.Profile?.Nickname} failed: {ex}");
                }
            }
        }

        if (Dead.Count > 0)
        {
            Handle[] due = Dead.ToArray();
            Dead.Clear();
            foreach (Handle handle in due)
            {
                try
                {
                    Promote(handle);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Group {handle.Name}: choosing a new leader failed: {ex}");
                }
            }
        }
    }

    public static void Clear()
    {
        Dead.Clear();
        Attaches.Clear();
    }

    private static bool Alive(BotOwner bot) =>
        bot != null && bot.BotState != EBotState.Disposed && bot.HealthController != null && bot.HealthController.IsAlive;

    private static void Attach(BotOwner leader, BotOwner member)
    {
        if (leader == member || leader.Boss == null || member.BotFollower == null)
        {
            return;
        }
        if (!leader.Boss.IamBoss)
        {
            leader.Boss.SetBoss(1);
        }
        List<BotOwner> followers = leader.Boss._followers.Followers;
        if (!followers.Contains(member))
        {
            leader.Boss._followers.AddFollower(member);
            StatusChange(leader.Boss)?.Invoke(member, FollowerStatusChange.Add);
        }
        member.BotFollower.SetToFollow(leader.Boss, followers.IndexOf(member), false);
        Plugin.Log.LogInfo($"Group: {member.Profile.Nickname} ({member.Profile.Info.Settings.Role}) follows {leader.Profile.Nickname}");
    }

    private static void Subscribe(Handle handle)
    {
        handle.Leader.Boss.OnBossDead += (_, followers) =>
        {
            handle.Orphans = followers != null ? new List<BotOwner>(followers) : new List<BotOwner>();
            Dead.Add(handle);
        };
    }

    private static void Promote(Handle handle)
    {
        List<BotOwner> alive = handle.Orphans.FindAll(Alive);
        if (alive.Count == 0)
        {
            return;
        }

        BotOwner next = alive.Find(b => b.Boss.IamBoss)
            ?? alive.Find(b => b.Profile.Info.Settings.Role.IsBoss())
            ?? alive[0];
        if (!next.Boss.IamBoss)
        {
            // Still following the dead leader, whose patrol data is gone; SetBoss would read it.
            next.BotFollower.BossToFollow = null;
            next.Boss.SetBoss(alive.Count - 1);
        }
        handle.Leader = next;
        Subscribe(handle);

        foreach (BotOwner bot in alive)
        {
            if (bot != next)
            {
                QueueAttach(handle, bot);
            }
        }
        Plugin.Log.LogInfo($"Group {handle.Name}: leader died, {next.Profile.Nickname} ({next.Profile.Info.Settings.Role}) leads {alive.Count - 1} follower(s)");
    }
}
