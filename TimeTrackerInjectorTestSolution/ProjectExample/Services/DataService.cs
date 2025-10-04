using ProjectExample.Models;
using System.Threading;

namespace ProjectExample.Services
{
    public class DataService
    {
        public List<Item> LoadItems()
        {
            Thread.Sleep(300);
            return new List<Item>
            {
                new Item { Id = 1, Name = "Item A", BaseValue = 10 },
                new Item { Id = 2, Name = "Item B", BaseValue = 20 },
                new Item { Id = 3, Name = "Item C", BaseValue = 30 }
            };
        }

        public decimal CalculateValue(Item item)
        {
            Thread.Sleep(100);
            return ApplyTax(item.BaseValue);
        }

        private decimal ApplyTax(decimal baseValue)
        {
            Thread.Sleep(80);
            return baseValue * 1.15m;
        }
    }
}
