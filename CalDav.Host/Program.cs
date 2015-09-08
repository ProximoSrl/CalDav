using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CalDav.Host.Support;
using Topshelf;

namespace CalDav.Host
{
    /// <summary>
    /// 
    /// </summary>
    class Program
    {
        private const string ServiceDescriptiveName = "Jarvis - CalDAV service";
        private const string ServiceName = "JarvisCalDAV";

        static void Main(string[] args)
        {
            Start();
        }

        private static TopshelfExitCode Start()
        {
            if (Environment.UserInteractive)
            {
                Console.Title = "CalDAV Service";
                Console.BackgroundColor = ConsoleColor.DarkMagenta;
                Console.Clear();
                Banner();
            }

            return HostFactory.Run(x =>
            {
                x.Service<HostInstaller>(s =>
                {
                    s.ConstructUsing(name => new HostInstaller());
                    s.WhenStarted(tc => tc.Start());
                    s.WhenStopped(tc => tc.Stop());
                });
                x.RunAsLocalSystem();
                x.SetDescription(ServiceDescriptiveName);
                x.SetDisplayName(ServiceDescriptiveName);
                x.SetServiceName(ServiceName);
            });
        }

        private static void Banner()
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("===================================================================");
            Console.WriteLine("CalDAV service - Proximo srl");
            Console.WriteLine("===================================================================");
            Console.WriteLine("  install                   -> Installa il servizio");
            Console.WriteLine("  uninstall                 -> Rimuove il servizio");
            Console.WriteLine("  net start Intranet        -> Avvia il servizio");
            Console.WriteLine("  net stop Intranet         -> Arresta il servizio");
            Console.WriteLine("===================================================================");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("===================================================================");
            Console.WriteLine("TODO: Convertire controller MVC a WebApi!!!!");
            Console.WriteLine("===================================================================");
            Console.WriteLine();
        }

    }
}
