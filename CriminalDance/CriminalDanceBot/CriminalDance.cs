using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using CriminalDanceBot.Models;
using System.Threading;
using Database;
using System.Diagnostics;
using System.IO;
using Telegram.Bot.Types.InlineKeyboardButtons;
using System.Xml.Linq;

namespace CriminalDanceBot
{
    public class CriminalDance : IDisposable
    {
        public long ChatId;
        public string GroupName;
        public int GameId;
        public Group DbGroup;
        public List<XPlayer> Players = new List<XPlayer>();
        public Queue<XPlayer> PlayerQueue = new Queue<XPlayer>();
        public XPlayer Initiator;
        public Guid Id = Guid.NewGuid();
        public XCardDeck Cards;
        public int JoinTime = Constants.JoinTime;
        public GamePhase Phase = GamePhase.Joining;
        private int _secondsToAdd = 0;
        public GameAction NowAction = GameAction.FirstFinder;
        private int _playerList = 0;

        public Database.Game DbGame;

        public Locale Locale;
        public string Language = "English";

        public XPlayer Winner;
        public XPlayer Culprit;
        public XCardType WinnerType;

        public CriminalDance(long chatId, User u, string groupName)
        {
            #region Creating New Game - Preparation
            using (var db = new CrimDanceDb())
            {
                ChatId = chatId;
                GroupName = groupName;
                DbGroup = db.Groups.FirstOrDefault(x => x.GroupId == ChatId);
                LoadLanguage(DbGroup.Language);
                if (DbGroup == null)
                    Bot.Gm.RemoveGame(this);
            }
            // something
            #endregion

            var msg = GetTranslation("NewGame", GetName(u));
            // beta message
            msg += Environment.NewLine + Environment.NewLine + GetTranslation("Beta");
            Bot.Send(chatId, msg);
            AddPlayer(u, true);
            Initiator = Players[0];
            new Task(() => { NotifyNextGamePlayers(); }).Start();
            new Thread(GameTimer).Start();
        }

        #region Main methods

        private void GameTimer()
        {
            while (Phase != GamePhase.Ending)
            {
                for (var i = 0; i < JoinTime; i++)
                {
                    if (this.Phase == GamePhase.InGame)
                        break;
                    if (this.Phase == GamePhase.Ending)
                        return;
                    //try to remove duplicated game
                    if (i == 10)
                    {
                        var count = Bot.Gm.Games.Count(x => x.ChatId == ChatId);
                        if (count > 1)
                        {
                            var toDel = Bot.Gm.Games.FirstOrDefault(x => x.Id != this.Id);
                            Bot.Send(toDel.ChatId, GetTranslation("DuplicatedGameRemoving"));
                            toDel.Phase = GamePhase.Ending;
                            Bot.Gm.RemoveGame(toDel);
                        }
                    }
                    if (_secondsToAdd != 0)
                    {
                        i = Math.Max(i - _secondsToAdd, Constants.JoinTime - Constants.JoinTimeMax);
                        // Bot.Send(ChatId, GetTranslation("JoinTimeLeft", TimeSpan.FromSeconds(Constants.JoinTime - i).ToString(@"mm\:ss")));
                        _secondsToAdd = 0;
                    }
                    var specialTime = JoinTime - i;
                    if (new int[] { 10, 30, 60, 90 }.Contains(specialTime))
                    {
                        Bot.Send(ChatId, GetTranslation("JoinTimeSpecialSeconds", specialTime));
                    }
                    if (Players.Count == 8)
                        break;
                    Thread.Sleep(1000);
                }

                do
                {
                    XPlayer p = Players.FirstOrDefault(x => Players.Count(y => y.TelegramUserId == x.TelegramUserId) > 1);
                    if (p == null) break;
                    Players.Remove(p);
                }
                while (true);

                if (this.Players.Count() >= 3)
                    this.Phase = GamePhase.InGame;
                if (this.Phase != GamePhase.InGame)
                {
                    /*
                    this.Phase = GamePhase.Ending;
                    Bot.Gm.RemoveGame(this);
                    Bot.Send(ChatId, "Game ended!");
                    */
                }
                else
                {
                    #region Ready to start game
                    if (Players.Count < 3)
                    {
                        Send(GetTranslation("GameEnded"));
                        return;
                    }

                    Bot.Send(ChatId, GetTranslation("GameStart"));

                    // create game + gameplayers in db
                    using (var db = new CrimDanceDb())
                    {
                        DbGame = new Database.Game
                        {
                            GrpId = DbGroup.Id,
                            GroupId = ChatId,
                            GroupName = GroupName,
                            TimeStarted = DateTime.UtcNow
                        };
                        db.Games.Add(DbGame);
                        db.SaveChanges();
                        GameId = DbGame.Id;
                        foreach (var p in Players)
                        {
                            GamePlayer DbGamePlayer = new GamePlayer
                            {
                                PlayerId = db.Players.FirstOrDefault(x => x.TelegramId == p.TelegramUserId).Id,
                                GameId = GameId
                            };
                            db.GamePlayers.Add(DbGamePlayer);
                        }
                        db.SaveChanges();
                    }

                    PrepareGame(Players.Count());

                    // remove joined players from nextgame list
                    // RemoveFromNextGame(Players.Select(x => x.TelegramUserId).ToList());

#if DEBUG
                    string allCards = "";
                    foreach (XPlayer p in Players)
                    {
                        allCards += $"{p.Name} has got: {p.Cards.Select(i => i.Name).Aggregate((i, j) => i + ", " + j)}" + Environment.NewLine;
                    }
                    // Bot.Send(ChatId, allCards);
#endif
                    #endregion

                    #region Start!
                    FirstFinder();
                    foreach (var player in Players)
                    {
                        SendPM(player, GenerateOwnCard(player));
                    }
                    while (NowAction != GameAction.Ending)
                    {
                        _playerList = Send(GeneratePlayerList()).MessageId;
                        while (NowAction != GameAction.Next)
                        {
                            switch (NowAction)
                            {
                                case GameAction.NormalCard:
                                    NormalActions();
                                    break;
                                case GameAction.Barter:
                                    Barter();
                                    break;
                                case GameAction.Rumor:
                                    Rumor();
                                    break;
                                case GameAction.InfoExchange:
                                    InfoExchange();
                                    break;
                                case GameAction.Detective:
                                    Detective();
                                    break;
                                case GameAction.Dog:
                                    Dog();
                                    break;
                                case GameAction.Witness:
                                    Witness();
                                    break;
                                case GameAction.Ending:
                                    break;
                                default:
                                    break;
                            }
                            if (NowAction == GameAction.Ending || Phase == GamePhase.Ending)
                                break;
                        }
                        if (NowAction == GameAction.Ending || Phase == GamePhase.Ending)
                            break;
                        NextPlayer();
                    }
                    if (Phase == GamePhase.Ending)
                        break;
                    EndGame();
                    #endregion
                    this.Phase = GamePhase.Ending;
                }
                this.Phase = GamePhase.Ending;
            }

            Bot.Gm.RemoveGame(this);
            Bot.Send(ChatId, GetTranslation("GameEnded"));
        }

