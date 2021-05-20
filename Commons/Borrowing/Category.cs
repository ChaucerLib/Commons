using System;

namespace Commons.Borrowing
{
    /// <summary>
    /// Intended to represent borrowing categories. Examples:
    /// - Newly released books may be borrowed for 2 weeks, not be renewed, and have a 50 cent/day late fee
    /// - DVDs may be borrowed for a week, and renewed once, and have a $1/day late fee
    /// </summary>
    public class Category
    {
        public Guid Id { get; set; }
        
        public string Name { get; set; }
        
        /// <summary>
        /// A description of the category
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Daily late fee. This should be expressed as a negative number.
        /// </summary>
        public decimal LateFee { get; set; }
        
        /// <summary>
        /// The number of days an item may be borrowed for
        /// </summary>
        public int BorrowingDays { get; set; }
        
        /// <summary>
        /// The number of times an item may be renewed
        /// </summary>
        public int RenewalLimit { get; set; }
    }
}