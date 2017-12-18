﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CriminalDanceBot.Models
{
    public class XCard
    {
        public string Name { get; set; }
        public string Desc { get; set; }
        public XCardType Type { get; set; }
    }

    public enum XCardType
    {
        FirstFinder, Culprit, Accomplice, Detective, Alibi, Dog, Rumor, InformationExchange, Witness, Barter, Bystander
    }
}
