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
        public readonly List<BotOwner> Members = new();
    }

    private sealed class PendingAttach
    {
        public Handle Group;
        public BotOwner Member;
        public float Until;
    }

    private sealed class PendingForm
    {
        public List<BotOwner> Bots;
        public string Name;
        public float Until;
    }

    private static readonly AccessTools.FieldRef<BotBoss, Action<BotOwner, FollowerStatusChange>> StatusChange =
        AccessTools.FieldRefAccess<BotBoss, Action<BotOwner, FollowerStatusChange>>("OnFollowerStatusChange");

    private static readonly System.Reflection.MethodInfo SetBotsGroup = AccessTools.PropertySetter(typeof(BotOwner), nameof(BotOwner.BotsGroup));

    private static readonly List<Handle> Groups = new();
    private static readonly List<Handle> Dead = new();
    private static readonly List<PendingAttach> Attaches = new();
    private static readonly List<PendingForm> Forms = new();

    public static Handle Track(BotOwner leader, string name)
    {
        Handle handle = new() { Leader = leader, Name = name };
        handle.Members.Add(leader);
        Subscribe(handle);
        Groups.Add(handle);
        return handle;
    }

    public static BotOwner LeaderOf(BotsGroup group)
    {
        if (group == null)
        {
            return null;
        }
        foreach (Handle handle in Groups)
        {
            if (Alive(handle.Leader) && ReferenceEquals(handle.Leader.BotsGroup, group))
            {
                return handle.Leader;
            }
        }
        return null;
    }

    // The spawn callback runs right after PreActivate; the bot's tactic and patrol data only exist once it is Active.
    public static void QueueAttach(Handle group, BotOwner member)
    {
        if (group != null && member != null)
        {
            if (!group.Members.Contains(member))
            {
                group.Members.Add(member);
            }
            Attaches.Add(new PendingAttach { Group = group, Member = member, Until = Time.time + AttachTimeout });
        }
    }

    // Several bots spawned as one unit: the game links only bots whose roles suit each other, so once all are active they
    // are put under one leader.
    public static void Form(List<BotOwner> bots, string name)
    {
        if (bots.Count > 1)
        {
            Forms.Add(new PendingForm { Bots = bots, Name = name, Until = Time.time + AttachTimeout });
        }
    }

    public static void Tick()
    {
        for (int i = Forms.Count - 1; i >= 0; i--)
        {
            PendingForm form = Forms[i];
            form.Bots.RemoveAll(b => !Alive(b));
            if (form.Bots.Exists(b => b.BotState != EBotState.Active) && Time.time <= form.Until)
            {
                continue;
            }
            Forms.RemoveAt(i);
            try
            {
                FormNow(form.Name, form.Bots.FindAll(b => b.BotState == EBotState.Active));
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Group {form.Name}: forming the group failed: {ex}");
            }
        }

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
                    Attach(attach.Group, attach.Member);
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
                    Promote(handle, "leader died");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Group {handle.Name}: choosing a new leader failed: {ex}");
                }
            }
        }
    }

    // Puts a bot under another bot's lead, from the RTS link view. When the target follows someone, the bot joins that
    // leader instead, so a group keeps one leader. Returns why it couldn't, or null.
    public static string Join(BotOwner member, BotOwner target, out BotOwner leader)
    {
        leader = null;
        if (!Alive(member) || !Alive(target) || member == target)
        {
            return "both units must be living bots";
        }
        if (member.BotState != EBotState.Active || target.BotState != EBotState.Active)
        {
            return "both bots must be active";
        }

        leader = Bots.LeaderOf(target) ?? target;
        if (leader == member)
        {
            // Dropped on one of its own followers: it stays in this BotsGroup, under whoever succeeds it.
            leader = Leave(member) ?? target;
        }
        else
        {
            MoveGroup(member, leader.BotsGroup);
            Leave(member);
        }

        Handle handle = HandleOf(leader) ?? Track(leader, leader.Profile.Nickname);
        MakeLeader(leader, 1);
        QueueAttach(handle, member);
        Compat.SainCompat.JoinSquad(member);
        Compat.SainCompat.MatchLeader(leader);
        Plugin.Log.LogInfo($"Group: {member.Profile.Nickname} joins {leader.Profile.Nickname}'s group");
        return null;
    }

    // A boss role leads, else the bot the game already made boss, else the best equipped.
    private static void FormNow(string name, List<BotOwner> bots)
    {
        if (bots.Count < 2)
        {
            return;
        }
        BotOwner leader = bots.Find(b => b.Profile.Info.Settings.Role.IsBoss()) ?? bots.Find(b => b.Boss.IamBoss);
        if (leader == null)
        {
            leader = bots[0];
            foreach (BotOwner bot in bots)
            {
                if (bot.GetPlayer.AIData.PowerOfEquipment > leader.GetPlayer.AIData.PowerOfEquipment)
                {
                    leader = bot;
                }
            }
        }

        Handle handle = HandleOf(leader) ?? Track(leader, name);
        foreach (BotOwner bot in bots)
        {
            if (bot == leader)
            {
                continue;
            }
            MoveGroup(bot, leader.BotsGroup);
            if (bot.Boss.IamBoss)
            {
                DropFollowers(bot);
            }
            QueueAttach(handle, bot);
        }
        Plugin.Log.LogInfo($"Group {name}: {leader.Profile.Nickname} leads {bots.Count - 1} follower(s)");
    }

    // Takes a bot out of its group into a new one of its own (link view: dropped on the ground).
    public static string Separate(BotOwner member)
    {
        if (!Alive(member) || member.BotState != EBotState.Active)
        {
            return "it must be a living, active bot";
        }
        if (member.BotsGroup == null || (member.BotsGroup.MembersCount <= 1 && Bots.LeaderOf(member) == null))
        {
            return "it is already on its own";
        }

        BotsGroup group = SpawnService.LoneGroup(member);
        if (group == null)
        {
            return "no bot system in this raid";
        }
        MoveGroup(member, group);
        Leave(member);
        Compat.SainCompat.JoinSquad(member);
        Plugin.Log.LogInfo($"Group: {member.Profile.Nickname} leaves its group");
        return null;
    }

    private static Handle HandleOf(BotOwner leader)
    {
        foreach (Handle handle in Groups)
        {
            if (handle.Leader == leader)
            {
                return handle;
            }
        }
        return null;
    }

    // Out of its leader's followers; a leader hands its followers to a successor, which is returned. Call it after the bot
    // has moved to its new BotsGroup: the successor's SetBoss signs up every member of its BotsGroup as a follower.
    private static BotOwner Leave(BotOwner bot)
    {
        foreach (Handle handle in Groups)
        {
            handle.Members.Remove(bot);
        }
        Attaches.RemoveAll(a => a.Member == bot);

        if (bot.BotFollower?.BossToFollow is BotBoss boss)
        {
            boss.RemoveFollower(bot);
            bot.BotFollower.BossToFollow = null;
        }

        Handle old = HandleOf(bot);
        if (bot.Boss == null || !bot.Boss.IamBoss)
        {
            Groups.Remove(old);
            return null;
        }
        List<BotOwner> followers = DropFollowers(bot);
        followers.RemoveAll(f => !Alive(f));
        if (followers.Count == 0)
        {
            Groups.Remove(old);
            return null;
        }

        if (old == null)
        {
            old = new Handle { Leader = bot, Name = bot.Profile.Nickname };
            Groups.Add(old);
        }
        foreach (BotOwner follower in followers)
        {
            if (!old.Members.Contains(follower))
            {
                old.Members.Add(follower);
            }
        }
        Promote(old, "leader left");
        return old.Leader;
    }

    private static List<BotOwner> DropFollowers(BotOwner bot)
    {
        List<BotOwner> followers = new(bot.Boss._followers.Followers);
        foreach (BotOwner follower in followers)
        {
            bot.Boss.RemoveFollower(follower);
        }
        bot.Boss.IamBoss = false;
        return followers;
    }

    // The bot is switched to the new group before it leaves the old one: SAIN re-reads a removed member's group to find
    // its new squad. Enemies the two sides had already taken of each other are dropped.
    private static void MoveGroup(BotOwner bot, BotsGroup group)
    {
        BotsGroup old = bot.BotsGroup;
        if (group == null || old == group)
        {
            return;
        }
        SetBotsGroup.Invoke(bot, new object[] { group });
        group.AddMember(bot, false);
        old?.RemoveAlly(bot);

        Player body = bot.GetPlayer;
        group.RemoveEnemy(body);
        for (int i = 0; i < group.MembersCount; i++)
        {
            BotOwner other = group.Member(i);
            if (other == null || other == bot)
            {
                continue;
            }
            Forget(other, body);
            Forget(bot, other.GetPlayer);
        }
    }

    // The game's Remove indexes its enemy table without checking, so a bot that never saw the other throws.
    private static void Forget(BotOwner bot, Player enemy)
    {
        if (enemy == null)
        {
            return;
        }
        BotEnemiesController enemies = bot.EnemiesController;
        if (enemies != null && enemies.EnemyInfos.ContainsKey(enemy))
        {
            enemies.Remove(enemy);
        }
        Compat.SainCompat.ForgetEnemy(bot, enemy);
    }

    public static void Clear()
    {
        Groups.Clear();
        Dead.Clear();
        Attaches.Clear();
        Forms.Clear();
    }

    private static bool Alive(BotOwner bot) =>
        bot != null && bot.BotState != EBotState.Disposed && bot.HealthController != null && bot.HealthController.IsAlive;

    private static void Attach(Handle group, BotOwner member)
    {
        BotOwner leader = group.Leader;
        if (leader == member || leader.Boss == null || member.BotFollower == null)
        {
            return;
        }
        MakeLeader(leader, 1);
        List<BotOwner> followers = leader.Boss._followers.Followers;
        if (!followers.Contains(member))
        {
            leader.Boss._followers.AddFollower(member);
            NotifyAdded(leader, member);
        }
        // SetToFollow keeps an existing boss unless forced; after a succession that is the dead leader.
        if (!ReferenceEquals(member.BotFollower.BossToFollow, leader.Boss))
        {
            member.BotFollower.BossToFollow = null;
        }
        member.BotFollower.SetToFollow(leader.Boss, followers.IndexOf(member), false);
        StandByGuard.Join(leader, member);
        Compat.SainCompat.MatchLeader(leader);
        Plugin.Log.LogInfo($"Group: {member.Profile.Nickname} ({member.Profile.Info.Settings.Role}) follows {leader.Profile.Nickname}");
    }

    // A promoted follower's current patrol point can have no sub-points; the game's patrol then throws while picking the
    // new follower's first point (PatrolPoint.GetSubPoint(-1)), after it has already registered him. He still follows.
    private static void NotifyAdded(BotOwner leader, BotOwner member)
    {
        try
        {
            StatusChange(leader.Boss)?.Invoke(member, FollowerStatusChange.Add);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Plugin.Log.LogWarning($"Group: {leader.Profile.Nickname}'s patrol couldn't give {member.Profile.Nickname} a first point ({ex.Message}); attached anyway");
        }
    }

    // The successor comes from the group's own members: the game's follower list leaves out bots it dropped on its own
    // (one taken over and given back) and members still waiting to attach.
    // The handler stays on the bot after it stops leading this group; only its death as this group's leader counts.
    private static void Subscribe(Handle handle)
    {
        BotOwner leader = handle.Leader;
        leader.Boss.OnBossDead += (_, _) =>
        {
            if (handle.Leader == leader && !Dead.Contains(handle))
            {
                Dead.Add(handle);
            }
        };
    }

    // SetBoss reads the patrol data of the boss the bot still follows, which is gone after a death or a move.
    private static void MakeLeader(BotOwner bot, int followers)
    {
        if (!bot.Boss.IamBoss)
        {
            bot.BotFollower.BossToFollow = null;
            bot.Boss.SetBoss(followers);
        }
    }

    private static void Promote(Handle handle, string reason)
    {
        handle.Members.RemoveAll(m => !Alive(m));
        List<BotOwner> alive = new(handle.Members);
        if (alive.Count == 0)
        {
            Groups.Remove(handle);
            return;
        }

        BotOwner next = alive.Find(b => b.Boss.IamBoss)
            ?? alive.Find(b => b.Profile.Info.Settings.Role.IsBoss())
            ?? alive[0];
        MakeLeader(next, alive.Count - 1);
        handle.Leader = next;
        Subscribe(handle);
        Compat.SainCompat.MatchLeader(next);

        foreach (BotOwner bot in alive)
        {
            if (bot != next)
            {
                QueueAttach(handle, bot);
            }
        }
        Plugin.Log.LogInfo($"Group {handle.Name}: {reason}, {next.Profile.Nickname} ({next.Profile.Info.Settings.Role}) leads {alive.Count - 1} follower(s)");
    }
}
