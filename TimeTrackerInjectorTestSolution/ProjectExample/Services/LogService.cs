using System;

namespace ProjectExample.Services
{
    public class LogService
    {
        public void Write(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}
