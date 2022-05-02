using PhotoOrganizer.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace PhotoOrganizer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var logService = new LogService();

            var error = false;
            string source = string.Empty, destination = string.Empty;
            if (args.Any() && args.Length == 2)
            {
                source = args[0];
                destination = args[1];
            }
            else
            {
                source = GetValue("Enter source path:");
                if (!Directory.Exists(source))
                {
                    logService.Error("Directory not found");
                    error = true;
                }
                if (!error)
                {
                    destination = GetValue("Enter destination path:");
                }
            }

            if (!error)
            {
                try
                {
                    var copyService = new CopyService(logService);
                    copyService.Copy(source, destination);
                }
                catch (Exception ex)
                {
                    logService.Error(ex.Message);
                }
            }

            Console.WriteLine("Press <Enter> to continue...");
            Console.ReadLine();
        }

        static string GetValue(string message)
        {
            Console.WriteLine(message);
            return Console.ReadLine();
        }
    }
}
