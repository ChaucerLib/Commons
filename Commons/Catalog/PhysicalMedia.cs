namespace Commons.Catalog
{
    public class PhysicalMedia :
        Media
    {
        /// <summary>
        /// To be used by something like an RFID tag
        /// </summary>
        public string Tag { get; set; }
    }
}