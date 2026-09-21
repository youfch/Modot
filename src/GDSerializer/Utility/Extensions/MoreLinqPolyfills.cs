using System;
using System.Collections.Generic;

// Declared in System.Linq on purpose: it keeps every call site of these methods unchanged, which is
// exactly why MoreLinq put them here. Being internal, this copy is invisible to Modot, which carries
// its own copy of the methods it uses — so the two assemblies never see two applicable candidates.
// This replaces the Carnagion.MoreLinq dependency: only these five methods were ever used.
namespace System.Linq
{
    /// <summary>
    /// The subset of the former <c>Carnagion.MoreLinq</c> dependency that this project used.
    /// </summary>
    internal static class MoreLinqPolyfills
    {
        /// <summary>
        /// Invokes <paramref name="action"/> on every element of <paramref name="source"/>.
        /// </summary>
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (T item in source)
            {
                action(item);
            }
        }

        /// <summary>
        /// Filters out the <see langword="null"/> elements of <paramref name="source"/>.
        /// </summary>
        public static IEnumerable<T> NotNull<T>(this IEnumerable<T?> source) where T : class
        {
            foreach (T? item in source)
            {
                if (item is not null)
                {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Returns the elements of <paramref name="source"/> that occur more than once, each once, in order of first appearance.
        /// </summary>
        public static IEnumerable<T> Indistinct<T>(this IEnumerable<T> source)
        {
            HashSet<T> seen = new();
            HashSet<T> reported = new();
            foreach (T item in source)
            {
                if (!seen.Add(item) && reported.Add(item))
                {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Returns the index of the first occurrence of <paramref name="value"/> in <paramref name="source"/>, or -1 if it is absent.
        /// </summary>
        public static int IndexOf<T>(this IEnumerable<T> source, T value)
        {
            int index = 0;
            foreach (T item in source)
            {
                if (EqualityComparer<T>.Default.Equals(item, value))
                {
                    return index;
                }
                index += 1;
            }
            return -1;
        }

        /// <summary>
        /// Concatenates the elements of <paramref name="source"/>, using <paramref name="separator"/> between them.
        /// </summary>
        public static string Join<T>(this IEnumerable<T> source, string separator = "")
        {
            return String.Join(separator, source);
        }
    }
}
