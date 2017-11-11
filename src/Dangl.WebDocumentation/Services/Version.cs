using System;

namespace Dangl.WebDocumentation.Services
{
    public class Version : IComparable<Version>
    {
        private readonly string[] _splitted;

        public Version(string version)
        {
            Value = version;
            _splitted = version.Split('.', '-');
        }

        public string Value { get; }

        public int CompareTo(Version other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;

            for (var i = 0; i < _splitted.Length; i++)
            {
                if (other._splitted.Length <= i)
                {
                    return -1;
                }
                var stringComparison = string.CompareOrdinal(_splitted[i], other._splitted[i]);
                if (stringComparison != 0)
                {
                    return stringComparison;
                }
            }
            return 1;
        }
    }
}
