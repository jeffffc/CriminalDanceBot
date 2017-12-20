using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CriminalDanceBot
{
    public class Constants
    {
        // Token from registry
        private static RegistryKey _key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey("SOFTWARE\\CriminalDanceBot");
        public static string GetBotToken(string key)
        {
            return _key.GetValue(key, "").ToString();
        }
        private static string _logPath = @"C:\Logs\MyTelegramBot.log";
        public static string GetLogPath()
        {
            return Path.GetFullPath(_logPath);
        }
        private static string _languageDirectory = @"D:\CrimDanceLanguages";
        public static string GetLangDirectory()
        {
            return Path.GetFullPath(_languageDirectory);
        }
        public static long LogGroupId = -1001374190576;
        public static int[] Dev = new int[] { 106665913 };

        #region GameConstants
        public static int JoinTime = 120;
        public static int JoinTimeMax = 300;
#if DEBUG
        public static int ChooseCardTime = 100;
#else
        public static int ChooseCardTime = 45;
#endif

        #endregion
    }
}
