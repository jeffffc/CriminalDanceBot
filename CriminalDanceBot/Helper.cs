using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CriminalDanceBot
{
    public static class Helper
    {
        public static void LogError(this Exception e)
        {
#if DEBUG
            Console.BackgroundColor = ConsoleColor.DarkRed;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("================================");
            Console.WriteLine($"Message: {e.Message}");
            Console.WriteLine($"Source: {e.Source}");
            Exception err;
            string m  = $"{e.StackTrace}\n";
            while (e.InnerException != null)
            {
                err = e.InnerException;
                m += $"{err.StackTrace}\n";
            }
            Console.WriteLine($"StackTrace:\n{m}");
            Console.WriteLine("================================");
            Console.ResetColor();

            Bot.Send(Constants.LogGroupId, m);
#else
            using (var sw = new StreamWriter(Constants.GetLogPath(), true))
            {
                sw.WriteLine("vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv");
                sw.WriteLine(DateTime.UtcNow);
                sw.WriteLine("================================");
                sw.WriteLine($"Message: {e.Message}");
                sw.WriteLine($"Source: {e.Source}");
                Exception err;
                string m = $"{e.StackTrace}\n";
                while (e.InnerException != null)
                {
                    err = e.InnerException;
                    m += $"{err.StackTrace}\n";
                }
                sw.WriteLine($"StackTrace:\n{m}");
                sw.WriteLine("^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^");
            }
#endif
        }
        
    }
}
