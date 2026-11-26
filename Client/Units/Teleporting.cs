using System;
using System.Collections.Generic;
using System.Reflection;
using EFT;
using HarmonyLib;
using Perun.Client.Mode;
using Perun.Client.Orders;
using UnityEngine;

namespace Perun.Client.Units;

internal static class Teleporting
{
    private const string FieldPrevSuccessLinkedFrom = "_prevSuccessLinkedFrom";

    private static readonly FieldInfo LinkedFromField = Mode.Reflect.Field(Mode.Feature.TeleportHold, typeof(BotMover), FieldPrevSuccessLinkedFrom);
    private static readonly List<Player> Buffer = new();

    public static int Teleport(IReadOnlyList<Player> units, Vector3 point)
    {
        Buffer.Clear();
        for (int i = 0; i < units.Count; i++)
        {
            Player unit = units[i];
            if (unit != null && unit.HealthController != null && unit.HealthController.IsAlive && !ReferenceEquals(unit, Possession.Body))
            {
                Buffer.Add(unit);
            }
        }

        Buffer.Sort((a, b) => (a.Position - point).sqrMagnitude.CompareTo((b.Position - point).sqrMagnitude));
        OrderService.Halt(Buffer);
        int moved = 0;
        for (int i = 0; i < Buffer.Count; i++)
        {
            Player unit = Buffer[i];
            Vector3 slot = OrderService.Slot(point, i) + Vector3.up * 0.1f;
            if (unit.IsAI || unit.IsYourPlayer)
            {
                if (Place(unit, slot))
                {
                    moved++;
                }
            }
            else if (PerunNetwork.TeleportRemote != null && unit.ProfileId != null)
            {
                PerunNetwork.TeleportRemote(unit.ProfileId, slot);
                moved++;
            }
        }
        Buffer.Clear();
        return moved;
    }

    public static bool Place(Player player, Vector3 position)
    {
        try
        {
            BotOwner bot = player.AIData?.BotOwner;
            if (bot != null && !bot.IsDead && bot.Mover != null)
            {
                bot.Mover.Stop();
                LinkedFromField?.SetValue(bot.Mover, position);
                bot.Mover.Teleport(position);
            }
            else
            {
                player.Teleport(position);
            }
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Teleport failed for {player.Profile?.Nickname}: {ex.Message}");
            return false;
        }
    }
}
