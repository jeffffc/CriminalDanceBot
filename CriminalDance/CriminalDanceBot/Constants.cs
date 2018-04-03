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
        # log path
        private static string _logPath = @"C:\Logs\MyTelegramBot.log";
        public static string GetLogPath()
        {
            return Path.GetFullPath(_logPath);
        }
        private static string _languageDirectory = @"C:\CrimDanceLanguages";
        private static string _languageTempDirectory = Path.Combine(_languageDirectory, @"temp\");
        public static string GetLangDirectory(bool temp = false)
        {
            return (!temp) ? Path.GetFullPath(_languageDirectory) : Path.GetFullPath(_languageTempDirectory);
        }
        public static long LogGroupId = -1001117997439;
        public static int[] Dev = new int[] { 106665913, 295152997 };

        #region GameConstants
        public static int JoinTime = 120;
        public static int JoinTimeMax = 300;
#if DEBUG
        public static int ChooseCardTime = 60;
#else
        public static int ChooseCardTime = 45;
#endif
        public static int ExtendTime = 30;
        public static int WitnessTime = 15;

        #endregion

        public static string DonationLiveToken = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey("SOFTWARE\\CriminalDanceBot").GetValue("DonationLiveToken").ToString();
        public static string DonationPayload = "CRIMINALDANCEBOTPAYLOAD:";

    }
}
