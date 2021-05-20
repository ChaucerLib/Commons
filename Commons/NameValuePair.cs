using System;

namespace Commons
{
    public class NameValuePair<T> where T : IEquatable<T>
    {
        public string Name { get; set; }
        public T Value { get; set; }
    }
}