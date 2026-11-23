using System.Collections.Generic;
using System.Text;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;

namespace Perun.Client.Orders;

public class PerunOrderLayer : CustomLayer
{
    public const int LayerPriority = 95;

    public static readonly List<string> Brains = new()
    {
        "ArenaFighter", "Assault", "BigPipe", "BirdEye", "BoarSniper", "BossBoar", "BossBully", "BossGluhar", "BossKojaniy",
        "BossKolontay", "BossPartisan", "BossSanitar", "BossTest", "BossZryachiy", "CursAssault", "ExUsec", "FlKlnAslt",
        "Fl_Zraychiy", "FollowerBully", "FollowerGluharAssault", "FollowerGluharProtect", "FollowerGluharScout", "FollowerKojaniy",
        "FollowerSanitar", "HelperAgro", "InfectedAssault", "Killa", "KillaAgro", "Knight", "KolonSec", "Marksman", "Obdolbs",
        "Oni", "PMC", "PeaceZryachiy", "PmcBear", "PmcUsec", "PrizrakSt", "Prst event", "RavangeZryachiy", "SctPredvst",
        "SectantPriest", "SectantWarrior", "Tagilla", "TagillaAgro", "TagillaFollower"
    };

    public PerunOrderLayer(BotOwner botOwner, int priority) : base(botOwner, priority)
    {
    }

    public override string GetName() => "PerunOrder";

    public override bool IsActive()
    {
        if (!OrderService.TryGet(BotOwner, out MoveOrder order))
        {
            return false;
        }

        return order.Forced || !InCombat(BotOwner);
    }

    public override Action GetNextAction() => new(typeof(PerunMoveLogic), "Perun order");

    public override bool IsCurrentActionEnding() => !OrderService.TryGet(BotOwner, out _);

    public override void BuildDebugText(StringBuilder stringBuilder)
    {
        if (OrderService.TryGet(BotOwner, out MoveOrder order))
        {
            stringBuilder.AppendLine($"Perun order to {order.Destination} forced={order.Forced} run={order.Run}");
        }
    }

    internal static bool InCombat(BotOwner bot)
    {
        BotMemory memory = bot.Memory;
        if (memory == null)
        {
            return false;
        }

        if (memory.IsUnderFire)
        {
            return true;
        }

        EnemyInfo enemy = memory.GoalEnemy;
        return enemy != null && (enemy.IsVisible || Time.time - enemy.PersonalLastSeenTime < 4f);
    }
}
