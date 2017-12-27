using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Command = CriminalDanceBot.Attributes.Command;
using Telegram.Bot.Types;
using Database;
using CriminalDanceBot.Handlers;
using Telegram.Bot.Types.Enums;

namespace CriminalDanceBot
{
    public partial class Commands
    {
        [Command(Trigger = "ping")]
        public static void Ping(Message msg, string[] args)
        {
            var now = DateTime.UtcNow;
            var span1 = now - msg.Date.ToUniversalTime();
            var ping = msg.Reply($"Time to receive: {span1.ToString("mm\\:ss\\.ff")}");
            // var span2 = ping.Date.ToUniversalTime() - now;
            // Bot.Edit(ping.Text + $"{Environment.NewLine}Time to send: {span2.ToString("mm\\:ss\\.ff")}", ping);

        }

        [Command(Trigger = "lang", DevOnly = true)]
        public static void ChangeLang(Message msg, string[] args)
        {
            if (args == null)
                return;
            var lang = args[1];
            try
            {
                using (var db = new CrimDanceDb())
                {
                    var p = db.Players.FirstOrDefault(x => x.TelegramId == msg.From.Id);
                    if (p != null)
                    {
                        p.Language = lang;
                        db.SaveChanges();
                        Bot.Send(msg.Chat.Id, "OK");
                    }
                }
            }
            catch { }
        }

        [Command(Trigger = "grouplang", DevOnly = true)]
        public static void ChangeGroupLang(Message msg, string[] args)
        {
            if (args == null)
                return;
            var lang = args[1];
            try
            {
                using (var db = new CrimDanceDb())
                {
                    var p = db.Groups.FirstOrDefault(x => x.GroupId == msg.Chat.Id);
                    if (p != null)
                    {
                        p.Language = lang;
                        db.SaveChanges();
                        Bot.Send(msg.Chat.Id, "OK");
                    }
                }
            }
            catch { }
        }

        [Command(Trigger = "config", GroupOnly = true, AdminOnly = true)]
        public static void Config(Message msg, string[] args)
        {
            var id = msg.Chat.Id;

            //make sure the group is in the database
            using (var db = new CrimDanceDb())
            {
                var grp = db.Groups.FirstOrDefault(x => x.GroupId == id);
                if (grp == null)
                {
                    grp = Helper.MakeDefaultGroup(msg.Chat);
                    db.Groups.Add(grp);
                }

                grp.UserName = msg.Chat.Username;
                grp.Name = msg.Chat.Title;
                db.SaveChanges();
            }

            var menu = Handler.GetConfigMenu(msg.Chat.Id);
            Bot.Send(msg.From.Id, GetTranslation("WhatToDo", GetLanguage(msg.From.Id)), replyMarkup: menu);
        }

        [Command(Trigger = "maintenance", DevOnly = true)]
        public static void Maintenance(Message msg, string[] args)
        {
            Program.MaintMode = !Program.MaintMode;
            Bot.Send(msg.Chat.Id, $"Maintenance Mode: {Program.MaintMode}");
        }

        [Command(Trigger = "getlang")]
        public static void GetLang(Message msg, string[] args)
        {
            if (!Constants.Dev.Contains(msg.From.Id) && msg.Chat.Type != ChatType.Private) return;

            Bot.Send(msg.Chat.Id, GetTranslation("GetWhichLang", GetLanguage(msg.Chat.Id)), Handler.GetGetLangMenu());
        }

        [Command(Trigger = "reloadlangs", DevOnly = true)]
        public static void ReloadLang(Message msg, string[] args)
        {
            Program.English = Helper.ReadEnglish();
            Program.Langs = Helper.ReadLanguageFiles();
            msg.Reply("Done.");
        }

        [Command(Trigger = "uploadlang", DevOnly = true)]
        public static void UploadLang(Message msg, string[] args)
        {
            try
            {
                var id = msg.Chat.Id;
                if (msg.ReplyToMessage?.Type != MessageType.DocumentMessage)
                {
                    Bot.Send(id, "Please reply to the file with /uploadlang");
                    return;
                }
                var fileid = msg.ReplyToMessage.Document?.FileId;
                if (fileid != null)
                    Commands.UploadFile(fileid, id,
                        msg.ReplyToMessage.Document.FileName,
                        msg.MessageId);
            }
            catch (Exception e)
            {
                Bot.Send(msg.Chat.Id, e.Message, parseMode: ParseMode.Default);
            }
        }
    }
}
