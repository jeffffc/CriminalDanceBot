using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace CriminalDanceBot.Handlers
{
    class CallbackQueryHandler
    {
        public static void HandleQuery(CallbackQuery query)
        {
            if (query.Data != null)
            {
                string[] args = query.Data.Split('|');
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
    }
}
