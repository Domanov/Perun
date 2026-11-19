using System.Collections.Generic;
using Comfort.Common;
using EFT;

namespace Perun.Client.Mode;

internal static class AiIgnore
{
    private static Player _player;
    private static readonly List<(BotsGroup group, EBotEnemyCause cause)> Removed = new();

    public static bool IsIgnored(IPlayer person) => _player != null && person != null && person.ProfileId == _player.ProfileId;

    public static void Enable(Player player)
    {
        Disable(false);
        _player = player;

        BotsController controller = Singleton<IBotGame>.Instantiated ? Singleton<IBotGame>.Instance.BotsController : null;
        if (controller?.Bots?.BotOwners == null)
        {
            return;
        }

        HashSet<BotsGroup> groups = new();
        foreach (BotOwner bot in controller.Bots.BotOwners)
        {
            if (bot != null && bot.BotsGroup != null)
            {
                groups.Add(bot.BotsGroup);
            }
        }

        foreach (BotsGroup group in groups)
        {
            IPlayer key = null;
            foreach (KeyValuePair<IPlayer, BotGroupEnemyInfo> enemy in group.Enemies)
            {
                if (enemy.Key != null && enemy.Key.ProfileId == player.ProfileId)
                {
                    key = enemy.Key;
                    Removed.Add((group, enemy.Value?.Cause ?? EBotEnemyCause.initial));
                    break;
                }
            }

            if (key != null)
            {
                group.RemoveEnemy(key, EBotEnemyCause.initial);
            }
        }

        if (Plugin.Verbose.Value)
        {
            Plugin.Log.LogInfo($"AI ignore: removed you from {Removed.Count} of {groups.Count} bot group(s)");
        }
    }

    public static void Disable(bool restoreEnemies = true)
    {
        Player player = _player;
        _player = null;

        if (restoreEnemies && player != null && player.HealthController != null && player.HealthController.IsAlive)
        {
            int restored = 0;
            foreach ((BotsGroup group, EBotEnemyCause cause) in Removed)
            {
                if (group != null && group.MembersCount > 0 && group.AddEnemy(player, cause))
                {
                    restored++;
                }
            }

            if (Plugin.Verbose.Value)
            {
                Plugin.Log.LogInfo($"AI ignore: {restored} bot group(s) hostile again");
            }
        }

        Removed.Clear();
    }
}