        public void FirstFinder()
        {
            try
            {
                bool succeed = false;
                while (!succeed)
                {
                    var p = PlayerQueue.First();
                    var card = p.Cards.FirstOrDefault(x => x.Type == XCardType.FirstFinder);
                    if (card != null)
                    {
                        succeed = true;
                        Send(GetTranslation("GotFirstFinder", GetName(p)));
                        UseCard(p, card);
                        NowAction = GameAction.NormalCard;
                    }
                    NextPlayer();
                }
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        public void UseCard(XPlayer p, XCard card, bool dump = false)
        {
            var c = p.Cards.FirstOrDefault(x => x == card);
            p.UsedCards.Add(c);
            p.Cards.Remove(c);
            if (!dump)
                Send(GetTranslation("GeneralUseCard", GetName(p), GetName(card)));
            if (p.Cards.Count == 0)
                p.UsedUp = true;
        }

        public void NormalActions()
        {
            try
            {
                var p = PlayerQueue.First();
                p.CardChoice1 = null;
                if (p.ReAnswer != true)
                    SendMenu(p, GetTranslation("ChooseCard"), GenerateMenu(p, p.Cards, GameAction.NormalCard), QuestionType.Card);
                for (int i = 0; i < Constants.ChooseCardTime; i++)
                {
                    Thread.Sleep(1000);
                    if (p.CurrentQuestion == null)
                        break;
                }
                try
                {
                    if (p.CurrentQuestion.MessageId != 0 && p.CardChoice1 == null)
                    {
                        SendTimesUp(p, p.CurrentQuestion.MessageId);
                    }
                }
                catch
                {
                    // ?
                }

                var cardchoice = p.CardChoice1;
                var afkCulprit = false;
                if (cardchoice == null)
                {
                    afkCulprit = DumpCard(p);
                    if (afkCulprit != true)
                        NowAction = GameAction.Next;
                    else
                    {
                        NowAction = GameAction.Ending;
                        Winner = p;
                        WinnerType = XCardType.Culprit;
                    }
                    return;
                }
                var card = p.Cards.FirstOrDefault(x => x.Id == cardchoice);
                p.CurrentQuestion = null;

                if (card == null)
                {
                    string m = "<b>Error occured!</b>" + Environment.NewLine;
                    m += "Card was null after player choice!" + Environment.NewLine;
                    m += $"Player: {p.GetName()} ({p.TelegramUserId})" + Environment.NewLine;
                    m += $"Cardchoice: {p.CardChoice1}" + Environment.NewLine;
                    m += $"Cards in hand: {GenerateOwnCard(p, true)}" + Environment.NewLine;
                    m += $"NowAction: {NowAction.ToString()}" + Environment.NewLine;
                    m += $"Group: {GroupName}" + Environment.NewLine;
                    m += $"Error time: {DateTime.UtcNow.ToLongTimeString()} UTC";
                    Bot.Send(Constants.LogGroupId, m);
                    Bot.Send(ChatId, "An error occured! Informed the developers! Trying to keep the game alive...");
                    NowAction = GameAction.Next;
                    return;
                }

                // What card?
                switch (card.Type)
                {
                    case XCardType.Accomplice:
                        if (!p.Accomplice)
                        {
                            p.Accomplice = true;
                            Send(GetTranslation("DeclareAccomplice", GetName(p)));
                            p.Name += " " + GetTranslation("AccompliceAppendName");
                        }
                        else
                        {
                            Send(GetTranslation("RepeatAccomplice", GetName(p)));
                        }
                        NowAction = GameAction.Next;
                        UseCard(p, card);
                        break;
                    case XCardType.Witness:
                        UseCard(p, card);
                        NowAction = GameAction.Witness;
                        break;
                    case XCardType.Alibi:
                    case XCardType.Bystander:
                        Send(GetTranslation("UselessCard"));
                        NowAction = GameAction.Next;
                        UseCard(p, card);
                        break;
                    case XCardType.Culprit:
                        if (p.Cards.Count == 1)
                        {
                            Winner = p;
                            WinnerType = XCardType.Culprit;
                            NowAction = GameAction.Ending;
                        }
                        else
                        {
                            p.ReAnswer = true;
                            SendMenu(p, GetTranslation("CulpritNonLastCard"), GenerateMenu(p, p.Cards, GameAction.NormalCard), QuestionType.Card);
                            NowAction = GameAction.NormalCard;
                        }
                        break;
                    case XCardType.Barter:
                        UseCard(p, card);
                        NowAction = GameAction.Barter;
                        break;
                    case XCardType.Detective:
                        UseCard(p, card);
                        NowAction = GameAction.Detective;
                        break;
                    case XCardType.Dog:
                        UseCard(p, card);
                        p.TempCard = card;
                        NowAction = GameAction.Dog;
                        break;
                    case XCardType.InfoExchange:
                        UseCard(p, card);
                        NowAction = GameAction.InfoExchange;
                        break;
                    case XCardType.Rumor:
                        UseCard(p, card);
                        NowAction = GameAction.Rumor;
                        break;
                }
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        public void Barter()
        {
            try
            {
                var p = PlayerQueue.First();
                if (p.UsedUp)
                {
                    Send(GetTranslation("BarterUsedUp", GetName(p)));
                    NowAction = GameAction.Next;
                    return;
                }
                XPlayer p2 = null;
                p.PlayerChoice1 = 0;
                SendMenu(p, GetTranslation("UseCardOn", GetName(XCardType.Barter)), GenerateMenu(p, Players.FindAll(x => x != p && !x.UsedUp), GameAction.Barter), QuestionType.Player);
                for (int i = 0; i < Constants.ChooseCardTime; i++)
                {
                    Thread.Sleep(1000);
                    if (p.CurrentQuestion == null)
                        break;
                }
                try
                {
                    if (p.CurrentQuestion.MessageId != 0 && p.PlayerChoice1 == 0)
                    {
                        SendTimesUp(p, p.CurrentQuestion.MessageId);
                    }
                }
                catch
                {
                    // ?
                }
                if (p.PlayerChoice1 == 0)
                {
                    p2 = Players.FindAll(x => x != p && !x.UsedUp)[Helper.RandomNum(Players.FindAll(x => x != p && !x.UsedUp).Count)];
                }
                else
                {
                    p2 = Players.FirstOrDefault(x => x.TelegramUserId == p.PlayerChoice1);
                }
                var BarterPlayers = new List<XPlayer> { p, p2 };
                foreach (XPlayer player in BarterPlayers)
                {
                    player.CardChoice1 = null;
                    SendMenu(player, GetTranslation("BarterChooseCard"), GenerateMenu(player, player.Cards, GameAction.Barter), QuestionType.Card);
                }
                for (int i = 0; i < Constants.ChooseCardTime; i++)
                {
                    Thread.Sleep(1000);
                    if (BarterPlayers.All(x => x.CurrentQuestion == null))
                        break;
                }
                try
                {
                    foreach (var player in BarterPlayers)
                    {
                        if (player.CurrentQuestion.MessageId != 0 && player.CardChoice1 == null)
                        {
                            SendTimesUp(player, player.CurrentQuestion.MessageId);
                        }
                    }
                }
                catch
                {
                    //
                }
                foreach (var player in BarterPlayers)
                {
                    if (player.CardChoice1 == null)
                    {
                        player.CardChoice1 = player.Cards[Helper.RandomNum(player.Cards.Count)].Id;
                    }
                }

                // switch the cards now
                var c1 = p.Cards.FirstOrDefault(x => x.Id == p.CardChoice1);
                var c2 = p2.Cards.FirstOrDefault(x => x.Id == p2.CardChoice1);
                p.Cards.Remove(c1);
                p2.Cards.Remove(c2);
                p.Cards.Add(c2);
                p2.Cards.Add(c1);
                p.CardChanged = true;
                p2.CardChanged = true;
                Send(GetTranslation("BarterExchangedCard", GetName(p), GetName(p2)));
                NowAction = GameAction.Next;
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        public void Witness()
        {
            try
            {
                var p = PlayerQueue.First();
                XPlayer p2 = null;
                p.PlayerChoice1 = 0;
                SendMenu(p, GetTranslation("UseCardOn", GetName(XCardType.Witness)), GenerateMenu(p, Players.FindAll(x => x != p && !x.UsedUp), GameAction.Witness), QuestionType.Player);
                for (int i = 0; i < Constants.ChooseCardTime; i++)
                {
                    Thread.Sleep(1000);
                    if (p.CurrentQuestion == null)
                        break;
                }
                try
                {
                    if (p.CurrentQuestion.MessageId != 0 && p.PlayerChoice1 == 0)
                    {
                        SendTimesUp(p, p.CurrentQuestion.MessageId);
                    }
                }
                catch
                {
                    // ?
                }
                if (p.PlayerChoice1 == 0)
                {
                    //times up
                    Send($"{GetName(p)} failed to make a choice in time, skipping.");
                    NowAction = GameAction.Next;
                    return;
                }
                else
                {
                    p2 = Players.FirstOrDefault(x => x.TelegramUserId == p.PlayerChoice1);
                }
                /*
                var toBeDeleted = SendPM(p, GenerateOwnCard(p2, true));
                Task.Factory.StartNew(() => {
                    Thread.Sleep(30000);
                    Bot.Api.DeleteMessageAsync(p.TelegramUserId, toBeDeleted.MessageId);
                    });
                */

                Send(GetTranslation("WitnessWatched", GetName(p), GetName(p2)));
                NowAction = GameAction.Next;
            }
            catch (Exception ex)
            {
                Log(ex);
            }

        }

        public void InfoExchange()
        {
            try
            {
                foreach (XPlayer player in Players.FindAll(x => !x.UsedUp))
                {
                    player.CardChoice1 = null;
                    SendMenu(player, GetTranslation("InfoExchangeChooseCard"), GenerateMenu(player, player.Cards, GameAction.InfoExchange), QuestionType.Card);
                }
                for (int i = 0; i < Constants.ChooseCardTime; i++)
                {
                    Thread.Sleep(1000);
                    if (Players.All(x => x.CurrentQuestion == null))
                        break;
                }
                try
                {
                    foreach (var player in Players.FindAll(x => !x.UsedUp))
                    {
                        if (player.CurrentQuestion.MessageId != 0 && player.CardChoice1 == null)
                        {
                            SendTimesUp(player, player.CurrentQuestion.MessageId);
                        }
                    }
                }
                catch
                {
                    //
                }
                foreach (var player in Players.FindAll(x => !x.UsedUp))
                {
                    if (player.CardChoice1 == null)
                    {
                        player.CardChoice1 = player.Cards[Helper.RandomNum(player.Cards.Count)].Id;
                    }
                }

                // switch the cards now
                var tempList = PlayerQueue.ToList().FindAll(x => !x.UsedUp);
                for (int i = 0; i < tempList.Count; i++)
                {
                    var p = tempList.ElementAt(i);
                    XPlayer next;
                    if (i < tempList.Count - 1)
                        next = tempList.ElementAt(i + 1);
                    else
                        next = tempList.First();
                    var card = p.Cards.FirstOrDefault(x => x.Id == p.CardChoice1);
                    p.Cards.Remove(card);
                    next.Cards.Add(card);
                    p.CardChanged = true;
                }
                Send(GetTranslation("InfoExchangeCompleted"));
                NowAction = GameAction.Next;
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        public void Rumor()
        {
            try
            {
                foreach (var player in Players.FindAll(x => !x.UsedUp))
                    player.CardChoice1 = player.Cards[Helper.RandomNum(player.Cards.Count)].Id;


                var tempList = PlayerQueue.ToList().FindAll(x => !x.UsedUp);
                foreach (var p in tempList)
                    p.ToBeSent.Clear();
                // switch the cards now
                for (int i = 0; i < tempList.Count; i++)
                {
                    var p = tempList.ElementAt(i);
                    XPlayer next;
                    if (i < tempList.Count - 1)
                        next = tempList.ElementAt(i + 1);
                    else
                        next = tempList.First();
                    var card = p.Cards.FirstOrDefault(x => x.Id == p.CardChoice1);
                    p.Cards.Remove(card);
                    next.Cards.Add(card);
                    p.ToBeSent.Add(GetTranslation("RumorGive", GetName(card), GetName(next)));
                    next.ToBeSent.Add(GetTranslation("RumorReceive", GetName(card), GetName(p)));
                    p.CardChanged = true;
                }
                foreach (var p in tempList)
                {
                    SendPM(p, p.ToBeSent.Aggregate((x, y) => x + Environment.NewLine + y));
                    p.ToBeSent.Clear();
                }
                Send(GetTranslation("RumorCompleted"));
                NowAction = GameAction.Next;
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        public void Detective()
        {
            {
                try
                {
                    var p = PlayerQueue.First();
                    XPlayer p2 = null;
                    p.PlayerChoice1 = 0;
                    SendMenu(p, GetTranslation("DetectiveGuessCulprit"), GenerateMenu(p, Players.FindAll(x => x != p && !x.UsedUp), GameAction.Detective), QuestionType.Player);
                    for (int i = 0; i < Constants.ChooseCardTime; i++)
                    {
                        Thread.Sleep(1000);
                        if (p.CurrentQuestion == null)
                            break;
                    }
                    try
                    {
                        if (p.CurrentQuestion.MessageId != 0 && p.PlayerChoice1 == 0)
                        {
                            SendTimesUp(p, p.CurrentQuestion.MessageId);
                        }
                    }
                    catch
                    {
                        // ?
                    }
                    if (p.PlayerChoice1 == 0)
                    {
                        Send(GetTranslation("DetectiveTimesUp", GetName(p)));
                        NowAction = GameAction.Next;
                        return;
                    }
                    else
                    {
                        p2 = Players.FirstOrDefault(x => x.TelegramUserId == p.PlayerChoice1);
                    }
                    if (p2 != null)
                    {
                        // check if p2 has culprit + if he has alibi
                        if (p2.Cards.FirstOrDefault(x => x.Type == XCardType.Culprit) == null)
                        {
                            // no culprit
                            Send(GetTranslation("DetectiveWrongGuess", GetName(p), GetName(p2)));
                        }
                        else
                        {
                            if (p2.Cards.FirstOrDefault(x => x.Type == XCardType.Alibi) == null)
                            {
                                // has culprit, no alibi ==> lose
                                Winner = p;
                                Culprit = p2;
                                WinnerType = XCardType.Detective;
                                NowAction = GameAction.Ending;
                                Send(GetTranslation("DetectiveCorrect", GetName(p), GetName(p2)));
                                return;
                            }
                            else
                            {
                                // is culprit but has alibi ==> nothing happens
                                Send(GetTranslation("DetectiveWrongGuess", GetName(p), GetName(p2)));
                            }
                        }
                    }
                    NowAction = GameAction.Next;
                }
                catch (Exception ex)
                {
                    Log(ex);
                }
            }
        }

        public void Dog()
        {
            try
            {
                var p = PlayerQueue.First();
                XPlayer p2 = null;
                p.PlayerChoice1 = 0;
                SendMenu(p, GetTranslation("UseCardOn", GetName(XCardType.Dog)), GenerateMenu(p, Players.FindAll(x => x != p && !x.UsedUp), GameAction.Dog), QuestionType.Player);
                for (int i = 0; i < Constants.ChooseCardTime; i++)
                {
                    Thread.Sleep(1000);
                    if (p.CurrentQuestion == null)
                        break;
                }
                try
                {
                    if (p.CurrentQuestion.MessageId != 0 && p.PlayerChoice1 == 0)
                    {
                        SendTimesUp(p, p.CurrentQuestion.MessageId);
                    }
                }
                catch
                {
                    // ?
                }
                if (p.PlayerChoice1 == 0)
                {
                    p2 = Players.FindAll(x => x != p && !x.UsedUp)[Helper.RandomNum(Players.FindAll(x => x != p && !x.UsedUp).Count - 1)];
                }
                else
                {
                    p2 = Players.FirstOrDefault(x => x.TelegramUserId == p.PlayerChoice1);
                }

                p2.CardChoice1 = null;
                SendMenu(p2, GetTranslation("DogThrowCard"), GenerateMenu(p2, p2.Cards, GameAction.Dog), QuestionType.Card);

                for (int i = 0; i < Constants.ChooseCardTime; i++)
                {
                    Thread.Sleep(1000);
                    if (p2.CardChoice1 != null)
                        break;
                }
                try
                {
                    if (p2.CurrentQuestion.MessageId != 0 && p2.CardChoice1 == null)
                    {
                        SendTimesUp(p2, p2.CurrentQuestion.MessageId);
                    }
                }
                catch
                {
                    //
                }

                if (p2.CardChoice1 == null)
                {
                    p2.CardChoice1 = p2.Cards[Helper.RandomNum(p2.Cards.Count)].Id;
                }


                var cardChosen = p2.Cards.FirstOrDefault(x => x.Id == p2.CardChoice1);
                if (cardChosen != null)
                {
                    if (cardChosen.Type == XCardType.Culprit)
                    {
                        Winner = p;
                        WinnerType = XCardType.Dog;
                        // Dog wins
                        Send(GetTranslation("DogThrowCulprit", GetName(p), GetName(p2)));
                        NowAction = GameAction.Ending;
                        return;
                    }
                    else
                    {
                        UseCard(p2, cardChosen, true);
                        p2.Cards.Add(p.TempCard); //get the dog as compensation
                        Send(GetTranslation("DogTransferCard", GetName(p), GetName(p2), GetName(cardChosen)));
                        p.CardChanged = true;
                        p2.CardChanged = true;
                        p.TempCard = null;
                    }
                }
                NowAction = GameAction.Next;
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        public void NextPlayer()
        {
            var p = PlayerQueue.Dequeue();
            p.ReAnswer = false;
            p.PlayerChoice1 = 0;
            p.PlayerChoice2 = 0;
            p.CardChoice1 = null;
            p.CardChoice2 = null;
            PlayerQueue.Enqueue(p);
            NowAction = GameAction.NormalCard;
            foreach (var player in Players)
            {
                if (player.CardChanged)
                    SendPM(player, GenerateOwnCard(player));
                player.CardChanged = false;
            }
        }

        public bool DumpCard(XPlayer p)
        {
            if (p.Cards.Count == 1 && p.Cards[0].Type == XCardType.Culprit)
            { 
                Send(GetTranslation("DumpCulprit", GetName(p), GetName(p.Cards[0])));
                return true;
            }
            var cards = p.Cards.FindAll(x => x.Type != XCardType.Culprit);
            var c = cards[Helper.RandomNum(cards.Count)];
            Send(GetTranslation(c.Type == XCardType.Accomplice ? "DumpAccomplice" : "DumpCard", GetName(p), GetName(c)));
            UseCard(p, c, true);
            return false;
        }

        #endregion

        #region Preparation
        private void AddPlayer(User u, bool newGame = false)
        {
            var player = this.Players.FirstOrDefault(x => x.TelegramUserId == u.Id);
            if (player != null)
                return;

            player = this.Players.FirstOrDefault(x => x.Name.ToLower() == u.FirstName.ToLower());
            var accomp = GetTranslation("AccompliceAppendName");                     // Avoid joining with (Accomplice) in name
            if (player != null || u.FirstName.ToLower().Contains(accomp.ToLower()))  // Avoid 2 players having the same name
            {
                Send(GetTranslation("ChangeNameToJoin", u.GetName()));
                return;
            }



            using (var db = new CrimDanceDb())
            {
                var DbPlayer = db.Players.FirstOrDefault(x => x.TelegramId == u.Id);
                if (DbPlayer == null)
                {
                    DbPlayer = new Player
                    {
                        TelegramId = u.Id,
                        Name = u.FirstName,
                        Language = "English"
                    };
                    db.Players.Add(DbPlayer);
                    db.SaveChanges();
                }
                XPlayer p = new XPlayer
                {
                    Name = u.FirstName,
                    Id = DbPlayer.Id,
                    TelegramUserId = u.Id,
                    Username = u.Username
                };
                try
                {
                    Message ret;
                    try
                    {
                        ret = SendPM(p, GetTranslation("YouJoined", GroupName));
                    }
                    catch
                    {
                        Bot.Send(ChatId, GetTranslation("NotStartedBot", GetName(u)), GenerateStartMe());
                        return;
                    }
                }
                catch { }
                this.Players.Add(p);
            }
            if (!newGame)
                _secondsToAdd += 15;

            do
            {
                XPlayer p = Players.FirstOrDefault(x => Players.Count(y => y.TelegramUserId == x.TelegramUserId) > 1);
                if (p == null) break;
                Players.Remove(p);
            }
            while (true);

            Send(GetTranslation("JoinedGame", GetName(u)) + Environment.NewLine + GetTranslation("JoinInfo", Players.Count, 3, 8));
        }

        private void RemovePlayer(User user)
        {
            if (this.Phase != GamePhase.Joining) return;

            var player = this.Players.FirstOrDefault(x => x.TelegramUserId == user.Id);
            if (player == null)
                return;

            this.Players.Remove(player);

            do
            {
                XPlayer p = Players.FirstOrDefault(x => Players.Count(y => y.TelegramUserId == x.TelegramUserId) > 1);
                if (p == null) break;
                Players.Remove(p);
            }
            while (true);

            Send(GetTranslation("FledGame", user.GetName()) + Environment.NewLine + GetTranslation("JoinInfo", Players.Count, 3, 8));
        }

        public void PrepareGame(int NumOfPlayers)
        {
            var tempPlayerList = Players.Shuffle();
            PlayerQueue = new Queue<XPlayer>(tempPlayerList);
            Cards = new XCardDeck(NumOfPlayers);
            for (int i = 0; i < NumOfPlayers; i++)
                Players[i].Cards.AddRange(Cards.Cards.Where((x, y) => y % NumOfPlayers == i));
            foreach (XPlayer p in Players)
            {
                p.CardChoice1 = null;
                p.CardChoice2 = null;
                p.PlayerChoice1 = 0;
                p.PlayerChoice2 = 0;
            }
        }

        public void EndGame()
        {
            var msg = "";
            switch (WinnerType)
            {
                case XCardType.Culprit:
                    var culprit = Winner;
                    var accomplices = Players.FindAll(x => x.Accomplice == true && x.TelegramUserId != culprit.TelegramUserId);
                    msg = GetTranslation("WinningCulprit", GetName(culprit));
                    if (accomplices.Count > 0)
                        msg += GetTranslation("WinningAccomplices", accomplices.Select(x => GetName(x)).Aggregate((x, y) => x + GetTranslation("And") + y));
                    msg += GetTranslation("WinningCulpritWon");
                    if (accomplices.Count > 0)
                    {
                        foreach (var p in accomplices)
                            p.Won = true;
                    }
                    culprit.Won = true;
                    break;
                case XCardType.Dog:
                    var dog = Winner;
                    dog.Won = true;
                    msg = GetTranslation("WinningDog", GetName(dog));
                    break;
                case XCardType.Detective:
                    var detective = Winner;
                    var bad = Players.FindAll(x => x.Accomplice == true || x.TelegramUserId == Culprit.TelegramUserId);
                    if (detective.Accomplice)
                        msg = GetTranslation("AccompliceTraitorDetectCulprit", detective.GetName());
                    else
                        msg = GetTranslation("WinningDetective", GetName(detective));
                    foreach (var p in Players.Where(x => !bad.Contains(x)))
                        p.Won = true;
                    break;
            }
            Send(msg);

            // db
            using (var db = new CrimDanceDb())
            {
                foreach (var p in Players)
                {
                    var gp = db.GamePlayers.FirstOrDefault(x => x.GameId == GameId && x.PlayerId == p.Id);
                    gp.Won = p.Won;
                    gp.Accomplice = p.Accomplice;
                }
                db.SaveChanges();
                var g = db.Games.FirstOrDefault(x => x.Id == GameId);
                g.TimeEnded = DateTime.Now;
                g.WinningTeam = WinnerType == XCardType.Culprit ? "Bad" : WinnerType == XCardType.Dog ? "Dog" : "Good";
                db.SaveChanges();
            }
            Phase = GamePhase.Ending;
        }
        #endregion

        #region Helpers
        public void HandleMessage(Message msg)
        {
            switch (msg.Text.ToLower().Substring(1).Split()[0].Split('@')[0])
            {
                case "join":
                    if (Phase == GamePhase.Joining)
                        AddPlayer(msg.From);
                    break;
                case "flee":
                    if (Phase == GamePhase.Joining)
                        RemovePlayer(msg.From);
                    else if (Phase == GamePhase.InGame)
                        Send(GetTranslation("CantFleeRunningGame"));
                    break;
                case "startgame":
                    if (Phase == GamePhase.Joining)
                        AddPlayer(msg.From);
                    break;
                case "forcestart":
                    if (this.Players.Count() >= 3) Phase = GamePhase.InGame;
                    else
                    {
                        Send(GetTranslation("GameEnded"));
                        Phase = GamePhase.Ending;
                        Bot.Gm.RemoveGame(this);
                    }
                    break;
                case "killgame":
                    Send(GetTranslation("KillGame"));
                    Phase = GamePhase.Ending;
                    Bot.Gm.RemoveGame(this);
                    break;
                case "seq":
                    if (_playerList == 0)
                        Reply(msg.MessageId, GetTranslation("PlayerSequenceNotStarted"));
                    else
                        Reply(_playerList, GetTranslation("GetPlayerSequence"));
                    break;
                case "extend":
                    if (Phase == GamePhase.Joining)
                    {
                        _secondsToAdd += Constants.ExtendTime;
                        Reply(msg.MessageId, GetTranslation("ExtendJoining", Constants.ExtendTime));
                    }
                    break;
            }
        }

        public void HandleQuery(CallbackQuery query, string[] args)
        {
            // args[0] = GameGuid
            // args[1] = playerId
            // args[2] = gameActionType
            // args[3] = cardId / playerId
            XPlayer p = Players.FirstOrDefault(x => x.TelegramUserId == Int32.Parse(args[1]));
            if (p != null)
            {

                GameAction actionType = (GameAction)Int32.Parse(args[2]);
                bool isPlayer = false;
                switch (actionType)
                {
                    case GameAction.NormalCard:
                        p.CardChoice1 = args[3];
                        break;
                    case GameAction.Witness:
                        var playerChoice1 = Int32.Parse(args[3]);
                        XPlayer p2 = Players.FirstOrDefault(x => x.TelegramUserId == playerChoice1);
                        if (p2 != null)
                        {
                            var cards = GenerateOwnCard(p2, true);
                            /* BotMethods.AnswerCallback(query, cards, true); */ // change back to old send message + delete method
                            
                            new Task(() => {
                                var sent = SendPM(p, GetTranslation("CardsInPlayer", p2.GetName()) + Environment.NewLine + cards);
                                Thread.Sleep(Constants.WitnessTime * 1000);
                                Bot.Api.DeleteMessageAsync(sent.Chat.Id, sent.MessageId);
                            });
                        }
                        p.PlayerChoice1 = playerChoice1;
                        isPlayer = true;
                        break;
                    case GameAction.Barter:
                        int a;
                        if (int.TryParse(args[3], out a))
                        {
                            p.PlayerChoice1 = a;
                            isPlayer = true;
                        }
                        else
                            p.CardChoice1 = args[3];
                        break;
                    case GameAction.InfoExchange:
                        p.CardChoice1 = args[3];
                        break;
                    case GameAction.Detective:
                        p.PlayerChoice1 = Int32.Parse(args[3]);
                        isPlayer = true;
                        break;
                    case GameAction.Dog:
                        int b;
                        if (int.TryParse(args[3], out b))
                        {
                            p.PlayerChoice1 = b;
                            isPlayer = true;
                        }
                        else
                            p.CardChoice1 = args[3];
                        break;
                }
                Bot.Edit(p.TelegramUserId, p.CurrentQuestion.MessageId, $"{GetTranslation("ReceivedButton")} - {(isPlayer == true ? Players.FirstOrDefault(x => x.TelegramUserId == p.PlayerChoice1).Name : GetName(p.Cards.FirstOrDefault(x => x.Id == p.CardChoice1)))}");
                p.CurrentQuestion = null;
            }
        }

        public Message Send(string msg)
        {
            return Bot.Send(ChatId, msg);
        }

        public Message SendPM(XPlayer p, string msg)
        {
            return Bot.Send(p.TelegramUserId, msg);
        }

        public Message SendMenu(XPlayer p, string msg, InlineKeyboardMarkup markup, QuestionType qType)
        {
            var sent = Bot.Send(p.TelegramUserId, msg, markup);
            p.CurrentQuestion = new QuestionAsked
            {
                Type = qType,
                MessageId = sent.MessageId
            };
            return sent;
        }

        public Message Reply(int oldMessageId, string msg)
        {
            return BotMethods.Reply(ChatId, oldMessageId, msg);
        }

        public Message SendTimesUp(XPlayer p, int currentQuestionMsgId)
        {
            return Bot.Edit(p.TelegramUserId, currentQuestionMsgId, GetTranslation("TimesUpButton"));
        }

        public InlineKeyboardMarkup GenerateMenu(XPlayer p, List<XCard> cardList, GameAction action)
        {
            var buttons = new List<Tuple<string, string>>();
            foreach (XCard card in cardList)
            {
                buttons.Add(new Tuple<string, string>(GetName(card), $"{this.Id}|{p.TelegramUserId}|{(int)action}|{card.Id}"));
            }
            var row = new List<InlineKeyboardButton>();
            var rows = new List<InlineKeyboardButton[]>();

            for (int i = 0; i < buttons.Count; i++)
            {
                row.Clear();
                row.Add(new InlineKeyboardCallbackButton(buttons[i].Item1, buttons[i].Item2));
                rows.Add(row.ToArray());
            }
            return new InlineKeyboardMarkup(rows.ToArray());
        }

        public InlineKeyboardMarkup GenerateMenu(XPlayer p, List<XPlayer> players, GameAction action)
        {
            var buttons = new List<Tuple<string, string>>();
            foreach (XPlayer player in players)
            {
                buttons.Add(new Tuple<string, string>(player.Name, $"{this.Id}|{p.TelegramUserId}|{(int)action}|{player.TelegramUserId}"));
            }
            var row = new List<InlineKeyboardButton>();
            var rows = new List<InlineKeyboardButton[]>();

            for (int i = 0; i < buttons.Count; i++)
            {
                row.Clear();
                row.Add(new InlineKeyboardCallbackButton(buttons[i].Item1, buttons[i].Item2));
                rows.Add(row.ToArray());
            }
            return new InlineKeyboardMarkup(rows.ToArray());
        }

        public InlineKeyboardMarkup GenerateStartMe()
        {
            var row = new List<InlineKeyboardButton>();
            var rows = new List<InlineKeyboardButton[]>();
            row.Add(new InlineKeyboardUrlButton(GetTranslation("StartMe"), $"https://telegram.me/{Bot.Me.Username}"));
            rows.Add(row.ToArray());
            return new InlineKeyboardMarkup(rows.ToArray());
        }

        public string GeneratePlayerList()
        {
            try
            {
                var msg = GetTranslation("PlayersTurn", GetName(PlayerQueue.First())) + Environment.NewLine;
                msg += GetTranslation("CurrentSequence") + Environment.NewLine;
                List<string> playerList = new List<string>();
                foreach (var p in PlayerQueue.ToList())
                {
                    playerList.Add(GetName(p) + $" ({p.Cards.Count})");
                }
                msg += $"{playerList.Aggregate((x, y) => x + GetTranslation("SequenceJoinSymbol") + Environment.NewLine + y)}";
                return msg;
            }
            catch (Exception ex)
            {
                Log(ex);
                return "";
            }
        }

        public string GetName(XPlayer p)
        {
            return Helper.GetName(p);
        }

        public string GetName(User p)
        {
            return Helper.GetName(p);
        }

        public string GetName(XCard c)
        {
            return GetTranslation(c.Name);
        }

        public string GetName(XCardType t)
        {
            return GetTranslation(t.ToString());
        }

        public string GenerateOwnCard(XPlayer p, bool witness = false)
        {
            string m = "";
            if (witness)
                m = GetTranslation("CardsInPlayer", p.Name) + Environment.NewLine;
            else
                m = GetTranslation("CardsInHand") + Environment.NewLine;
            for (int i = 0; i < p.Cards.Count; i++)
            {
                m += $"{i + 1}. {GetName(p.Cards[i])}\n";
            }
            return m;
        }

        public void NotifyNextGamePlayers()
        {
            var grpId = ChatId;
            using (var db = new CrimDanceDb())
            {
                var dbGrp = db.Groups.FirstOrDefault(x => x.GroupId == grpId);
                if (dbGrp != null)
                {
                    var toNotify = db.NotifyGames.Where(x => x.GroupId == grpId && x.UserId != Initiator.TelegramUserId).Select(x => x.UserId).ToList();
                    foreach (int user in toNotify)
                    {
                        Bot.Send(user, GetTranslation("GameIsStarting", GroupName));
                    }
                    db.Database.ExecuteSqlCommand($"DELETE FROM NotifyGame WHERE GROUPID = {grpId}");
                    db.SaveChanges();
                }
            }
        }

        /*
        public void RemoveFromNextGame(List<int> players)
        {
            using (var db = new CrimDanceDb())
            {
                var grpId = ChatId;
                var dbGrp = db.Groups.FirstOrDefault(x => x.GroupId == grpId);
                if (dbGrp != null)
                {
                    
                }
            }
        }
        */

        public void Dispose()
        {
            Players?.Clear();
            Players = null;
            PlayerQueue?.Clear();
            PlayerQueue = null;
            Cards = null;
            // MessageQueueing = false;
        }

        public void Log(Exception ex)
        {
            Helper.LogError(ex, ChatId);
            Send("Sorry there is some problem with me, I gonna go die now.");
            this.Phase = GamePhase.Ending;
            Bot.Gm.RemoveGame(this);
        }
        #endregion

        #region Language related
        public void LoadLanguage(string language)
        {
            try
            {
                var files = Directory.GetFiles(Constants.GetLangDirectory());
                var file = files.First(x => Path.GetFileNameWithoutExtension(x) == language);
                {
                    var doc = XDocument.Load(file);
                    Locale = new Locale
                    {
                        Language = Path.GetFileNameWithoutExtension(file),
                        File = doc
                    };
                }
                Language = Locale.Language;
            }
            catch
            {
                if (language != "English")
                    LoadLanguage("English");
            }
        }

        private string GetTranslation(string key, params object[] args)
        {
            try
            {
                var strings = Locale.File.Descendants("string").FirstOrDefault(x => x.Attribute("key")?.Value == key) ??
                              Program.English.Descendants("string").FirstOrDefault(x => x.Attribute("key")?.Value == key);
                if (strings != null)
                {
                    var values = strings.Descendants("value");
                    var choice = Helper.RandomNum(values.Count());
                    var selected = values.ElementAt(choice).Value;

                    return String.Format(selected, args).Replace("\\n", Environment.NewLine);
                }
                else
                {
                    throw new Exception($"Error getting string {key} with parameters {(args != null && args.Length > 0 ? args.Aggregate((a, b) => a + "," + b.ToString()) : "none")}");
                }
            }
            catch (Exception e)
            {
                try
                {
                    //try the english string to be sure
                    var strings =
                        Program.English.Descendants("string").FirstOrDefault(x => x.Attribute("key")?.Value == key);
                    var values = strings?.Descendants("value");
                    if (values != null)
                    {
                        var choice = Helper.RandomNum(values.Count());
                        var selected = values.ElementAt(choice - 1).Value;
                        // ReSharper disable once AssignNullToNotNullAttribute
                        return String.Format(selected, args).Replace("\\n", Environment.NewLine);
                    }
                    else
                        throw new Exception("Cannot load english string for fallback");
                }
                catch
                {
                    throw new Exception(
                        $"Error getting string {key} with parameters {(args != null && args.Length > 0 ? args.Aggregate((a, b) => a + "," + b.ToString()) : "none")}",
                        e);
                }
            }
        }

        #endregion
        #region Constants

        public enum GamePhase
        {
            Joining, InGame, Ending
        }

        public enum GameAction
        {
            FirstFinder, NormalCard, Rumor, InfoExchange, Barter, Detective, Dog, Witness, Ending, Next
        }

        #endregion
    }
}
