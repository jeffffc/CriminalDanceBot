using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Database;
using System.IO;
using System.Net;
using System.Xml.Linq;
using Telegram.Bot.Types.Enums;
using System.Threading;
using Telegram.Bot.Types.InlineKeyboardButtons;
using Telegram.Bot.Types.ReplyMarkups;

namespace CriminalDanceBot
{
    public partial class Commands
    {
        /// <summary>
        /// Get the language for a chat. May be used for both players and groups
        /// </summary>
        public static string GetLanguage(long id)
        {
            using (var db = new CrimDanceDb())
            {
                Player p = null;
                var grp = db.Groups.FirstOrDefault(x => x.GroupId == id);
                if (grp == null)
                    p = db.Players.FirstOrDefault(x => x.TelegramId == id);
                if (p != null && String.IsNullOrEmpty(p.Language))
                {
                    p.Language = "English";
                    db.SaveChanges();
                }
                return grp?.Language ?? p?.Language ?? "English";
            }
        }

        public static string GetTranslation(string key, string language, params object[] args)
        {
            return Program.Translations.GetTranslation(key, language, args);
        }

        public static InlineKeyboardMarkup GenerateStartMe(int id)
        {
            var row = new List<InlineKeyboardButton>();
            var rows = new List<InlineKeyboardButton[]>();
            row.Add(new InlineKeyboardUrlButton(GetTranslation("StartMe", GetLanguage(id)), $"https://telegram.me/{Bot.Me.Username}"));
            rows.Add(row.ToArray());
            return new InlineKeyboardMarkup(rows.ToArray());
        }
    }
}
