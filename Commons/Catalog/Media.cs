using System;
using System.Collections.Generic;
using NodaTime;

namespace Commons.Catalog
{
    public abstract class Media
    {
        /// <summary>
        /// Usually an ISBN (books), ISAN (movies), etc.
        /// </summary>
        public string UniversalId { get; set; }
        
        /// <summary>
        /// A case-insensitive Id representing a specific instance of media that will typically be represented as a scannable barcode or RFID tag in the case of
        /// a physical object, or corresponds to a license for digital media that a library system is allowed to distribute.
        /// </summary>
        public string InstanceId { get; set; }
        
        public string Title { get; set; }
        public IReadOnlyList<string> Authors { get; set; }
        public LocalDate PublishDate { get; set; }
        public int Edition { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal ReplacementCost { get; set; }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(InstanceId);
        }
    }
}