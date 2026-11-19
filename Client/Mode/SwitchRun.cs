#nullable enable
namespace Perun.Client.Mode;

// Game callbacks for a weapon switch can arrive after the switch was cancelled or replaced; they must check
// that their run is still the current one before touching the player.
internal sealed class SwitchRun
{
    private int _current;

    public int Start() => ++_current;

    public void Cancel() => _current++;

    public bool IsCurrent(int run) => run == _current;
}
