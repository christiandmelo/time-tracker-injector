using ProjectExample.Services;

namespace ProjectExample
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var process = new ProcessService();
            process.ProcessAll();
        }
    }
}
