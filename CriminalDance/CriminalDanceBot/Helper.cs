using CriminalDanceBot.Models;
using Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CriminalDanceBot
{
    public static class Helper
    {
        public static void LogError(this Exception e, long chatId = 0)
        {
#if DEBUG
            /*
            Console.BackgroundColor = ConsoleColor.DarkRed;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("================================");
            Console.WriteLine($"Message: {e.Message}");
            Console.WriteLine($"Source: {e.Source}");
            */
            //Exception err;
            // string m  = $"Message: <code>{e.Message}</code>\nSource: <code>{e.Source}</code>\nStackTrace:\n{e.StackTrace}\n";
            /*err = e.InnerException;
            while (err != null)
            {
                m += $"{err.StackTrace}\n";
                err = err.InnerException;
            }*/
            /*
            Console.WriteLine($"StackTrace:\n{m}");
            Console.WriteLine("================================");
            Console.ResetColor();

            */
            string m = "Error occured." + Environment.NewLine;
            if (chatId > 0)
                m += $"ChatId: {chatId}" + Environment.NewLine + Environment.NewLine;
            var trace = e.StackTrace;
            do
            {
                m += e.Message + Environment.NewLine + Environment.NewLine;
                e = e.InnerException;
            }
            while (e != null);

            m += trace;

            Bot.Send(Constants.LogGroupId, m, parseMode: ParseMode.Default);
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
            return rnd.Next(0, size);
        }

        public static string ToBold(this object str)
        {
            if (str == null)
                return null;
            return $"<b>{str.ToString().FormatHTML()}</b>";
        }

        public static string ToItalic(this object str)
        {
            if (str == null)
                return null;
            return $"<i>{str.ToString().FormatHTML()}</i>";
        }

        public static string FormatHTML(this string str)
        {
            return str.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        public static string GetName(this XPlayer player)
        {
            var name = player.Name;
            if (!String.IsNullOrEmpty(player.Username))
                return $"<a href=\"https://telegram.me/{player.Username}\">{name.FormatHTML()}</a>";
            return name.ToBold();
        }

        public static string GetName(this User player)
        {
            var name = player.FirstName;
            if (!String.IsNullOrEmpty(player.Username))
                return $"<a href=\"https://telegram.me/{player.Username}\">{name.FormatHTML()}</a>";
            return name.ToBold();
        }

        public static bool IsGroupAdmin(Message msg, bool IgnoreDev = false)
        {
            if (msg.Chat.Type == ChatType.Private) return false;
            if (msg.Chat.Type == ChatType.Channel) return false;
            return IsGroupAdmin(msg.From.Id, msg.Chat.Id, IgnoreDev);
        }

        public static bool IsGroupAdmin(int userid, long chatid, bool IgnoreDev = false)
        {
            if (Constants.Dev.Contains(userid) && !IgnoreDev) return true;

            var admins = Bot.Api.GetChatAdministratorsAsync(chatid).Result;
            if (admins.Any(x => x.User.Id == userid)) return true;
            return false;
        }


        public static Group MakeDefaultGroup(Chat chat)
        {
            return new Group
            {
                GroupId = chat.Id,
                Name = chat.Title,
                Language = "English",
                CreatedBy = "Command",
                CreatedTime = DateTime.UtcNow,
                UserName = chat.Username,
                GroupLink = chat.Username == "" ? $"https://telegram.me/{chat.Username}" : null
            };
        }

        public static Player MakeDefaultPlayer(User user)
        {
            return new Player
            {
                Name = user.FirstName,
                TelegramId = user.Id,
                UserName = user.Username,
                Language = "English"
            };
        }
    }
}
