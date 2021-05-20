using System.Collections.Generic;
using Commons.Contact;
using Commons.People;

namespace Commons.Network
{
    /// <summary>
    /// Represents one or more library branches that make up a single library system. Does NOT represent a consortium of library systems. Typically library fees
    /// are owed at the system level.
    ///
    /// Examples:
    /// - The two library locations in your home town
    /// - A university library system with branches spread throughout the state
    /// </summary>
    public class LibrarySystem
    {
        public string Administrator { get; set; }
        public EmailAddress EmailAddress { get; set; }
        public MailingAddress MailingAddress { get; set; }
        public Branch MainOffice { get; set; }
        public List<Branch> Branches { get; set; }
        public List<EmailAddress> OtherEmailAddresses { get; set; }
        public List<NameValuePair<Person>> OtherPeople { get; set; }
        public string Currency { get; set; }
    }
}