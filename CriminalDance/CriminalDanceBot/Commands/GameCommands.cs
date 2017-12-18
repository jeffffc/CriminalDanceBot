using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Command = CriminalDanceBot.Attributes.Command;
using Telegram.Bot.Types;

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
                Bot.Gm.AddGame(new CriminalDance(msg.Chat.Id, msg.From, msg.Chat.Title));
            }
            else
            {
                msg.Reply("A game has already been started before.");
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
    }
}
