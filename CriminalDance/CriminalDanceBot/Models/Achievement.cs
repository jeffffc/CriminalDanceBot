using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static CriminalDanceBot.Commands;

namespace CriminalDanceBot.Models
{
    [Flags]
    public enum Achievements : long
    {
        None = 0,
        LetsDance = 1, // Play a game
        AfterParty = 2, // Win a game
        OfficiallyCulprit = 4, // Win as a culprit
        YouBastard = 8, // Win as a Dog
        Addicted = 16, // Play 100 games
        ProDancer = 32, //Win 100 games
        Waltz = 64, // Play a game > 30 minutes
    } // MAX VALUE: 9223372036854775807
      //            

    public static partial class Extensions
    {
        public static string GetAchvDescription(this Achievements value, string language)
        {
            return GetTranslation($"Achv{value.ToString()}Desc", language);
        }
        public static string GetAchvName(this Achievements value, string language)
        {
            return GetTranslation($"Achv{value.ToString()}Name", language);
        }

        public static IEnumerable<Achievements> GetUniqueFlags(this Enum flags, bool no = false)
        {
            ulong flag = 1;
            foreach (var value in Enum.GetValues(flags.GetType()).Cast<Achievements>())
            {
                ulong bits = Convert.ToUInt64(value);
                while (flag < bits)
                {
                    flag <<= 1;
                }

                if (flag == bits && no == false ? flags.HasFlag(value) : !flags.HasFlag(value))
                {
                    yield return value;
                }
            }
        }
    }
}