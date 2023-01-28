using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DivCardTrader
{
    public class ItemRepo {
        public List<Item> Items { get; }
        public int Total { get; set; } = 0;

        public ItemRepo() {
            Items = new List<Item>();
        }

        public void AddItem(Item item) {
            Items.Add(item);
            Total++;
        }

        public void RemoveItem(Item item) {
            Items.Remove(item);
        }
    }
}
