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
        public int TelegramUserId { get; set; }
        public string Name { get; set; }
        public string Username { get; set; }
        public int Id { get; set; }
        public bool? Won { get; set; }
        public string CardsInHand { get; set; } = null;
        public XCard TempCard { get; set; } = null;
        public bool CardChanged { get; set; } = false;
        public List<XCard> Cards { get; set; } = new List<XCard>();
        public List<XCard> UsedCards { get; set; } = new List<XCard>();
        public List<string> ToBeSent { get; set; } = new List<string>();

        public string CardChoice1 { get; set; } = null;
        public string CardChoice2 { get; set; } = null;
        public int? PlayerChoice1 { get; set; } = 0;
        public int? PlayerChoice2 { get; set; } = 0;

        public QuestionAsked CurrentQuestion { get; set; }
        public bool ReAnswer { get; set; } = false;

        public bool Accomplice { get; set; } = false;
        public bool UsedUp { get; set; } = false;
    }

    public class QuestionAsked
    {
        public QuestionType Type { get; set; }
        public int MessageId { get; set; } = 0;
    }

    public enum QuestionType
    {
        Card, Player
    }
}
