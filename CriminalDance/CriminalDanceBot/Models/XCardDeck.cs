using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Specialized;

namespace CriminalDanceBot.Models
{
    public class XCardDeck
    {
        public List<XCard> Cards { get; set; } = new List<XCard>();

        public XCardDeck(int NumOfPlayers)
        {
            Cards.Add(new XCard(XCardType.FirstFinder));
            Cards.Add(new XCard(XCardType.Culprit));

            int _toAdd = 0;

            var FullDeck = new Dictionary<XCardType, int>();
            foreach (XCardType c in Enum.GetValues(typeof(XCardType)))
            {
                var num = 1;
                switch (c)
                {
                    case XCardType.Accomplice:
                    case XCardType.Bystander:
                        num = 2;
                        break;
                    case XCardType.Witness:
                        num = 3;
                        break;
                    case XCardType.InformationExchange:
                    case XCardType.Barter:
                    case XCardType.Detective:
                        num = 4;
                        break;
                    case XCardType.Rumor:
                    case XCardType.Alibi:
                        num = 5;
                        break;
                    case XCardType.FirstFinder:
                    case XCardType.Dog:
                        continue;
                    default:
                        num = 1;
                        break;
                }
                FullDeck.Add(c, num);
            }
            switch (NumOfPlayers)
            {
                case 3:
                    Cards.Add(new XCard(XCardType.Detective));
                    FullDeck[XCardType.Detective] -= 1;
                    Cards.Add(new XCard(XCardType.Alibi));
                    FullDeck[XCardType.Alibi] -= 1;
                    _toAdd = 8;
                    break;
                case 4:
                    Cards.Add(new XCard(XCardType.Detective));
                    FullDeck[XCardType.Detective] -= 1;
                    Cards.Add(new XCard(XCardType.Alibi));
                    FullDeck[XCardType.Alibi] -= 1;
                    Cards.Add(new XCard(XCardType.Accomplice));
                    FullDeck[XCardType.Accomplice] -= 1;
                    _toAdd = 11;
                    break;
                case 5:
                    Cards.Add(new XCard(XCardType.Detective));
                    FullDeck[XCardType.Detective] -= 1;
                    Cards.AddRange(Enumerable.Repeat(new XCard(XCardType.Alibi), 2).ToList());
                    FullDeck[XCardType.Alibi] -= 2;
                    Cards.Add(new XCard(XCardType.Accomplice));
                    FullDeck[XCardType.Accomplice] -= 1;
                    _toAdd = 14;
                    break;
                case 6:
                    Cards.AddRange(Enumerable.Repeat(new XCard(XCardType.Detective), 2).ToList());
                    FullDeck[XCardType.Detective] -= 2;
                    Cards.AddRange(Enumerable.Repeat(new XCard(XCardType.Alibi), 2).ToList());
                    FullDeck[XCardType.Alibi] -= 2;
                    Cards.AddRange(Enumerable.Repeat(new XCard(XCardType.Accomplice), 2).ToList());
                    FullDeck[XCardType.Accomplice] -= 2;
                    _toAdd = 16;
                    break;
                case 7:
                    Cards.AddRange(Enumerable.Repeat(new XCard(XCardType.Detective), 2).ToList());
                    FullDeck[XCardType.Detective] -= 2;
                    Cards.AddRange(Enumerable.Repeat(new XCard(XCardType.Alibi), 3).ToList());
                    FullDeck[XCardType.Alibi] -= 3;
                    Cards.AddRange(Enumerable.Repeat(new XCard(XCardType.Accomplice), 2).ToList());
                    FullDeck[XCardType.Accomplice] -= 2;
                    _toAdd = 19;
                    break;
                default:
                    _toAdd = 30;
                    break;
            }
            List<XCard> tempList = new List<XCard> { new XCard(XCardType.Dog) };
            foreach (KeyValuePair<XCardType, int> x in FullDeck)
            {
                switch (x.Key)
                {
                    case XCardType.Accomplice:
                        if (x.Value > 0)
                            tempList.AddRange(Enumerable.Repeat(new XCard(x.Key), x.Value).ToList());
                        break;
                    default:
                        tempList.AddRange(Enumerable.Repeat(new XCard(x.Key), x.Value).ToList());
                        break;
                }
            }
            tempList.Shuffle();
            tempList.Shuffle();
            Cards.AddRange(tempList.GetRange(0, _toAdd));
            Cards.Shuffle();
            Cards.Shuffle();
        }

    }
}
