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
        public static bool MaintMode = false;

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledException;

            Bot.Api = new TelegramBotClient(Constants.GetBotToken("BotToken"));
            Bot.Me = Bot.Api.GetMeAsync().Result;

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

            English = Helper.ReadEnglish();
            Langs = Helper.ReadLanguageFiles();

            Bot.Api.GetUpdatesAsync(-1).Wait();
            Handler.HandleUpdates(Bot.Api);
            Bot.Api.StartReceiving();
            new Thread(UpdateConsole).Start();
            Console.ReadLine();
            Bot.Api.StopReceiving();
        }

        private static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception exc = (Exception)e.ExceptionObject;
            string message = Environment.NewLine + Environment.NewLine + exc.Message + Environment.NewLine + Environment.NewLine;
            string trace = exc.StackTrace;

            do
            {
                exc = exc.InnerException;
                if (exc == null) break;
                message += exc.Message + Environment.NewLine + Environment.NewLine;
            }
            while (true);

            message += trace;
            Bot.Send(Constants.LogGroupId, "<b>UNHANDELED EXCEPTION! BOT IS PROBABLY CRASHING!</b>" + message.FormatHTML());
            Thread.Sleep(5000); // Give the message time to be sent
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
