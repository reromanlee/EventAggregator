using System;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.EventAggregator.Editor
{
    /// <summary>
    /// Readable, cached display names for trace types: generic types render as <c>Name&lt;Argument&gt;</c> instead
    /// of <c>Name`1</c>, and each name is built once per type. Main thread only.
    /// </summary>
    internal static class TypeNames
    {
        private const string UnknownName = "Unknown";

        private static readonly Dictionary<Type, string> cache = new();

        public static string Get(Type type)
        {
            if (type == null)
            {
                return UnknownName;
            }

            if (!cache.TryGetValue(type, out string name))
            {
                name = Format(type);
                cache.Add(type, name);
            }
            return name;
        }

        private static string Format(Type type)
        {
            if (!type.IsGenericType)
            {
                return type.Name;
            }

            string name = type.Name;
            int aritySeparator = name.IndexOf('`');
            StringBuilder builder = new(aritySeparator >= 0 ? name.Substring(0, aritySeparator) : name);
            builder.Append('<');
            Type[] arguments = type.GetGenericArguments();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(Get(arguments[i]));
            }
            return builder.Append('>').ToString();
        }
    }
}
