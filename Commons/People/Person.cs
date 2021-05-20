using System;
using Commons.Contact;

namespace Commons.People
{
    public class Person :
        IEquatable<Person>
    {
        public string GivenName { get; set; }
        public string MiddleName { get; set; }
        public string Surname { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string TaxpayerId { get; set; }
        
        public MailingAddress PrimaryAddress { get; set; }
        public NameValuePair<MailingAddress> OtherAddresses { get; set; }

        public bool Equals(Person other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(GivenName, other.GivenName)
                 && string.Equals(MiddleName, other.MiddleName)
                 && string.Equals(Surname, other.Surname)
                 && DateOfBirth.Equals(other.DateOfBirth)
                 && string.Equals(TaxpayerId, other.TaxpayerId)
                 && Equals(PrimaryAddress, other.PrimaryAddress)
                 && Equals(OtherAddresses, other.OtherAddresses);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((Person) obj);
        }

        public override int GetHashCode()
            => HashCode.Combine(GivenName, MiddleName, Surname, DateOfBirth, TaxpayerId, PrimaryAddress, OtherAddresses);

        public static bool operator ==(Person left, Person right)
            => Equals(left, right);

        public static bool operator !=(Person left, Person right)
            => !Equals(left, right);
    }
}