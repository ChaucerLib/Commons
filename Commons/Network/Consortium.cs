using System.Collections.Generic;
using Commons.Contact;

namespace Commons.Network
{
    /// <summary>
    /// Represents a confederation of library systems, such as when several towns get together and create a large, mutli-system partnership to pool resources,
    /// collections, etc. Any fees accrued by a Patron are typically at the library system level, NOT at the consortium level.
    /// </summary>
    public class Consortium
    {
        public string Administrator { get; set; }
        public EmailAddress EmailAddress { get; set; }
        public MailingAddress MailingAddress { get; set; }
        public List<LibrarySystem> ParticipatingLibraries { get; set; }
    }
}