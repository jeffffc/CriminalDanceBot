using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CriminalDanceBot
{
    public static class Helper
    {
        public static void LogError(this Exception e)
        {
#if DEBUG
            /*
            Console.BackgroundColor = ConsoleColor.DarkRed;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("================================");
            Console.WriteLine($"Message: {e.Message}");
            Console.WriteLine($"Source: {e.Source}");
            */
            Exception err;
            string m  = $"Message: `{e.Message}`\nSource: ```{e.Source}```\nStackTrace:\n```{e.StackTrace}```\n";
            err = e.InnerException;
            while (err != null)
            {
                err = err.InnerException;
                m += $"```{err.StackTrace}```\n";
            }
            /*
            Console.WriteLine($"StackTrace:\n{m}");
            Console.WriteLine("================================");
            Console.ResetColor();
            */
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
        
        public static List<T> Shuffle<T>(this List<T> list)
        {
            RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
            int n = list.Count;
            while (n > 1)
            {
                byte[] box = new byte[1];
                do provider.GetBytes(box);
                while (!(box[0] < n * (Byte.MaxValue / n)));
                int k = (box[0] % n);
                n--;
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
            return list;
        }

        public static int RandomNum(int size)
        {
            Random rnd = new Random();
            return rnd.Next(1, size + 1);
        }
    }
}
