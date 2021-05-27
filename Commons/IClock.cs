using System;

namespace Commons
{
    public interface IClock
    {
        DateTime Now();
        DateTime UtcNow();
        DateTimeOffset OffsetNow();
        DateTimeOffset OffsetUtcNow();
    }

    public class Clock :
        IClock
    {
        public DateTime Now() => DateTime.Now;
        public DateTime UtcNow() => DateTime.UtcNow;
        public DateTimeOffset OffsetNow() => DateTimeOffset.Now;
        public DateTimeOffset OffsetUtcNow() => DateTimeOffset.UtcNow;
    }
}