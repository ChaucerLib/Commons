using System;
using System.Net.Mail;

namespace Commons.Contact
{
    public class EmailAddress : IEquatable<EmailAddress>
    {
        public string Description { get; set; }
        public MailAddress Email { get; set; }

        public bool Equals(EmailAddress other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Description == other.Description && Equals(Email, other.Email);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((EmailAddress) obj);
        }

        public override int GetHashCode()
            => HashCode.Combine(Description, Email);

        public static bool operator ==(EmailAddress left, EmailAddress right)
            => Equals(left, right);

        public static bool operator !=(EmailAddress left, EmailAddress right)
            => !Equals(left, right);
    }
}