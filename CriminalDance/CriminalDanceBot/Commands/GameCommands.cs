using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Command = CriminalDanceBot.Attributes.Command;
using Telegram.Bot.Types;
using Database;

namespace CriminalDanceBot
{
    public partial class Commands
    {
        [Command(Trigger = "startgame")]
        public static void StartGame(Message msg, string[] args)
        {
            CriminalDance game = Bot.Gm.GetGameByChatId(msg.Chat.Id);
            if (game == null)
            {
                if (Program.MaintMode)
                {
                    Bot.Send(msg.Chat.Id, GetTranslation("CantStartGameMaintenance", GetLanguage(msg.Chat.Id)));
                    return;
                }


                Bot.Gm.AddGame(new CriminalDance(msg.Chat.Id, msg.From, msg.Chat.Title));
            }
            else
            {
                Bot.Gm.HandleMessage(msg);
                // msg.Reply(GetTranslation("ExistingGame", GetLanguage(msg.Chat.Id)));
            }
        }

        [Command(Trigger = "test")]
        public static void Testing(Message msg, string[] args)
        {
            CriminalDance game = Bot.Gm.GetGameByChatId(msg.Chat.Id);
            if (game == null)
            {
                return;
            }
            else
            {
                Bot.Gm.HandleMessage(msg);
            }
        }

        [Command(Trigger = "join")]
        public static void JoinGame(Message msg, string[] args)
        {
            CriminalDance game = Bot.Gm.GetGameByChatId(msg.Chat.Id);
            if (game == null)
            {
                return;
            }
            else
            {
                Bot.Gm.HandleMessage(msg);
            }
        }

        [Command(Trigger = "flee")]
        public static void FleeGame(Message msg, string[] args)
        {
            CriminalDance game = Bot.Gm.GetGameByChatId(msg.Chat.Id);
            if (game == null)
            {
                return;
            }
            else
            {
                Bot.Gm.HandleMessage(msg);
            }
        }

        [Command(Trigger = "forcestart")]
        public static void ForceStart(Message msg, string[] args)
        {
            CriminalDance game = Bot.Gm.GetGameByChatId(msg.Chat.Id);
            if (game == null)
            {
                return;
            }
            else
            {
                Bot.Gm.HandleMessage(msg);
            }
        }

        [Command(Trigger = "killgame", DevOnly = true)]
        public static void KillGame(Message msg, string[] args)
        {
            CriminalDance game = Bot.Gm.GetGameByChatId(msg.Chat.Id);
            if (game == null)
            {
                return;
            }
            else
            {
                Bot.Gm.HandleMessage(msg);
            }
        }

        [Command(Trigger = "seq")]
        public static void GetSequence(Message msg, string[] args)
        {
            CriminalDance game = Bot.Gm.GetGameByChatId(msg.Chat.Id);
            if (game == null)
            {
                msg.Reply(GetTranslation("NoGame", GetLanguage(msg.Chat.Id)));
            }
            else
            {
                Bot.Gm.HandleMessage(msg);
            }
        }
        
    }
}
