using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System.Windows.Forms;

namespace DivCardTrader;

public class DivCardTraderSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    public ToggleNode DebugMode { get; set; } = new ToggleNode(false);
    [Menu("Run Key: ")]
    public HotkeyNode RunKey { get; set; } = new HotkeyNode(Keys.F7);
    [Menu("Extra Delay")]
    public RangeNode<int> ExtraDelay { get; set; } = new RangeNode<int>(50, 0, 500);
    [Menu("Use Thread.Sleep", "Is a little faster, but HUD will hang while clicking")]
    public ToggleNode UseThreadSleep { get; set; } = new(false);
    [Menu("Cancel With Right Click")]
    public ToggleNode CancelWithRightMouseButton { get; set; } = new ToggleNode(true);
}