using System;
using NodaTime;

namespace Commons.Borrowing
{
    public class LibraryCard
    {
        /// <summary>
        /// Internal identifier for a physical card
        /// </summary>
        public Guid Id { get; set; }
        
        /// <summary>
        /// The library card "number"
        /// </summary>
        public string Value { get; set; }
        
        public bool Active { get; set; }
        public LocalDate Created { get; set; }
    }
}