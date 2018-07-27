using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Database;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Telegram.Bot.Types.InputFiles;

namespace CriminalDanceBot.Handlers
{
    partial class Handler
    {
        public static void HandleQuery(CallbackQuery query)
        {
            if (query.Data != null)
            {
                string[] args = query.Data.Split('|');

                //dev only buttons
                if (new[] { "upload", "update", "validate" }.Contains(args[0]))
                {
                    //global admin only commands
                    if (!Constants.Dev.Contains(query.From.Id) /* && !Helpers.IsGlobalAdmin(query.From.Id) */)
                    {
                        Bot.Api.AnswerCallbackQueryAsync(query.Id, "Dev only!!", true);
                        return;
                    }
                }

                //config
                switch (args[0])
                {
                    case "config":
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
                        else if (args[1] == "back")
                        {
                            Bot.Edit(query.Message.Chat.Id, query.Message.MessageId, GetTranslation("WhatToDo", GetLanguage(query.From.Id)), GetConfigMenu(long.Parse(args[2])));
                        }
                        return;
                    case "setlang":
                        if (args.Length > 3)
                        {
                            var chatId = int.Parse(args[2]);
                            var chosenLang = args[3];
                            SetLanguage(chatId, chosenLang);
                            var toSend = GetTranslation("ReceivedButton", GetLanguage(query.From.Id));
                            Bot.Edit(query.Message.Chat.Id, query.Message.MessageId, toSend);
                            return;
                        }
                        break;
                    case "getlang":
                        if (args[1] == "get")
                        {
                            try
                            {
                                var lang = args[2] ?? "";
                                if (lang == "") throw new Exception("No language to download was specified");
                                Bot.Edit(query.Message.Chat.Id, query.Message.MessageId, GetTranslation("OneMoment", GetLanguage(query.From.Id)));
                                lang += ".xml";
                                using (var sr = new StreamReader(Path.Combine(Constants.GetLangDirectory(), lang)))
                                {
                                    var file = new InputOnlineFile(sr.BaseStream, lang);
                                    BotMethods.SendDocument(query.Message.Chat.Id, file);
                                }
                            }
                            catch (Exception e)
                            {
                                e.LogError();
                                Bot.Edit(query.Message.Chat.Id, query.Message.MessageId, "An error occured.");
                            }
                        }
                        else if (args[1] == "cancel")
                        {
                            Bot.Edit(query.Message.Chat.Id, query.Message.MessageId, GetTranslation("ConfigDone", GetLanguage(query.From.Id)));
                        }
                        return;
                    case "upload":
                        if (args[2] == "current")
                        {
                            Bot.Edit(query.Message.Chat.Id, query.Message.MessageId, "No action taken.");
                            return;
                        }
                        Bot.Edit(query.Message.Chat.Id, query.Message.MessageId, Program.Translations.UploadLanguage(args[2]));
                        return;
                    case "update":
                        if (args[1] == "yes")
                        {
                            Process.Start(Path.Combine(@"C:\CrimDance\", "Updater.exe"), query.Message.Chat.Id.ToString());
                            Program.MaintMode = true;
                            new Thread(Commands.CheckCurrentGames).Start();
                            Bot.Api.EditMessageTextAsync(query.Message.Chat.Id, query.Message.MessageId, "Ok. I will restart the bot now.");
                        }
                        else
                            Bot.Api.EditMessageTextAsync(query.Message.Chat.Id, query.Message.MessageId, "Ok. I will do nothing now.");
                        return;
                    case "validate":
                        if (args[1] == "cancel")
                        {
                            Bot.Edit(query.Message.Chat.Id, query.Message.MessageId, "Cancelled.");
                            return;
                        }
                        Bot.Edit(query.Message.Chat.Id, query.Message.MessageId, Program.Translations.ValidateLanguage(args[2]));
                        return;
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
            buttons.Add(InlineKeyboardButton.WithCallbackData("Change Language", $"config|lang|{id}"));
            buttons.Add(InlineKeyboardButton.WithCallbackData("Done", $"config|done"));
            var twoMenu = new List<InlineKeyboardButton[]>();
            for (var i = 0; i < buttons.Count; i++)
            {
                twoMenu.Add(new[] { buttons[i] });
            }

            var menu = new InlineKeyboardMarkup(twoMenu.ToArray());
            return menu;
        }

        internal static InlineKeyboardMarkup GetConfigLangMenu(long id, bool setlang = false)
        {
            List<InlineKeyboardButton> buttons = new List<InlineKeyboardButton>();
            //base menu
            foreach (string lang in Program.Translations.GetLanguageVariants().Select(x => x.FileName))
                buttons.Add(InlineKeyboardButton.WithCallbackData(lang, !setlang ? $"config|lang|{id}|{lang}" : $"setlang|lang|{id}|{lang}"));
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
            if (!setlang)
                twoMenu.Add(new[] { InlineKeyboardButton.WithCallbackData("Back", $"config|back|{id}") });

            var menu = new InlineKeyboardMarkup(twoMenu.ToArray());
            return menu;
        }

        internal static InlineKeyboardMarkup GetGetLangMenu()
        {
            List<InlineKeyboardButton> buttons = new List<InlineKeyboardButton>();
            //base menu
            foreach (string lang in Program.Translations.GetLanguageVariants().Select(x => x.FileName))
                buttons.Add(InlineKeyboardButton.WithCallbackData(lang, $"getlang|get|{lang}"));
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
            twoMenu.Add(new[] { InlineKeyboardButton.WithCallbackData("Cancel", $"getlang|cancel") });

            var menu = new InlineKeyboardMarkup(twoMenu.ToArray());
            return menu;
        }

        internal static InlineKeyboardMarkup GetValidateLangsMenu()
        {
            List<InlineKeyboardButton> buttons = new List<InlineKeyboardButton>();
            //base menu
            foreach (string lang in Program.Translations.GetLanguageVariants().Select(x => x.FileName))
                buttons.Add(InlineKeyboardButton.WithCallbackData(lang, $"validate|val|{lang}"));
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
            twoMenu.Add(new[] { InlineKeyboardButton.WithCallbackData("Cancel", $"validate|cancel") });

            var menu = new InlineKeyboardMarkup(twoMenu.ToArray());
            return menu;
        }
    }
}
