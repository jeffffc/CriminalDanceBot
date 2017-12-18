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
        public static long LogGroupId = -1001374190576;
        public static int[] Dev = new int[] { 106665913 };
    }
}
