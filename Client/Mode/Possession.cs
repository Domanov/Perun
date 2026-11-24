using EFT;
using Perun.Client.Compat;
using Perun.Client.Orders;
using UnityEngine;

namespace Perun.Client.Mode;

internal static class Possession
{
    private static GameObject _object;
    private static bool _sainDontUnstuck;

    public static BotOwner Bot { get; private set; }
    public static Player Body { get; private set; }
    public static object SainComponent { get; private set; }

    public static bool Active => Bot != null;

    public static bool JustWoke { get; private set; }

    public static bool IsPossessed(BotOwner bot) => bot != null && ReferenceEquals(bot, Bot);

    public static bool IsPossessedObject(GameObject gameObject) => _object != null && ReferenceEquals(gameObject, _object);

    public static bool IsPossessedShooter(ShootData shootData) => Bot != null && shootData != null && ReferenceEquals(Bot.ShootData, shootData);

    // Taking control clears the bot's IsAI; checks that tell bots from human players flip it back for their duration
    // (AsBotPatch), so friends by role (Black Division and the like) don't take the controlled bot for a hostile human.
    public static bool ShowAsBot()
    {
        if (Body != null && Body.AIData is AIData data && !data.IsAI)
        {
            data.IsAI = true;
            return true;
        }
        return false;
    }

    public static void EndShowAsBot(bool shown)
    {
        if (shown && Body != null && Body.AIData is AIData data)
        {
            data.IsAI = false;
        }
    }

    public static bool Begin(Player player)
    {
        End();
        BotOwner bot = player != null ? player.AIData?.BotOwner : null;
        if (bot == null || bot.IsDead || player.HealthController == null || !player.HealthController.IsAlive)
        {
            return false;
        }

        OrderService.Complete(bot);
        try
        {
            bot.Mover?.Stop();
            bot.ShootData?.EndShoot();
            if (player.HandsController is Player.FirearmController firearm && firearm.Blindfire)
            {
                firearm.BlindFire(0);
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"Control: could not stop {player.Profile?.Nickname}'s AI cleanly: {ex.Message}");
        }

        JustWoke = Units.StandByGuard.Hold(bot);
        Bot = bot;
        Body = player;
        _object = bot.gameObject;
        SainComponent = SainCompat.ComponentOf(bot);
        _sainDontUnstuck = SainCompat.SuspendUnstuck(SainComponent);
        SmoothieControl.Free(player);
        return true;
    }

    private static void AnchorMover(BotOwner bot, Player body)
    {
        if (bot != null && !bot.IsDead && body != null)
        {
            Units.Teleporting.Place(body, body.Position);
        }
    }

    public static void End()
    {
        Player body = Body;
        BotOwner owner = Bot;
        Units.StandByGuard.Release(owner);
        object sain = SainComponent;
        Bot = null;
        Body = null;
        _object = null;
        SainComponent = null;
        if (sain != null)
        {
            SainCompat.ResumeUnstuck(sain, _sainDontUnstuck);
        }
        _sainDontUnstuck = false;

        if (body == null)
        {
            return;
        }

        AnchorMover(owner, body);

        try
        {
            body.Move(Vector2.zero);
            body.EnableSprint(false);
            if (body.HandsController is Player.FirearmController firearm)
            {
                firearm.SetTriggerPressed(false);
                if (firearm.IsAiming)
                {
                    firearm.SetAim(false);
                }
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"Control: could not release {body.Profile?.Nickname} cleanly: {ex.Message}");
        }
    }
}
