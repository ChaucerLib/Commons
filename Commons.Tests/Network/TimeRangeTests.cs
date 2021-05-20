using System.Collections.Generic;
using Commons.Network;
using NodaTime;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Commons.Tests.Network
{
    public class TimeRangeTests
    {
        [Test, TestCaseSource(nameof(IsOpenTestCases))]
        public bool IsOpenTests(LocalTime open, LocalTime close)
        {
            var range = new TimeRange
            {
                Open = open,
                Close = close,
            };
            return range.IsOpen;
        }

        public static IEnumerable<ITestCaseData> IsOpenTestCases()
        {
            var now = new LocalTime(hour: 6, minute: 0);
            var later = now.PlusHours(1);
            yield return new TestCaseData(now, later)
                .Returns(true)
                .SetName("Non-empty range is open");
            
            yield return new TestCaseData(now, now)
                .Returns(false)
                .SetName("TimeRange of zero is closed");
            
            yield return new TestCaseData(now, now.PlusHours(-1))
                .Returns(false)
                .SetName("Negative time range is closed");
        }

        [Test, TestCaseSource(nameof(IsAlwaysOpenTestsCases))]
        public bool IsAlwaysOpenTests(LocalTime open, LocalTime close)
        {
            var range = new TimeRange
            {
                Open = open,
                Close = close,
            };
            return range.IsAlwaysOpen;
        }
        
        public static IEnumerable<ITestCaseData> IsAlwaysOpenTestsCases()
        {
            var now = new LocalTime(hour: 6, minute: 0);
            var later = now.PlusHours(1);
            yield return new TestCaseData(now, later)
                .Returns(false)
                .SetName("TimeRange of one hour is NOT always open");
            
            var midnight = new LocalTime(hour: 0, minute: 0);
            yield return new TestCaseData(midnight, midnight)
                .Returns(false)
                .SetName("Midnight to midnight is indistinguishable from never open");

            var justBeforeMidnight = midnight.PlusMilliseconds(-1);
            yield return new TestCaseData(midnight, justBeforeMidnight)
                .Returns(true)
                .SetName("Midnight to one millisecond before midnight means always open");

        }
    }
}