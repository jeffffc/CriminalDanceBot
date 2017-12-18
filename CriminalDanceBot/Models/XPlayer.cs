using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace CriminalDanceBot.Models
{
    public class XPlayer
    {
        public User TelegramUser { get; set; }
        public string Name { get; set; }
        public int Id { get; set; }
        public string CardsInHand { get; set; } = null;
        public XCard TempCard { get; set; } = null;
        public bool CardChanged { get; set; } = false;
    }
}
