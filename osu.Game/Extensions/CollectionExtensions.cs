// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace osu.Game.Extensions
{
    public static class CollectionExtensions
    {
        extension<T>(ICollection<T> collection)
        {
            public void AddRange(IEnumerable<T> items)
            {
                if (collection is List<T> list)
                    list.AddRange(items);
                else
                {
                    foreach (T obj in items)
                        collection.Add(obj);
                }
            }
        }

        extension<T>(List<T> list)
        {
            /// <summary>
            /// A view over the list backing store. Invalid after the list is resized.
            /// </summary>
            public Span<T> AsSpan() => CollectionsMarshal.AsSpan(list);

            /// <summary>
            /// A slice of the list backing store. Invalid after the list is resized.
            /// </summary>
            public Span<T> this[int start, int length] => CollectionsMarshal.AsSpan(list).Slice(start, length);
        }

        extension<TKey>(Dictionary<TKey, int> dictionary)
            where TKey : notnull
        {
            /// <summary>
            /// Adds one to the entry for <paramref name="key"/>, inserting zero first if needed.
            /// </summary>
            public void Increment(TKey key)
            {
                ref int count = ref CollectionsMarshal.GetValueRefOrAddDefault(dictionary, key, out _);
                count++;
            }
        }

        extension<TKey, TValue>(Dictionary<TKey, TValue> dictionary)
            where TKey : notnull
        {
            /// <summary>
            /// A ref to the entry for <paramref name="key"/>, inserting the default value if needed.
            /// Invalid after the dictionary is mutated.
            /// </summary>
            public ref TValue? GetValueRefOrAddDefault(TKey key, out bool exists)
                => ref CollectionsMarshal.GetValueRefOrAddDefault(dictionary, key, out exists);
        }
    }
}
