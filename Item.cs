using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.Models;
using SharpDX;

namespace DivCardTrader
{
    public class Item
    {
        public NormalInventoryItem InventoryItem { get; }
        public string BaseName { get; }
        public string ClassName { get; }
        public int Size { get; set; }
        public bool FullStack { get; set; }
        public Vector2 ClientRect { get; }

        public Item(NormalInventoryItem inventroyItem, BaseItemType baseItemType)
        {
            InventoryItem = inventroyItem;
            BaseName = baseItemType.BaseName;
            ClassName = baseItemType.ClassName;
            Size = inventroyItem.Item.GetComponent<Stack>().Size;
            FullStack = inventroyItem.Item.GetComponent<Stack>().FullStack;
            ClientRect = InventoryItem.GetClientRect().Center;
        }
    }
}
