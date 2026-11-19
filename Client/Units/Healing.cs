using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EFT;
using EFT.HealthSystem;

namespace Perun.Client.Units;

internal static class Healing
{
    private static readonly EBodyPart[] Parts =
    {
        EBodyPart.Head, EBodyPart.Chest, EBodyPart.Stomach, EBodyPart.LeftArm, EBodyPart.RightArm, EBodyPart.LeftLeg, EBodyPart.RightLeg
    };

    public static int Heal(IReadOnlyList<Player> units)
    {
        int healed = 0;
        for (int i = 0; i < units.Count; i++)
        {
            Player unit = units[i];
            if (unit == null || unit.HealthController == null || !unit.HealthController.IsAlive)
            {
                continue;
            }

            if (unit.IsYourPlayer || unit.IsAI)
            {
                if (HealLocal(unit))
                {
                    healed++;
                }
            }
            else if (PerunNetwork.HealRemote != null && unit.ProfileId != null)
            {
                PerunNetwork.HealRemote(unit.ProfileId);
                healed++;
            }
        }
        return healed;
    }

    private static void RefreshBody(Player player)
    {
        if (player == null || player.HealthController == null || !player.HealthController.IsAlive)
        {
            return;
        }
        try
        {
            player.UpdateSpeedLimitByHealth();
            player.UpdateArmsCondition();
            var movement = player.MovementContext;
            movement.SetPhysicalCondition(EPhysicalCondition.Tremor, false);
            movement.SetPhysicalCondition(EPhysicalCondition.Panic, false);
            movement.SetPhysicalCondition(EPhysicalCondition.HealingLegs, false);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Heal: couldn't refresh {player.Profile?.Nickname}'s movement: {ex.Message}");
        }
    }

    private static async void RefreshLater(Player player)
    {
        try
        {
            await Task.Delay(1500, Mode.RaidScope.Token);
            RefreshBody(player);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Heal: the delayed refresh failed: {ex.Message}");
        }
    }

    public static bool HealLocal(Player player)
    {
        ActiveHealthController health = player?.ActiveHealthController;
        if (health == null || !health.IsAlive)
        {
            return false;
        }

        try
        {
            foreach (EBodyPart part in Parts)
            {
                health.RemoveNegativeEffects(part);
                health.FullRestoreBodyPart(part);
            }
            health.RemoveNegativeEffects(EBodyPart.Common);
            health.ChangeEnergy(health.Energy.Maximum);
            health.ChangeHydration(health.Hydration.Maximum);
            RefreshBody(player);
            RefreshLater(player);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Heal failed for {player.Profile?.Nickname}: {ex.Message}");
            return false;
        }
    }
}
