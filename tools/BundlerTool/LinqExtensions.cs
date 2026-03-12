using System;
using System.Collections.Generic;

namespace BundlerTool
{
    public static class LinqExtensions
    {
        public static IEnumerable<T[]> Chunk<T>(this IEnumerable<T> source, int size)
        {
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var chunk = new T[size];
                    chunk[0] = enumerator.Current;
                    int count = 1;
                    while (count < size && enumerator.MoveNext())
                    {
                        chunk[count++] = enumerator.Current;
                    }
                    if (count < size) Array.Resize(ref chunk, count);
                    yield return chunk;
                }
            }
        }
    }
}
