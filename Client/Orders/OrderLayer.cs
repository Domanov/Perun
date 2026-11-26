using System.Collections.Generic;
using System.Text;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;

namespace Perun.Client.Orders;

public class PerunOrderLayer : CustomLayer
{
    private const string BrainArenaFighter = "ArenaFighter";
    private const string BrainAssault = "Assault";
    private const string BrainBigPipe = "BigPipe";
    private const string BrainBirdEye = "BirdEye";
    private const string BrainBoarSniper = "BoarSniper";
    private const string BrainBossBoar = "BossBoar";
    private const string BrainBossBully = "BossBully";
    private const string BrainBossGluhar = "BossGluhar";
    private const string BrainBossKojaniy = "BossKojaniy";
    private const string BrainBossKolontay = "BossKolontay";
    private const string BrainBossPartisan = "BossPartisan";
    private const string BrainBossSanitar = "BossSanitar";
    private const string BrainBossTest = "BossTest";
    private const string BrainBossZryachiy = "BossZryachiy";
    private const string BrainCursAssault = "CursAssault";
    private const string BrainExUsec = "ExUsec";
    private const string BrainFlKlnAslt = "FlKlnAslt";
    private const string BrainFlZraychiy = "Fl_Zraychiy";
    private const string BrainFollowerBully = "FollowerBully";
    private const string BrainFollowerGluharAssault = "FollowerGluharAssault";
    private const string BrainFollowerGluharProtect = "FollowerGluharProtect";
    private const string BrainFollowerGluharScout = "FollowerGluharScout";
    private const string BrainFollowerKojaniy = "FollowerKojaniy";
    private const string BrainFollowerSanitar = "FollowerSanitar";
    private const string BrainHelperAgro = "HelperAgro";
    private const string BrainInfectedAssault = "InfectedAssault";
    private const string BrainKilla = "Killa";
    private const string BrainKillaAgro = "KillaAgro";
    private const string BrainKnight = "Knight";
    private const string BrainKolonSec = "KolonSec";
    private const string BrainMarksman = "Marksman";
    private const string BrainObdolbs = "Obdolbs";
    private const string BrainOni = "Oni";
    private const string BrainPMC = "PMC";
    private const string BrainPeaceZryachiy = "PeaceZryachiy";
    private const string BrainPmcBear = "PmcBear";
    private const string BrainPmcUsec = "PmcUsec";
    private const string BrainPrizrakSt = "PrizrakSt";
    private const string BrainPrstEvent = "Prst event";
    private const string BrainRavangeZryachiy = "RavangeZryachiy";
    private const string BrainSctPredvst = "SctPredvst";
    private const string BrainSectantPriest = "SectantPriest";
    private const string BrainSectantWarrior = "SectantWarrior";
    private const string BrainTagilla = "Tagilla";
    private const string BrainTagillaAgro = "TagillaAgro";
    private const string BrainTagillaFollower = "TagillaFollower";
    private const string BrainLayerOrder = "PerunOrder";
    private const string BrainReasonOrder = "Perun order";

    private const string DumpOrder = "Perun order to {0} forced={1} run={2}";

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
