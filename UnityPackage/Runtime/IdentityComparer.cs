using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace reromanlee.EventAggregator
{
    /// <summary>
    /// Compares instances by reference, ignoring any <see cref="object.Equals(object)"/> override, so listener
    /// identity cannot be confused by value-equality types or Unity's destroyed-object equality.
    /// </summary>
    /// <typeparam name="T">The reference type to compare.</typeparam>
    internal sealed class IdentityComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly IdentityComparer<T> Instance = new();

        private IdentityComparer()
        {
        }

        public bool Equals(T x, T y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(T obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
