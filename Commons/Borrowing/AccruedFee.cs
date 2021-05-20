using System;
using System.Collections.Generic;
using System.Linq;
using NodaTime;

namespace Commons.Borrowing
{
    public class AccruedFee
    {
        public Guid Patron { get; set; }
        public Guid BorrowedItem { get; set; }
        public LocalDate FirstIncurred { get; set; }
        public FeeType FeeType { get; set; }
        
        /// <summary>
        /// The fee rate by day, replacement copy, etc.
        /// </summary>
        public decimal FeeAccrual { get; set; }
        
        /// <summary>
        /// The multiplier for the FeeAccrual to calculate the subtotal
        /// 
        /// Usage:
        /// - 10 days overdue
        /// - 1 replacement copy
        /// </summary>
        public int Quantity { get; set; }
        
        /// <summary>
        /// Payment value will add up to a maximum of the assessed fee
        /// </summary>
        public List<Payment> AppliedPayments { get; set; }

        public decimal OriginalFee => Quantity * FeeAccrual;
        public decimal TotalPayments => AppliedPayments.Sum(p => p.Paid);
        public decimal RemainingFee => OriginalFee - TotalPayments;
        public bool IsPaid => RemainingFee == decimal.Zero;
    }
}