using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using CriminalDanceBot;
using Database;

namespace CriminalDanceBot.Handlers
{
    class MessageHandler
    {
        public static void HandleMessage(Message msg)
        {
            switch (msg.Type)
            {
                case MessageType.TextMessage:
                    string text = msg.Text;
                    string[] args = text.Contains(' ')
                                    ? new[] { text.Split(' ')[0].ToLower(), text.Remove(0, text.IndexOf(' ') + 1) }
                                    : new[] { text.ToLower(), null };
                    if (msg.Text.StartsWith("/"))
                    {
                        args[0] = args[0].Substring(1);
                        var cmd = Bot.Commands.FirstOrDefault(x => x.Trigger == args[0]);
                        if (cmd != null)
                        {
                            if (new[] { ChatType.Supergroup, ChatType.Group }.Contains(msg.Chat.Type))
                            {
                                using (var db = new CrimDanceDb())
                                {
                                    var DbGroup = db.Groups.FirstOrDefault(x => x.GroupId == msg.Chat.Id);
                                    if (DbGroup == null)
                                    {
                                        DbGroup = MakeDefaultGroup(msg.Chat);
                                        db.Groups.Add(DbGroup);
                                        db.SaveChanges();
                                    }
                                }
                            }
                            if (cmd.GroupOnly && !new[] { ChatType.Supergroup, ChatType.Group }.Contains(msg.Chat.Type))
                            {
                                msg.Reply("This command can only be used in groups!");
                                return;
                            }

                            if (cmd.AdminOnly)
                            {
                                msg.Reply("You aren't a group admin!");
                                return;
                            }

                            if (cmd.DevOnly && !Constants.Dev.Contains(msg.From.Id))
                            {
                                msg.Reply("You aren't a bot dev!");
                                return;
                            }

                            cmd.Method.Invoke(msg, args);
                            return;
                        }
                    }
                    if (msg.Chat.Type == ChatType.Supergroup && msg.Text.StartsWith("/startgame"))
                    {
                        // nothing
                    }
                    else if (msg.Text.StartsWith("/chatid"))
                    {
                        msg.Reply($"{msg.Chat.Id}");
                    }
                    else if (msg.Text.StartsWith("/join"))
                    {
                        var g = Bot.Gm.GetGameByChatId(msg.Chat.Id);
                    }
                    break;
                case MessageType.ServiceMessage:
                    //
                    break;
            }
        }

        internal static Group MakeDefaultGroup(Chat chat)
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
    }
}
