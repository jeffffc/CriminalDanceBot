using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Database;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InlineKeyboardButtons;
using Telegram.Bot.Types.ReplyMarkups;

namespace CriminalDanceBot.Handlers
{
    partial class Handler
    {
        public static void HandleQuery(CallbackQuery query)
        {
            if (query.Data != null)
            {
                string[] args = query.Data.Split('|');
                //config
                if (args[0] == "config")
                {
                    if (args[1] == "lang")
                    {
                        if (args.Length == 3)
                        {
                            var menu = Handler.GetConfigLangMenu(long.Parse(args[2]));
                            Bot.Edit(query.Message.Chat.Id, query.Message.MessageId, GetTranslation("ChooseLanguage", GetLanguage(query.From.Id)), menu);
                        }
                        if (args.Length > 3)
                        {
                            var chatId = long.Parse(args[2]);
                            var chosenLang = args[3];
                            SetLanguage(chatId, chosenLang);
                            var menu = GetConfigMenu(chatId);
                            var toSend = GetTranslation("ReceivedButton", GetLanguage(query.From.Id)) + Environment.NewLine + GetTranslation("WhatToDo", GetLanguage(query.From.Id));
                            Bot.Edit(query.Message.Chat.Id, query.Message.MessageId, toSend, menu);
                        }
                    }
                    else if (args[1] == "done")
                    {
                        Bot.Edit(query.Message.Chat.Id, query.Message.MessageId, GetTranslation("ConfigDone", GetLanguage(query.From.Id)));
                    }
                }
                //game
                var gameId = args[0];
                try
                {
                    if (Bot.Gm.GetGameByGuid(Guid.Parse(gameId)) != null)
                    {
                        Bot.Gm.HandleQuery(query, args);
                    }
                }
                catch
                {
                    //
                }
            }
            else
            {
                //
            }
        }

        internal static InlineKeyboardMarkup GetConfigMenu(long id)
        {
            List<InlineKeyboardButton> buttons = new List<InlineKeyboardButton>();
            //base menu
            buttons.Add(new InlineKeyboardCallbackButton("Change Language", $"config|lang|{id}"));
            buttons.Add(new InlineKeyboardCallbackButton("Done", $"config|done"));
            var twoMenu = new List<InlineKeyboardButton[]>();
            for (var i = 0; i < buttons.Count; i++)
            {
                twoMenu.Add(new[] { buttons[i] });
            }

            var menu = new InlineKeyboardMarkup(twoMenu.ToArray());
            return menu;
        }

        internal static InlineKeyboardMarkup GetConfigLangMenu(long id)
        {
            List<InlineKeyboardButton> buttons = new List<InlineKeyboardButton>();
            //base menu
            foreach (string lang in Program.Langs.Keys)
                buttons.Add(new InlineKeyboardCallbackButton(lang, $"config|lang|{id}|{lang}"));
            var twoMenu = new List<InlineKeyboardButton[]>();
            for (var i = 0; i < buttons.Count; i++)
            {
                if (buttons.Count - 1 == i)
                {
                    twoMenu.Add(new[] { buttons[i] });
                }
                else
                    twoMenu.Add(new[] { buttons[i], buttons[i + 1] });
                i++;
            }
            twoMenu.Add(new[] { new InlineKeyboardCallbackButton("Back", $"config|back") });

            var menu = new InlineKeyboardMarkup(twoMenu.ToArray());
            return menu;
        }
    }
}
