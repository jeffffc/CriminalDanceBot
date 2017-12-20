using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using System.Runtime.Caching;
using CriminalDanceBot.Handlers;
using CriminalDanceBot.Models;
using System.Threading;
using ConsoleTables;
using System.Xml.Linq;
using System.IO;

namespace CriminalDanceBot
{
    class Program
    {
        internal static XDocument English;
        public static Dictionary<string, XDocument> Langs;
        public static readonly MemoryCache AdminCache = new MemoryCache("GroupAdmins");

        static void Main(string[] args)
        {
            Bot.Api = new TelegramBotClient(Constants.GetBotToken("BotToken"));
            Bot.Me = Bot.Api.GetMeAsync().Result;

            Langs = new Dictionary<string, XDocument>();
            English = XDocument.Load(Path.Combine(Constants.GetLangDirectory(), "English.xml"));

            Bot.Gm = new GameManager();

            Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            Console.Title = $"CriminalDanceBot - Connected to {Bot.Me.FirstName} (@{Bot.Me.Username} | {Bot.Me.Id}) - Version {version.ToString()}";

            foreach (var m in typeof(Commands).GetMethods())
            {
                foreach (var a in m.GetCustomAttributes(true))
                {
                    if (a is Attributes.Command cmd)
                    {
                        var method = m.CreateDelegate(typeof(Bot.CommandMethod)) as Bot.CommandMethod;
                        Bot.Commands.Add(new Models.Command(cmd.Trigger, cmd.AdminOnly, cmd.DevOnly, cmd.GroupOnly, method));
                    }
                    
                }
            }

            var files = Directory.GetFiles(Constants.GetLangDirectory());
            try
            {
                foreach (var file in files)
                {
                    var lang = Path.GetFileNameWithoutExtension(file);
                    XDocument doc = XDocument.Load(file);
                    Langs.Add(lang, doc);
                }
            }
            catch { }

            Handler.HandleUpdates(Bot.Api);
            Bot.Api.StartReceiving();
            new Thread(UpdateConsole).Start();
            Console.ReadLine();
            Bot.Api.StopReceiving();
        }

        private static void UpdateConsole()
        {
            DateTime dt = DateTime.Now;
            while (true)
            {
                Console.Clear();
                var Uptime = DateTime.Now - dt;
                string msg = $"Startup Time: {dt.ToString()}";
                msg += Environment.NewLine + $"Uptime: {Uptime.ToString()}";
                var games = Bot.Gm.Games;
                int gameCount = games.Count();

                msg += Environment.NewLine + $"Number of Games: {gameCount.ToString()}";
                Console.WriteLine(msg);

                var table = new ConsoleTable("Game GUID", "Phase", "InGame Action", "# of Players");
                foreach (CriminalDance game in games)
                {
                    table.AddRow(game.Id.ToString(), game.Phase.ToString(), game.Phase == CriminalDance.GamePhase.InGame ? game.NowAction.ToString() : "------", game.Players.Count().ToString());
                }
                table.Write(Format.Alternative);
                
                Thread.Sleep(2000);
            }
        }
    }
}
