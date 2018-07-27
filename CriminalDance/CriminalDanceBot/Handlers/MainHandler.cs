using Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;

namespace CriminalDanceBot.Handlers
{
    partial class Handler
    {
        public static void HandleUpdates(ITelegramBotClient Bot)
        {
            Bot.OnCallbackQuery += BotOnCallbackQueryReceived;
            Bot.OnMessage += BotOnMessageReceived;
            Bot.OnMessageEdited += BotOnMessageReceived;
            Bot.OnInlineQuery += BotOnInlineQueryReceived;
            Bot.OnInlineResultChosen += BotOnChosenInlineResultReceived;
            Bot.OnUpdate += BotOnUpdateReceived;
            Bot.OnReceiveError += BotOnReceiveError;
        }


        private static void BotOnUpdateReceived(object sender, UpdateEventArgs updateEventArgs)
        {
            // answer precheckout for donation
            if (updateEventArgs.Update.PreCheckoutQuery != null)
            {
                var pcq = updateEventArgs.Update.PreCheckoutQuery;
                if (pcq.InvoicePayload != (Constants.DonationPayload + pcq.From.Id.ToString()))
                    Bot.Api.AnswerPreCheckoutQueryAsync(pcq.Id, GetTranslation("DonateError", GetLanguage(pcq.From.Id)));
                else
                    Bot.Api.AnswerPreCheckoutQueryAsync(pcq.Id);
            }
        }

        private static void BotOnChosenInlineResultReceived(object sender, ChosenInlineResultEventArgs chosenInlineResultEventArgs)
        {

        }

        private static void BotOnInlineQueryReceived(object sender, InlineQueryEventArgs inlineQueryEventArgs)
        {

        }

        private static void BotOnMessageReceived(object sender, MessageEventArgs e)
        {
            new Task(() => { Handler.HandleMessage(e.Message); }).Start();
        }

        private static void BotOnCallbackQueryReceived(object sender, CallbackQueryEventArgs e)
        {
            new Task(() => { Handler.HandleQuery(e.CallbackQuery); }).Start();
        }

        private static void BotOnReceiveError(object sender, ReceiveErrorEventArgs receiveErrorEventArgs)
        {
            Helper.LogError(receiveErrorEventArgs.ApiRequestException);
        }

        private static string GetTranslation(string key, string language, params object[] args)
        {
            return Program.Translations.GetTranslation(key, language, args);
        }

        public static string GetLanguage(int id)
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

        public static void SetLanguage(long chatId, string lang)
        {
            using (var db = new CrimDanceDb())
            {
                var grp = db.Groups.FirstOrDefault(x => x.GroupId == chatId);
                if (grp == null)
                    return;
                grp.Language = lang;
                db.SaveChanges();
            }
        }

        public static void SetLanguage(int userId, string lang)
        {
            using (var db = new CrimDanceDb())
            {
                var user = db.Players.FirstOrDefault(x => x.TelegramId == userId);
                if (user == null)
                    return;
                user.Language = lang;
                db.SaveChanges();
            }
        }
    }
}
