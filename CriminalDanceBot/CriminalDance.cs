using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using CriminalDanceBot.Models;
using System.Threading;
using Database;
using System.Diagnostics;
using System.IO;

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
        public List<XCard> Cards = new List<XCard>();
        public GamePhase Phase = GamePhase.Joining;

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
            AddPlayer(u);
            new Thread(GameTimer).Start();
        }

        private void GameTimer()
        {
            for (var i = 0; i < 20; i++)
            {
                if (this.Phase == GamePhase.InGame)
                    break;
                if (this.Players.Count() >= 5)
                {
                    this.Phase = GamePhase.InGame;
                }
                Thread.Sleep(1000);
            }
            if (this.Phase != GamePhase.InGame)
            {
                this.Phase = GamePhase.Ending;
                Bot.Gm.RemoveGame(this);
            }
        }

        private void AddPlayer(User u)
        {
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
                    TelegramUser = u
                };
                this.Players.Add(p);
            }
        }

        public void HandleMessage(Message msg)
        {

        }

        public void Dispose()
        {
            Players?.Clear();
            Players = null;
            PlayerQueue?.Clear();
            PlayerQueue = null;
            Cards?.Clear();
            Cards = null;
            // MessageQueueing = false;
        }

        public enum GamePhase
        {
            Joining, InGame, Ending
        }
    }
}
