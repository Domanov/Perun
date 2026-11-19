using EFT;
using EFT.HealthSystem;

namespace Perun.Client.Mode;

internal static class GodMode
{
    private static ActiveHealthController _controller;
    private static float _damageCoeff;
    private static bool _metabolismDisabled;

    public static bool IsProtected(ActiveHealthController controller) => controller != null && controller == _controller;

    public static bool IsProtected(Player player) => player != null && _controller != null && player.ActiveHealthController == _controller;

    public static bool Active => _controller != null;

    public static void Enable(Player player)
    {
        Disable();
        ActiveHealthController controller = player.ActiveHealthController;
        if (controller == null)
        {
            return;
        }

        _controller = controller;
        _damageCoeff = controller.DamageCoeff;
        _metabolismDisabled = controller.MetabolismDisabled;
        controller.SetDamageCoeff(0f);
        controller.MetabolismDisabled = true;
    }

    public static void Disable()
    {
        ActiveHealthController controller = _controller;
        _controller = null;
        if (controller == null)
        {
            return;
        }

        controller.SetDamageCoeff(_damageCoeff);
        controller.MetabolismDisabled = _metabolismDisabled;
    }
}
