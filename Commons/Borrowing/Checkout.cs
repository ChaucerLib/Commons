using System;
using System.Collections.Generic;
using NodaTime;

namespace Commons.Borrowing
{
    public class Checkout
    {
        public Guid Id { get; set; }
        public Guid BorrowedItem { get; set; }
        public Period RenewalInterval { get; set; }
        public LocalDate CheckedOut { get; set; }
        
        /// <summary>
        /// Late fees are calculated based on renewal dates. If you renew when the item is 7 days overdue, the late fee is 7 * daily charge
        /// </summary>
        public List<LocalDate> Renewals { get; set; }
        public LocalDate Returned { get; set; }
    }
}