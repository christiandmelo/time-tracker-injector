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

    public decimal CalculateValue(Item item, int tax)
    {
      Thread.Sleep(100);
      return ApplyTax(item.BaseValue, tax);
    }

    private decimal ApplyTax(decimal baseValue, int tax)
    {
      Thread.Sleep(80);

      baseValue = ChangeBaseValue(baseValue, tax);

      return baseValue * 1.15m;
    }

    private decimal ChangeBaseValue(decimal baseValue, int tax)
    {
      switch (tax)
      {
        case 1:
          Thread.Sleep(10);
          break;
        case 2:
          Thread.Sleep(20);
          break;
        case 3:
          Thread.Sleep(30);
          break;
        default:
          Thread.Sleep(500);
          break;
      }


      if (baseValue <= 10) {
        Thread.Sleep(10);
        return (baseValue * 2)*tax;
      }

      if (baseValue <= 20)
      {
        Thread.Sleep(20);
        return (baseValue * 3) * tax;
      }

      Thread.Sleep(40);
      return (baseValue * 4) * tax;
    }
  }
}
