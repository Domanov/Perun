using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using Fika.Core.Main.FreeCamera;
using UnityEngine;
// The game has its own FreeCamera in the global namespace, which wins over a using directive.
using FikaFreeCamera = Fika.Core.Main.FreeCamera.FreeCamera;

namespace Perun.Fika;

// After you die, FreeCameraController switches on FreeCamera (it moves the main camera every frame) and shows its UI and
// the extract message. RTS mode takes that camera, so they are paused and put back as they were when it ends.
internal sealed class DeathCamera
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly FieldInfo FreeCamField = typeof(FreeCameraController).GetField("_freeCamScript", Private);
    private static readonly FieldInfo ExtractTextField = typeof(FreeCameraController).GetField("_extractText", Private);
    private static readonly FieldInfo UiField = typeof(FikaFreeCamera).GetField("_freecamUI", Private);

    private readonly List<(Behaviour behaviour, bool enabled)> _paused = new();
    private readonly List<(GameObject gameObject, bool active)> _hidden = new();

    public static string Missing =>
        FreeCamField == null ? "FreeCameraController._freeCamScript" :
        ExtractTextField == null ? "FreeCameraController._extractText" :
        UiField == null ? "FreeCamera._freecamUI" : null;

    // Its death routine switches the camera on after a delay, from a coroutine that runs even while paused.
    public bool Suspend()
    {
        Resume();
        if (!Singleton<FreeCameraController>.Instantiated || !Singleton<FreeCameraController>.Instance.IsScriptActive)
        {
            return false;
        }

        FreeCameraController controller = Singleton<FreeCameraController>.Instance;
        Pause(controller);
        Hide(ExtractTextField.GetValue(controller) as Component);
        if (FreeCamField.GetValue(controller) is FikaFreeCamera freeCamera)
        {
            Pause(freeCamera);
            Hide(UiField.GetValue(freeCamera) as Component);
        }
        return true;
    }

    public void Resume()
    {
        foreach ((Behaviour behaviour, bool enabled) in _paused)
        {
            if (behaviour != null)
            {
                behaviour.enabled = enabled;
            }
        }
        foreach ((GameObject gameObject, bool active) in _hidden)
        {
            if (gameObject != null)
            {
                gameObject.SetActive(active);
            }
        }
        _paused.Clear();
        _hidden.Clear();
    }

    private void Pause(Behaviour behaviour)
    {
        _paused.Add((behaviour, behaviour.enabled));
        behaviour.enabled = false;
    }

    private void Hide(Component component)
    {
        if (component == null)
        {
            return;
        }
        GameObject gameObject = component.gameObject;
        _hidden.Add((gameObject, gameObject.activeSelf));
        gameObject.SetActive(false);
    }
}
