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

        public Locale Locale;
        public string Language = "English";

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

            Bot.Send(chatId, GetTranslation("NewGame", u.FirstName));
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
                                default:
                                    break;
                            }
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

        public void UseCard(XPlayer p, XCard card, bool dump = false)
        {
            var c = p.Cards.FirstOrDefault(x => x == card);
            p.UsedCards.Add(c);
            p.Cards.Remove(c);
            if (!dump)
                Send($"{p.Name} just used {card.Name}");
        }

        public void NormalActions()
        {
            try
            {
                var p = PlayerQueue.First();
                p.CardChoice1 = null;
                if (p.ReAnswer != true)
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
                {
                    DumpCard(p);
                    NowAction = GameAction.Next;
                }
                var card = p.Cards.FirstOrDefault(x => x.Id == p.CardChoice1);
                p.CurrentQuestion = null;

                // What card?
                switch (card.Type)
                {
                    case XCardType.Accomplice:
                        if (!p.Accomplice)
                        {
                            p.Accomplice = true;
                            Send($"{p.Name} declared themselves as an accomplice.");
                            p.Name += " (Accomplice)";
                        }
                        else
                        {
                            Send($"{p.Name} was already an accomplice, card dumped.");
                        }
                        NowAction = GameAction.Next;
                        UseCard(p, card);
                        break;
                    case XCardType.Alibi:
                    case XCardType.Bystander:
                    case XCardType.Witness:
                        Send("This card is useless at this moment, card dumped");
                        NowAction = GameAction.Next;
                        UseCard(p, card);
                        break;
                    case XCardType.Culprit:
                        if (p.Cards.Count == 1) { }
                        // to do
                        else
                        {
                            p.ReAnswer = true;
                            SendMenu(p, "You can only use Culprit when you have 1 card left, please choose again.", GenerateMenu(p, p.Cards, GameAction.NormalCard), QuestionType.Card);
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
                        NowAction = GameAction.Dog;
                        break;
                    case XCardType.InformationExchange:
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
                XPlayer p2 = null;
                p.PlayerChoice1 = 0;
                SendMenu(p, "Which player do you want to use Barter on?", GenerateMenu(p, Players.FindAll(x => x != p), GameAction.Barter), QuestionType.Player);
                for (int i = 0; i < Constants.ChooseCardTime; i++)
                {
                    Thread.Sleep(1000);
                    if (p.PlayerChoice1 != 0)
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
                    p2 = Players.FindAll(x => x != p)[Helper.RandomNum(Players.Count - 1)];
                }
                else
                {
                    p2 = Players.FirstOrDefault(x => x.TelegramUserId == p.PlayerChoice1);
                }
                var BarterPlayers = new List<XPlayer> { p, p2 };
                foreach (XPlayer player in BarterPlayers)
                {
                    player.CardChoice1 = null;
                    SendMenu(player, "BARTER: Which card do you wanna exchange?", GenerateMenu(player, player.Cards, GameAction.Barter), QuestionType.Card);
                }
                for (int i = 0; i < Constants.ChooseCardTime; i++)
                {
                    Thread.Sleep(1000);
                    if (BarterPlayers.All(x => x.CardChoice1 != null))
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
                Send($"{p.Name} and {p2.Name} exchanged their cards.");
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
                foreach (XPlayer player in Players)
                {
                    player.CardChoice1 = null;
                    SendMenu(player, "INFO-EXCHANGE: Which card do you wanna exchange?", GenerateMenu(player, player.Cards, GameAction.InfoExchange), QuestionType.Card);
                }
                for (int i = 0; i < Constants.ChooseCardTime; i++)
                {
                    Thread.Sleep(1000);
                    if (Players.All(x => x.CardChoice1 != null))
                        break;
                }
                try
                {
                    foreach (var player in Players)
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
                foreach (var player in Players)
                {
                    if (player.CardChoice1 == null)
                    {
                        player.CardChoice1 = player.Cards[Helper.RandomNum(player.Cards.Count)].Id;
                    }
                }

                // switch the cards now
                for (int i = 0; i < PlayerQueue.Count; i++)
                {
                    var p = PlayerQueue.ElementAt(i);
                    XPlayer next;
                    if (i < PlayerQueue.Count - 1)
                        next = PlayerQueue.ElementAt(i + 1);
                    else
                        next = PlayerQueue.First();
                    var card = p.Cards.FirstOrDefault(x => x.Id == p.CardChoice1);
                    p.Cards.Remove(card);
                    next.Cards.Add(card);
                    p.CardChanged = true;
                }
                Send($"Information Exchange Completed.");
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
                foreach (var player in Players)
                    player.CardChoice1 = player.Cards[Helper.RandomNum(player.Cards.Count)].Id;


                // switch the cards now
                for (int i = 0; i < PlayerQueue.Count; i++)
                {
                    var p = PlayerQueue.ElementAt(i);
                    XPlayer next;
                    if (i < PlayerQueue.Count - 1)
                        next = PlayerQueue.ElementAt(i + 1);
                    else
                        next = PlayerQueue.First();
                    var card = p.Cards.FirstOrDefault(x => x.Id == p.CardChoice1);
                    p.Cards.Remove(card);
                    next.Cards.Add(card);
                    p.CardChanged = true;
                }
                Send($"Rumor Completed.");
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
                    SendMenu(p, "Whdo you think is the Culprit?", GenerateMenu(p, Players.FindAll(x => x != p), GameAction.Detective), QuestionType.Player);
                    for (int i = 0; i < Constants.ChooseCardTime; i++)
                    {
                        Thread.Sleep(1000);
                        if (p.PlayerChoice1 != 0)
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
                        Send($"{p.Name} failed to choose in time. Detective card dumped.");
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
                            Send($"{p2.Name} is not the culprit.");
                        }
                        else
                        {
                            if (p2.Cards.FirstOrDefault(x => x.Type == XCardType.Alibi) == null)
                            {
                                // has culprit, no alibi ==> lose
                                // EndGame();
                                NowAction = GameAction.Ending;
                            }
                            else
                            {
                                // is culprit but has alibi ==> nothing happens
                                Send($"{p2.Name} is not the culprit.");
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
                SendMenu(p, "Which player do you want to use Dog on?", GenerateMenu(p, Players.FindAll(x => x != p), GameAction.Dog), QuestionType.Player);
                for (int i = 0; i < Constants.ChooseCardTime; i++)
                {
                    Thread.Sleep(1000);
                    if (p.PlayerChoice1 != 0)
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
                    p2 = Players.FindAll(x => x != p)[Helper.RandomNum(Players.Count - 1)];
                }
                else
                {
                    p2 = Players.FirstOrDefault(x => x.TelegramUserId == p.PlayerChoice1);
                }

                p2.CardChoice1 = null;
                SendMenu(p2, "DOG: Which card do you want to throw away?", GenerateMenu(p2, p2.Cards, GameAction.Dog), QuestionType.Card);

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
                        //EndGame()
                        // Dog wins
                        Send($"{p.Name} used Dog on {p2.Name} and {p2.Name} threw out the Culprit! {p.Name} won!");
                        NowAction = GameAction.Ending;
                        return;
                    }
                    else
                    {
                        UseCard(p2, cardChosen, true);
                        p.Cards.Add(cardChosen);
                        Send($"{p.Name} used Dog on {p2.Name} and {p2.Name} gave {cardChosen.Name} to {p.Name}.");
                        p.CardChanged = true;
                        p2.CardChanged = true;
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

        public void DumpCard(XPlayer p)
        {
            var cards = p.Cards.FindAll(x => x.Type != XCardType.Culprit);
            var c = cards[Helper.RandomNum(cards.Count)];
            Send($"{p.Name} did not choose in time, I helped him dump a random card: {c.Name}");
            UseCard(p, c, true);
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
            // args[3] = cardId / playerId
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
                    case GameAction.Barter:
                        Bot.Edit(p.TelegramUserId, p.CurrentQuestion.MessageId, "OK!");
                        int a;
                        if (int.TryParse(args[3], out a))
                            p.PlayerChoice1 = a;
                        else
                            p.CardChoice1 = args[3];
                        break;
                    case GameAction.InfoExchange:
                        Bot.Edit(p.TelegramUserId, p.CurrentQuestion.MessageId, "OK!");
                        p.CardChoice1 = args[3];
                        break;
                    case GameAction.Detective:
                        Bot.Edit(p.TelegramUserId, p.CurrentQuestion.MessageId, "OK!");
                        p.PlayerChoice1 = Int32.Parse(args[3]);
                        break;
                    case GameAction.Dog:
                        Bot.Edit(p.TelegramUserId, p.CurrentQuestion.MessageId, "OK!");
                        int b;
                        if (int.TryParse(args[3], out b))
                            p.PlayerChoice1 = b;
                        else
                            p.CardChoice1 = args[3];
                        break;
                }
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

        public string GenerateOwnCard(XPlayer p)
        {
            string m = "Cards in Hand:\n";
            for (int i = 0; i < p.Cards.Count; i++)
            {
                m += $"{i + 1}. {p.Cards[i].Name}\n";
            }
            return m;
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
                    var selected = values.ElementAt(choice - 1).Value;

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
            FirstFinder, NormalCard, Rumor, InfoExchange, Barter, Detective, Dog, Ending, Next
        }

        #endregion
    }
}
