using System.Collections.Generic;
using System.Text;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;
using static Perun.Client.Orders.Constants.PerunOrderLayer;

namespace Perun.Client.Orders;

public class PerunOrderLayer : CustomLayer
{
    public const int LayerPriority = 95;

    public static readonly List<string> Brains = new()
    {
        BrainArenaFighter, BrainAssault, BrainBigPipe, BrainBirdEye, BrainBoarSniper, BrainBossBoar, BrainBossBully, BrainBossGluhar, BrainBossKojaniy,
        BrainBossKolontay, BrainBossPartisan, BrainBossSanitar, BrainBossTest, BrainBossZryachiy, BrainCursAssault, BrainExUsec, BrainFlKlnAslt,
        BrainFlZraychiy, BrainFollowerBully, BrainFollowerGluharAssault, BrainFollowerGluharProtect, BrainFollowerGluharScout, BrainFollowerKojaniy,
        BrainFollowerSanitar, BrainHelperAgro, BrainInfectedAssault, BrainKilla, BrainKillaAgro, BrainKnight, BrainKolonSec, BrainMarksman, BrainObdolbs,
        BrainOni, BrainPMC, BrainPeaceZryachiy, BrainPmcBear, BrainPmcUsec, BrainPrizrakSt, BrainPrstEvent, BrainRavangeZryachiy, BrainSctPredvst,
        BrainSectantPriest, BrainSectantWarrior, BrainTagilla, BrainTagillaAgro, BrainTagillaFollower
    };

    public PerunOrderLayer(BotOwner botOwner, int priority) : base(botOwner, priority)
    {
    }

    public override string GetName() => BrainLayerOrder;

    public override bool IsActive()
    {
        if (!OrderService.TryGet(BotOwner, out MoveOrder order))
        {
            return false;
        }

        return order.Forced || !InCombat(BotOwner);
    }

    public override Action GetNextAction() => new(typeof(PerunMoveLogic), BrainReasonOrder);

    public override bool IsCurrentActionEnding() => !OrderService.TryGet(BotOwner, out _);

    public override void BuildDebugText(StringBuilder stringBuilder)
    {
        if (OrderService.TryGet(BotOwner, out MoveOrder order))
        {
            stringBuilder.AppendLine(string.Format(DumpOrder, order.Destination, order.Forced, order.Run));
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
