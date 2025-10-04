using ProjectExample.Models;
using System.Threading;

namespace ProjectExample.Services
{
    public class ProcessService
    {
        private readonly DataService _dataService = new();
        private readonly LogService _logService = new();

        public void ProcessAll()
        {
            _logService.Write("Iniciando processamento...");
            Thread.Sleep(500);

            var items = _dataService.LoadItems();

            // For
            for (int i = 0; i < items.Count; i++)
            {
                ProcessItem(items[i]);
                Thread.Sleep(200);
            }

            // Foreach
            foreach (var item in items)
            {
                ProcessItem(item);
                Thread.Sleep(150);
            }

            // While
            int index = 0;
            while (index < items.Count)
            {
                ProcessItem(items[index]);
                index++;
                Thread.Sleep(100);
            }

            _logService.Write("Processamento finalizado.");
        }

        private void ProcessItem(Item item)
        {
            _logService.Write($"Processando item {item.Id} - {item.Name}");
            Thread.Sleep(120);
            var value = _dataService.CalculateValue(item);
            _logService.Write($"Valor calculado: {value}");
        }
    }
}
