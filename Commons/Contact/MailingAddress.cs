using System;

namespace Commons.Contact
{
    public class MailingAddress :
        IEquatable<MailingAddress>
    {
        public string Line1 { get; set; }
        public string Line2 { get; set; }
        public string Line3 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }

        public bool Equals(MailingAddress other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Line1 == other.Line1
                && Line2 == other.Line2
                && Line3 == other.Line3
                && City == other.City
                && State == other.State
                && PostalCode == other.PostalCode;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((MailingAddress) obj);
        }

        public override int GetHashCode()
            => HashCode.Combine(Line1, Line2, Line3, City, State, PostalCode);

        public static bool operator ==(MailingAddress left, MailingAddress right)
            => Equals(left, right);

        public static bool operator !=(MailingAddress left, MailingAddress right)
            => !Equals(left, right);
    }
}