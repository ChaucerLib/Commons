using System;
using System.Collections.Generic;
using System.Linq;

namespace Commons.People
{
    public class Household
    {
        public Guid Id { get; set; }
        public List<Patron> Members { get; set; }
        public decimal HouseholdFees => Members.Sum(m => m.FeesOwed);
    }
}