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

namespace CriminalDanceBot
{
    public class CriminalDance : IDisposable
    {
        public long ChatId;
        public string GroupName;
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

        public CriminalDance(long chatId, User u, string groupName)
        {
            #region Creating New Game - Preparation
            using (var db = new CrimDanceDb())
            {
                ChatId = chatId;
                GroupName = groupName;
                DbGroup = db.Groups.FirstOrDefault(x => x.GroupId == ChatId);
                if (DbGroup == null)
                    Bot.Gm.RemoveGame(this);
            }
            // something
            #endregion

            Bot.Send(chatId, "A new game has been started!");
            AddPlayer(u, true);
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
                    if (_secondsToAdd != 0)
                    {
                        i = Math.Max(i - _secondsToAdd, Constants.JoinTime - Constants.JoinTimeMax);
                        Bot.Send(ChatId, $"You still have {TimeSpan.FromSeconds(Constants.JoinTime - i).ToString(@"mm\:ss")} to join.");
                        _secondsToAdd = 0;
                    }
                    var specialTime = JoinTime - i;
                    if (new int[] { 10, 30, 60 }.Contains(specialTime))
                    {
                        Bot.Send(ChatId, $"You still have {specialTime}s to join.");
                    }
                    Thread.Sleep(1000);
                }
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
                    Bot.Send(ChatId, "Game is starting!");
                    PrepareGame(Players.Count());
#if DEBUG
                    string allCards = "";
                    foreach (XPlayer p in Players)
                    {
                        allCards += $"{p.Name} has got: {p.Cards.Select(i => i.Name).Aggregate((i, j) => i + ", " + j)}" + Environment.NewLine;
                    }
                    Bot.Send(ChatId, allCards);
#endif
                    #endregion

                    #region Start!
                    FirstFinder();
                    while (NowAction != GameAction.Ending)
                    {
                        _playerList = Send(GeneratePlayerList()).MessageId;
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
                            default:
                                break;
                        }
                        NextPlayer();
                    }
                    #endregion
                    this.Phase = GamePhase.Ending;
                }
                this.Phase = GamePhase.Ending;
            }
            
            Bot.Gm.RemoveGame(this);
            Bot.Send(ChatId, "Game ended!");
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
                        Send($"{p.Name} has got the First Finder! Next please!");
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

        public void UseCard(XPlayer p, XCard card)
        {
            var c = p.Cards.FirstOrDefault(x => x == card);
            p.UsedCards.Add(c);
            p.Cards.Remove(c);
        }

        public void NormalActions()
        {
            try
            {
                var p = PlayerQueue.First();
                p.CardChoice1 = null;
                SendMenu(p, "Which card do you want to use?", GenerateMenu(p, p.Cards, GameAction.NormalCard), QuestionType.Card);
                for (int i = 0; i < Constants.ChooseCardTime; i++)
                {
                    Thread.Sleep(1000);
                    if (p.CardChoice1 != null)
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
                
                if (p.CardChoice1 == null)
                    p.CardChoice1 = p.Cards[Helper.RandomNum(p.Cards.Count)].Id;
                var card = p.Cards.FirstOrDefault(x => x.Id == p.CardChoice1);
                UseCard(p, card);
                Send($"{p.Name} just used {card.Name}");
                p.CurrentQuestion = null;
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        public void Barter()
        {
            // to be done
        }

        public void InfoExchange()
        {
            // to be done
        }

        public void NextPlayer()
        {
            var p = PlayerQueue.Dequeue();
            PlayerQueue.Enqueue(p);
        }

        public void Rumor()
        {
            // to be done
        }
        #endregion

        #region Preparation
        private void AddPlayer(User u, bool newGame = false)
        {
            var player = this.Players.FirstOrDefault(x => x.TelegramUserId == u.Id);
            if (player != null)
                return;
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
                    TelegramUserId = u.Id
                };
                this.Players.Add(p);
            }
            if (!newGame)
                _secondsToAdd += 15;
            Bot.Send(ChatId, $"{u.FirstName} joined the game!");
        }

        public void PrepareGame(int NumOfPlayers)
        {
            var tempPlayerList = Players.Shuffle();
            PlayerQueue = new Queue<XPlayer>(tempPlayerList);
            Cards = new XCardDeck(NumOfPlayers);
            for (int i = 0; i < NumOfPlayers; i++ )
                Players[i].Cards.AddRange(Cards.Cards.Where((x, y) => y % NumOfPlayers == i));
            foreach (XPlayer p in Players)
            {
                p.CardChoice1 = null;
                p.CardChoice2 = null;
                p.PlayerChoice1 = 0;
                p.PlayerChoice2 = 0;
            }
        }

        #endregion

        #region Helpers
        public void HandleMessage(Message msg)
        {
            if (msg.Text.StartsWith("/join"))
            {
                AddPlayer(msg.From);
            }
            if (msg.Text.StartsWith("/forcestart"))
            {
                Phase = GamePhase.InGame;
            }
            if (msg.Text.StartsWith("/killgame"))
            {
                Send("Sorry the game has to end now...");
                Phase = GamePhase.Ending;
                Bot.Gm.RemoveGame(this);
            }
            if (msg.Text.StartsWith("/seq"))
            {
                if (_playerList == 0)
                    Reply(msg.MessageId, "I suppose the game has not yet started..");
                else
                    Reply(_playerList, "The latest player sequence is here!");
            }
        }

        public void HandleQuery(CallbackQuery query, string[] args)
        {
            // args[0] = GameGuid
            // args[1] = playerId
            // args[2] = gameActionType
            // args[3] = cardId
            XPlayer p = Players.FirstOrDefault(x => x.TelegramUserId == Int32.Parse(args[1]));
            if (p != null)
            {
                GameAction actionType = (GameAction)Int32.Parse(args[2]);
                switch (actionType)
                {
                    case GameAction.NormalCard:
                        Bot.Edit(p.TelegramUserId, p.CurrentQuestion.MessageId, "OK!");
                        p.CardChoice1 = args[3];
                        break;
                }
            }
        }

        public Message Send(string msg)
        {
            return Bot.Send(ChatId, msg);
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
            return Bot.Edit(p.TelegramUserId, currentQuestionMsgId, "Time's up!");
        }

        public InlineKeyboardMarkup GenerateMenu(XPlayer p, List<XCard> cardList, GameAction action)
        {
            var buttons = new List<Tuple<string, string>>();
            foreach (XCard card in cardList)
            {
                buttons.Add(new Tuple<string, string>(card.Name, $"{this.Id}|{p.TelegramUserId}|{(int)action}|{card.Id}"));
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

        public string GeneratePlayerList()
        {
            try
            {
                var msg = $"{PlayerQueue.First().Name}, it is your turn now." + Environment.NewLine;
                msg += "Sequence:" + Environment.NewLine;
                msg += $"{PlayerQueue.ToList().Select(x => x.Name).Aggregate((x, y) => x + " ➡️ " + y)}";
                return msg;
            }
            catch (Exception ex)
            {
                Log(ex);
                return "";
            }
        }

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
            Helper.LogError(ex);
        }
        #endregion

        #region Constants

        public enum GamePhase
        {
            Joining, InGame, Ending
        }

        public enum GameAction
        {
            FirstFinder, NormalCard, Rumor, InfoExchange, Barter, Ending
        }

        #endregion
    }
}
