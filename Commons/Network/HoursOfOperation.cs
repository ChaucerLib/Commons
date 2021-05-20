using System;
using System.Collections.Generic;

namespace Commons.Network
{
    public class HoursOfOperation
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<DayOfWeek, TimeRange> Hours { get; set; }
    }
}