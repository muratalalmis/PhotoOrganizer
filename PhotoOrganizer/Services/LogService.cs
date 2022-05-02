using System;

namespace PhotoOrganizer.Services
{
    internal class LogService
    {
        internal void Info(string messsage)
        {
            Console.WriteLine(messsage);
        }

        internal void Warn(string messsage)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(messsage);
            Console.ResetColor();
        }

        internal void Error(string messsage)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(messsage);
            Console.ResetColor();
        }
    }
}
