using NodaTime;

namespace Commons.Borrowing
{
    public class Payment
    {
        public LocalDate Date { get; set; }
        public decimal Paid { get; set; }
    }
}