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
using Telegram.Bot.Types.Payments;
using CriminalDanceBot.Models;
using System.IO;
using Telegram.Bot.Types.ReplyMarkups;

namespace CriminalDanceBot
{
    public partial class Commands
    {
        [Command(Trigger = "start")]
        public static void Start(Message msg, string[] args)
        {
            if (msg.Chat.Type != ChatType.Private) return;
            if (args.Length == 0)
            {
                msg.Reply("Thank you for starting me. This bot is still in BETA phase. Please find updates at @CriminalDance.");
            }
        }

        [Command(Trigger = "ping")]
        public static void Ping(Message msg, string[] args)
        {
            var now = DateTime.UtcNow;
            var span1 = now - msg.Date.ToUniversalTime();
            var ping = msg.Reply($"Time to receive: {span1.ToString("mm\\:ss\\.ff")}");
            var span2 = ping.Date.ToUniversalTime() - now;
            Bot.Edit(ping.Chat.Id, ping.MessageId, ping.Text + $"{Environment.NewLine}Time to send: {span2.ToString("mm\\:ss\\.ff")}");

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

        [Command(Trigger = "setlang")]
        public static void SetLang(Message msg, string[] args)
        {
            var id = msg.From.Id;

            //make sure the user is in the database
            using (var db = new CrimDanceDb())
            {
                var user = db.Players.FirstOrDefault(x => x.TelegramId == id);
                if (user == null)
                {
                    user = Helper.MakeDefaultPlayer(msg.From);
                    db.Players.Add(user);
                }

                user.UserName = msg.From.Username;
                user.Name = msg.From.FirstName;
                db.SaveChanges();
            }

            var menu = Handler.GetConfigLangMenu(msg.From.Id, true);
            Bot.Send(msg.From.Id, GetTranslation("ChoosePMLanguage", GetLanguage(msg.From.Id)), replyMarkup: menu);
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
        public static void ReloadLangs(Message msg, string[] args)
        {
            Program.Translations.ReloadLanguages();
            msg.Reply("Done.");
        }

        [Command(Trigger = "uploadlang", DevOnly = true)]
        public static void UploadLang(Message msg, string[] args)
        {
            try
            {
                var id = msg.Chat.Id;
                if (msg.ReplyToMessage?.Type != MessageType.Document || Path.GetExtension(msg.ReplyToMessage?.Document?.FileName ?? "") != ".xml")
                {
                    Bot.Send(id, "Please reply to the file with /uploadlang");
                    return;
                }
                var fileid = msg.ReplyToMessage.Document?.FileId;
                if (fileid != null)
                {
                    msg.ReplyNoQuote(Program.Translations.PrepareUploadLanguage(msg.ReplyToMessage.Document.FileId, msg.ReplyToMessage.Document.FileName, out bool CanUpload));
                    if (CanUpload)
                    {
                        var filename = Path.GetFileNameWithoutExtension(msg.ReplyToMessage.Document.FileName);
                        var buttons = new[]
                        {
                            InlineKeyboardButton.WithCallbackData($"New", $"upload|{id}|{filename}"),
                            InlineKeyboardButton.WithCallbackData($"Old", $"upload|{id}|current")
                        };
                        msg.Reply("Which file do you want to keep?", new InlineKeyboardMarkup(buttons));
                    }
                    else msg.Reply("Fatal errors present, cannot upload!");
                }
            }
            catch (Exception e)
            {
                Bot.Send(msg.Chat.Id, e.Message, parseMode: ParseMode.Default);
            }
        }

        [Command(Trigger = "validatelangs", DevOnly = true)]
        public static void ValidateLangs(Message msg, string[] args)
        {
            msg.Reply("Which language file do you want to validate?", Handler.GetValidateLangsMenu());
        }

        [Command(Trigger = "rules")]
        public static void Rules(Message msg, string[] args)
        {
            try
            {
                Bot.Send(msg.From.Id, GetTranslation("Rules", GetLanguage(msg.From.Id)));
            }
            catch
            {
                msg.Reply(GetTranslation("NotStartedBot", GetLanguage(msg.From.Id)), GenerateStartMe(msg.From.Id));
                return;
            }
            if (msg.Chat.Type != ChatType.Private)
            {
                msg.Reply(GetTranslation("SentPM", GetLanguage(msg.From.Id)));
                return;
            }
        }

        [Command(Trigger = "donate")]
        public static void Donate(Message msg, string[] args)
        {
            if (msg.Chat.Type != ChatType.Private)
            {
                msg.Reply(GetTranslation("DonatePrivateOnly", GetLanguage(msg.From.Id)));
                return;
            }
            var argList = msg.Text.Split(' ');
            int money = 0;
            if (argList.Count() <= 1)
            {
                msg.Reply(GetTranslation("DonateInputValue", GetLanguage(msg.From.Id)));
                return;
            }
            else
            {
                if (!int.TryParse(argList[1], out money))
                {
                    msg.Reply(GetTranslation("DonateInputValue", GetLanguage(msg.From.Id)));
                    return;
                }
                else
                {
                    if (money < 10 || money > 100000)
                    {
                        msg.Reply(GetTranslation("DonateWrongValue", GetLanguage(msg.From.Id)));
                        return;
                    }
                    else
                    {
                        var providerToken = Constants.DonationLiveToken;
                        var title = GetTranslation("DonateTitle", GetLanguage(msg.From.Id));
                        var description = GetTranslation("DonateDescription", GetLanguage(msg.From.Id), money);
                        var payload = Constants.DonationPayload + msg.From.Id.ToString();
                        var startParameter = "donate";
                        var currency = "HKD";
                        var prices = new LabeledPrice[1] {
                            new LabeledPrice {
                                Label = GetTranslation("Donation", GetLanguage(msg.From.Id)),
                                Amount = money * 100
                            } };
                        Bot.Api.SendInvoiceAsync(msg.From.Id, title, description, payload, providerToken,
                            startParameter, currency, prices);
                    }
                }
            }

        }

        [Command(Trigger = "runinfo")]
        public static void RunInfo(Message msg, string[] args)
        {
            string uptime = $"{(DateTime.Now - Program.Startup):dd\\.hh\\:mm\\:ss\\.ff}";
            int gamecount = Bot.Gm.Games.Count;
            int playercount = Bot.Gm.Games.Select(x => x.Players.Count).Sum();
            Bot.Send(msg.Chat.Id, GetTranslation("Runinfo", GetLanguage(msg.Chat.Id), uptime, gamecount, playercount));
        }

        [Command(Trigger = "stats")]
        public static void Stats(Message msg, string[] args)
        {
            using (var db = new CrimDanceDb())
            {
                var isGroup = !(msg.Chat.Type == ChatType.Private);
                var player = msg.ReplyToMessage?.From ?? msg.From;
                var playerId = player.Id;
                var temp = db.Players.FirstOrDefault(x => x.TelegramId == playerId).Achievements ?? 0;
                var achv = (Achievements)temp;
                var achvCount = achv.GetUniqueFlags().Count();
                if (!db.GamePlayers.Any(x => x.Player.TelegramId == playerId))
                {
                    msg.Reply(GetTranslation("StatsHaveNotPlayed", GetLanguage(playerId)));
                    return;
                }
                var playerName = $"{player.GetName()} (<code>{playerId}</code>)";
                int numOfWins = db.GetNumOfWins(playerId).First().Value;
                var numOfGames = db.GetPlayerNumOfGames(playerId).First().Value;
                var numOfCrimWins = db.getCrimWinTimes(playerId).First().Value;
                var numOfDogWins = db.getDogWinTimes(playerId).First().Value;
                var send = GetTranslation("StatsDetails", GetLanguage(isGroup == true ? msg.Chat.Id : playerId),
                    playerName,
                    achvCount.ToBold(),
                    numOfGames.ToBold(),
                    $"{numOfWins} ({Math.Round((double)numOfWins * 100 / numOfGames, 0)}%)".ToBold(),
                    $"{numOfGames - numOfWins} ({Math.Round((double)(numOfGames - numOfWins) * 100 / numOfGames, 0)}%)".ToBold(),
                    numOfCrimWins.ToBold(),
                    numOfDogWins.ToBold()
                    );
                msg.Reply(send);
            }
        }

        [Command(Trigger = "achievements")]
        public static void Achievements(Message msg, string[] args)
        {
            using (var db = new CrimDanceDb())
            {
                var p = db.Players.FirstOrDefault(x => x.TelegramId == msg.From.Id);
                var temp = p.Achievements ?? 0;
                var achv = (Achievements)temp;
                var lang = GetLanguage(msg.From.Id);
                var achvList = achv.GetUniqueFlags().ToList();
                var msg1 = $"{GetTranslation("AchievementsGot", lang, achvList.Count)}\n\n";
                msg1 = achvList.Aggregate(msg1, (current, a) => current + $"{a.GetAchvName(lang).ToBold()}\n{a.GetAchvDescription(lang)}\n\n");
                var noAchvList = achv.GetUniqueFlags(true).ToList();
                var msg2 = $"{GetTranslation("AchievementsLack", lang, noAchvList.Count)}\n\n";
                msg2 = noAchvList.Aggregate(msg2, (current, a) => current + $"{a.GetAchvName(lang).ToBold()}\n{a.GetAchvDescription(lang)}\n\n");
                msg.ReplyPM(new[] { msg1, msg2 });
            }
        }
    }
}
