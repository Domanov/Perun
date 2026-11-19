using UnityEngine;

namespace Perun.Client.UI;

internal sealed class PerunGui : MonoBehaviour
{
    public PerunHud Hud { get; set; }

    private void OnGUI()
    {
        try
        {
            Hud?.Draw();
        }
        catch (ExitGUIException)
        {
            throw;
        }
        catch (System.Exception ex)
        {
            ErrorGuard.Report("Interface", ex);
        }
    }
}
