using System;
using NodaTime;

namespace Commons.Network
{
    public class TimeRange :
        IEquatable<TimeRange>
    {
        public LocalTime Open { get; set; }
        public LocalTime Close { get; set; }
        public bool IsOpen
        {
            get
            {
                var seconds = Period.Between(Open, Close, PeriodUnits.Seconds).Seconds;
                return seconds > 0;
            }
        }

        public bool IsAlwaysOpen => this == TimeRange.FromWholeDay();

        /// <summary>
        /// When things never close
        /// </summary>
        /// <returns></returns>
        public static TimeRange FromWholeDay()
        {
            return new TimeRange
            {
                Open = new LocalTime(hour: 0, minute: 0),
                Close = new LocalTime(hour: 23, minute: 59, second: 59, millisecond: 999),
            };
        }

        public bool Equals(TimeRange other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Open.Equals(other.Open) && Close.Equals(other.Close);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((TimeRange) obj);
        }

        public override int GetHashCode()
            => HashCode.Combine(Open, Close);

        public static bool operator ==(TimeRange left, TimeRange right)
            => Equals(left, right);

        public static bool operator !=(TimeRange left, TimeRange right)
            => !Equals(left, right);
    }
}