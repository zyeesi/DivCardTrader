using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

#pragma warning disable CA1416 // Validate platform compatibility
namespace DivCardTrader {
    public class Settings : ISettings
    {
        // ------------------------------------------------------------------
        [Menu("Enable", 1000)]
        public ToggleNode Enable { get; set; } = new ToggleNode(false);
        [Menu("Run Key: ", 1000)]
        public HotkeyNode RunKey { get; set; } = new HotkeyNode(Keys.F7);
        [Menu("Extra Delay", 1000)]
        public RangeNode<int> ExtraDelay { get; set; } = new RangeNode<int>(50, 0, 1000);

    }
}
#pragma warning restore CA1416 // Validate platform compatibility
