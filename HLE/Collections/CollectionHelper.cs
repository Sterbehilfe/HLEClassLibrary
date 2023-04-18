using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using HLE.Memory;

namespace HLE.Collections;

/// <summary>
/// A class to help with any kind of collections.
/// </summary>
public static class CollectionHelper
{
    /// <summary>
    /// Return a random element from the <paramref name="collection"/>.
    /// </summary>
    /// <typeparam name="T">The type of the <paramref name="collection"/>.</typeparam>
    /// <param name="collection">The collection the random element will be taken from.</param>
    /// <returns>A random element or the default type value if the <paramref name="collection"/> doesn't contain any elements.</returns>
    [Pure]
    public static T? Random<T>(this IEnumerable<T> collection)
    {
        return collection switch
        {
            T[] array => Random(array),
            List<T> list => Random(list),
            _ => Random(collection.ToArray())
        };
    }

    [Pure]
    public static ref T? Random<T>(this List<T> list)
    {
        return ref Random(CollectionsMarshal.AsSpan(list));
    }

    [Pure]
    public static ref T? Random<T>(this T[] array)
    {
        return ref Random((ReadOnlySpan<T>)array);
    }

    [Pure]
    public static ref T? Random<T>(this Span<T> span)
    {
        return ref Random((ReadOnlySpan<T>)span);
    }

    [Pure]
    public static ref T? Random<T>(this ReadOnlySpan<T> span)
    {
        int spanLength = span.Length;
        if (spanLength == 0)
        {
            return ref Unsafe.NullRef<T>()!;
        }

        ref T firstItem = ref MemoryMarshal.GetReference(span);
        int randomIdx = HLE.Random.Int(0, spanLength - 1);
        return ref Unsafe.Add(ref firstItem, randomIdx)!;
    }

    /// <summary>
    /// Concatenates every element of the <paramref name="collection"/> separated by the <paramref name="separator"/>.
    /// </summary>
    /// <param name="collection">The <see cref="string"/> enumerable that will be converted to a <see cref="string"/>.</param>
    /// <param name="separator">The separator <see cref="char"/>.</param>
    /// <returns>Returns the <paramref name="collection"/> as a <see cref="string"/>.</returns>
    [Pure]
    public static string JoinToString(this IEnumerable<string> collection, char separator)
    {
        return string.Join(separator, collection);
    }

    [Pure]
    public static string JoinToString(this IEnumerable<string> collection, string separator)
    {
        return string.Join(separator, collection);
    }

    [Pure]
    public static string JoinToString(this IEnumerable<char> collection, char separator)
    {
        return string.Join(separator, collection);
    }

    [Pure]
    public static string JoinToString(this IEnumerable<char> collection, string separator)
    {
        return string.Join(separator, collection);
    }

    [Pure]
    public static string ConcatToString(this IEnumerable<char> collection)
    {
        return string.Concat(collection);
    }

    [Pure]
    public static string ConcatToString(this IEnumerable<string> collection)
    {
        return string.Concat(collection);
    }

    [Pure]
    public static IEnumerable<T> Replace<T>(this IEnumerable<T> collection, Func<T, bool> condition, T replacement)
    {
        foreach (T item in collection)
        {
            yield return condition(item) ? replacement : item;
        }
    }

    [Pure]
    public static List<T> Replace<T>(this List<T> list, Func<T, bool> condition, T replacement)
    {
        List<T> copy = new(list);
        Replace(CollectionsMarshal.AsSpan(copy), condition, replacement);
        return copy;
    }

    [Pure]
    public static T[] Replace<T>(this T[] array, Func<T, bool> condition, T replacement)
    {
        T[] copy = new T[array.Length];
        Array.Copy(array, copy, array.Length);
        Replace((Span<T>)copy, condition, replacement);
        return copy;
    }

    public static void Replace<T>(this Span<T> span, Func<T, bool> condition, T replacement)
    {
        int spanLength = span.Length;
        if (spanLength == 0)
        {
            return;
        }

        ref T firstItem = ref MemoryMarshal.GetReference(span);
        for (int i = 0; i < spanLength; i++)
        {
            ref T item = ref Unsafe.Add(ref firstItem, i);
            if (condition(item))
            {
                item = replacement;
            }
        }
    }

    [Pure]
    public static unsafe List<T> Replace<T>(this List<T> list, delegate*<T, bool> condition, T replacement)
    {
        List<T> copy = new(list);
        Replace(CollectionsMarshal.AsSpan(copy), condition, replacement);
        return copy;
    }

    [Pure]
    public static unsafe T[] Replace<T>(this T[] array, delegate*<T, bool> condition, T replacement)
    {
        T[] copy = new T[array.Length];
        Array.Copy(array, copy, array.Length);
        Replace((Span<T>)copy, condition, replacement);
        return copy;
    }

    public static unsafe void Replace<T>(this Span<T> span, delegate*<T, bool> condition, T replacement)
    {
        int spanLength = span.Length;
        if (spanLength == 0)
        {
            return;
        }

        ref T firstItem = ref MemoryMarshal.GetReference(span);
        for (int i = 0; i < spanLength; i++)
        {
            ref T item = ref Unsafe.Add(ref firstItem, i);
            if (condition(item))
            {
                item = replacement;
            }
        }
    }

    [Pure]
    public static T[][] Split<T>(this IEnumerable<T> collection, T separator)
    {
        return Split(collection.ToArray(), separator);
    }

    [Pure]
    public static T[][] Split<T>(this List<T> list, T separator)
    {
        return Split(CollectionsMarshal.AsSpan(list), separator);
    }

    [Pure]
    public static T[][] Split<T>(this T[] array, T separator)
    {
        return Split((ReadOnlySpan<T>)array, separator);
    }

    [Pure]
    public static T[][] Split<T>(this Span<T> span, T separator)
    {
        return Split((ReadOnlySpan<T>)span, separator);
    }

    [Pure]
    public static T[][] Split<T>(this ReadOnlySpan<T> span, T separator)
    {
        if (span.Length == 0)
        {
            return Array.Empty<T[]>();
        }

        Span<int> indices = MemoryHelper.UseStackAlloc<int>(span.Length) ? stackalloc int[span.Length] : new int[span.Length];
        int indicesLength = IndicesOf(span, separator, indices);
        if (indicesLength == 0)
        {
            return new[]
            {
                span.ToArray()
            };
        }

        T[][] result = new T[indicesLength + 1][];
        ref T[] firstResultValue = ref MemoryMarshal.GetArrayDataReference(result);
        int resultLength = 0;
        int start = 0;
        ref int firstIndex = ref MemoryMarshal.GetReference(indices);
        for (int i = 0; i < indicesLength; i++)
        {
            int index = Unsafe.Add(ref firstIndex, i);
            ReadOnlySpan<T> split = span[start..index];
            start = index + 1;
            if (split.Length > 0)
            {
                Unsafe.Add(ref firstResultValue, resultLength++) = split.ToArray();
            }
        }

        ReadOnlySpan<T> end = span[(indices[indicesLength - 1] + 1)..];
        if (end.Length > 0)
        {
            Unsafe.Add(ref firstResultValue, resultLength) = end.ToArray();
        }

        return result[..resultLength];
    }

    [Pure]
    public static string RandomString(this IEnumerable<char> collection, int wordLength)
    {
        return RandomString(collection.ToArray(), wordLength);
    }

    [Pure]
    public static string RandomString(this List<char> list, int wordLength)
    {
        return RandomString(CollectionsMarshal.AsSpan(list), wordLength);
    }

    [Pure]
    public static string RandomString(this char[] array, int wordLength)
    {
        return RandomString((ReadOnlySpan<char>)array, wordLength);
    }

    [Pure]
    public static string RandomString(this Span<char> span, int wordLength)
    {
        return RandomString((ReadOnlySpan<char>)span, wordLength);
    }

    public static void RandomString(this Span<char> span, Span<char> randomString)
    {
        RandomString((ReadOnlySpan<char>)span, randomString);
    }

    [Pure]
    public static string RandomString(this ReadOnlySpan<char> span, int wordLength)
    {
        if (!MemoryHelper.UseStackAlloc<char>(wordLength))
        {
            using RentedArray<char> resultArray = ArrayPool<char>.Shared.Rent(wordLength);
            RandomString(span, resultArray);
            return new(resultArray[..wordLength]);
        }

        Span<char> result = stackalloc char[wordLength];
        RandomString(span, result);
        return new(result);
    }

    public static void RandomString(this ReadOnlySpan<char> span, Span<char> randomString)
    {
        int randomStringLength = randomString.Length;
        ref char firstChar = ref MemoryMarshal.GetReference(randomString);
        for (int i = 0; i < randomStringLength; i++)
        {
            Unsafe.Add(ref firstChar, i) = span.Random();
        }
    }

    [Pure]
    public static int[] IndicesOf<T>(this IEnumerable<T> collection, Func<T, bool> condition)
    {
        using PoolBufferList<int> indices = new(50, 25);
        int index = 0;
        foreach (T item in collection)
        {
            if (condition(item))
            {
                indices.Add(index);
            }

            index++;
        }

        return indices.ToArray();
    }

    [Pure]
    public static int[] IndicesOf<T>(this List<T> list, Func<T, bool> condition)
    {
        return IndicesOf(CollectionsMarshal.AsSpan(list), condition);
    }

    [Pure]
    public static int[] IndicesOf<T>(this T[] array, Func<T, bool> condition)
    {
        return IndicesOf((ReadOnlySpan<T>)array, condition);
    }

    [Pure]
    public static int[] IndicesOf<T>(this Span<T> span, Func<T, bool> condition)
    {
        return IndicesOf((ReadOnlySpan<T>)span, condition);
    }

    public static int IndicesOf<T>(this Span<T> span, Func<T, bool> condition, Span<int> indices)
    {
        return IndicesOf((ReadOnlySpan<T>)span, condition, indices);
    }

    [Pure]
    public static int[] IndicesOf<T>(this ReadOnlySpan<T> span, Func<T, bool> condition)
    {
        Span<int> indices = MemoryHelper.UseStackAlloc<int>(span.Length) ? stackalloc int[span.Length] : new int[span.Length];
        int length = IndicesOf(span, condition, indices);
        return indices[..length].ToArray();
    }

    public static int IndicesOf<T>(this ReadOnlySpan<T> span, Func<T, bool> condition, Span<int> indices)
    {
        int length = 0;
        int spanLength = span.Length;
        if (spanLength == 0)
        {
            return 0;
        }

        ref T firstItem = ref MemoryMarshal.GetReference(span);
        for (int i = 0; i < spanLength; i++)
        {
            if (condition(Unsafe.Add(ref firstItem, i)))
            {
                indices[length++] = i;
            }
        }

        return length;
    }

    [Pure]
    public static unsafe int[] IndicesOf<T>(this List<T> list, delegate*<T, bool> condition)
    {
        return IndicesOf(CollectionsMarshal.AsSpan(list), condition);
    }

    [Pure]
    public static unsafe int[] IndicesOf<T>(this T[] array, delegate*<T, bool> condition)
    {
        return IndicesOf((ReadOnlySpan<T>)array, condition);
    }

    [Pure]
    public static unsafe int[] IndicesOf<T>(this Span<T> span, delegate*<T, bool> condition)
    {
        return IndicesOf((ReadOnlySpan<T>)span, condition);
    }

    public static unsafe int IndicesOf<T>(this Span<T> span, delegate*<T, bool> condition, Span<int> indices)
    {
        return IndicesOf((ReadOnlySpan<T>)span, condition, indices);
    }

    [Pure]
    public static unsafe int[] IndicesOf<T>(this ReadOnlySpan<T> span, delegate*<T, bool> condition)
    {
        Span<int> indices = MemoryHelper.UseStackAlloc<int>(span.Length) ? stackalloc int[span.Length] : new int[span.Length];
        int length = IndicesOf(span, condition, indices);
        return indices[..length].ToArray();
    }

    public static unsafe int IndicesOf<T>(this ReadOnlySpan<T> span, delegate*<T, bool> condition, Span<int> indices)
    {
        int length = 0;
        int spanLength = span.Length;
        if (spanLength == 0)
        {
            return 0;
        }

        ref T firstItem = ref MemoryMarshal.GetReference(span);
        for (int i = 0; i < spanLength; i++)
        {
            if (condition(Unsafe.Add(ref firstItem, i)))
            {
                indices[length++] = i;
            }
        }

        return length;
    }

    [Pure]
    public static int[] IndicesOf<T>(this IEnumerable<T> collection, T item)
    {
        using PoolBufferList<int> indices = new(50, 25);
        int index = 0;
        foreach (T t in collection)
        {
            if (t?.Equals(item) == true)
            {
                indices.Add(index);
            }

            index++;
        }

        return indices.ToArray();
    }

    [Pure]
    public static int[] IndicesOf<T>(this List<T> list, T item)
    {
        return CollectionsMarshal.AsSpan(list).IndicesOf(item);
    }

    [Pure]
    public static int[] IndicesOf<T>(this T[] array, T item)
    {
        return array.AsSpan().IndicesOf(item);
    }

    [Pure]
    public static int[] IndicesOf<T>(this Span<T> span, T item)
    {
        return IndicesOf((ReadOnlySpan<T>)span, item);
    }

    public static int IndicesOf<T>(this Span<T> span, T item, Span<int> indices)
    {
        return IndicesOf((ReadOnlySpan<T>)span, item, indices);
    }

    [Pure]
    public static int[] IndicesOf<T>(this ReadOnlySpan<T> span, T item)
    {
        Span<int> indices = MemoryHelper.UseStackAlloc<int>(span.Length) ? stackalloc int[span.Length] : new int[span.Length];
        int length = IndicesOf(span, item, indices);
        return indices[..length].ToArray();
    }

    public static int IndicesOf<T>(this ReadOnlySpan<T> span, T item, Span<int> indices)
    {
        int spanLength = span.Length;
        if (spanLength == 0)
        {
            return 0;
        }

        int length = 0;
        ref T firstIndex = ref MemoryMarshal.GetReference(span);
        for (int i = 0; i < spanLength; i++)
        {
            bool equals = Unsafe.Add(ref firstIndex, i)?.Equals(item) == true;
            byte asByte = Unsafe.As<bool, byte>(ref equals);
            indices[length] = i;
            length += asByte;
        }

        return length;
    }

    [Pure]
    public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<(TKey Key, TValue Value)> collection) where TKey : notnull
    {
        return collection.ToDictionary(i => i.Key, i => i.Value);
    }

    [Pure]
    public static T[] Randomize<T>(this IEnumerable<T> collection)
    {
        return Randomize(collection.ToArray());
    }

    [Pure]
    public static List<T> Randomize<T>(this List<T> list)
    {
        List<T> copy = new(list);
        Randomize(CollectionsMarshal.AsSpan(copy));
        return copy;
    }

    [Pure]
    public static T[] Randomize<T>(this T[] array)
    {
        T[] copy = new T[array.Length];
        Array.Copy(array, copy, array.Length);
        Randomize((Span<T>)copy);
        return copy;
    }

    public static void Randomize<T>(this Span<T> span)
    {
        if (span.Length <= 1)
        {
            return;
        }

        int maxIdx = span.Length - 1;
        ref T firstItem = ref MemoryMarshal.GetReference(span);
        for (int i = 0; i < span.Length; i++)
        {
            int randomIdx = HLE.Random.Int(0, maxIdx);
            ref T item = ref Unsafe.Add(ref firstItem, i);
            (item, span[randomIdx]) = (span[randomIdx], item);
        }
    }

    [Pure]
    public static T[] RandomCollection<T>(this IEnumerable<T> collection, int length)
    {
        return RandomCollection(collection.ToArray(), length);
    }

    [Pure]
    public static T[] RandomCollection<T>(this T[] array, int length)
    {
        return RandomCollection((Span<T>)array, length);
    }

    [Pure]
    public static T[] RandomCollection<T>(this List<T> list, int length)
    {
        return RandomCollection(CollectionsMarshal.AsSpan(list), length);
    }

    [Pure]
    public static T[] RandomCollection<T>(this Span<T> span, int length)
    {
        if (span.Length == 0)
        {
            return Array.Empty<T>();
        }

        T[] result = new T[length];
        ref T firstItem = ref MemoryMarshal.GetArrayDataReference(result);
        for (int i = 0; i < length; i++)
        {
            Unsafe.Add(ref firstItem, i) = span.Random()!;
        }

        return result;
    }

    [Pure]
    public static RangeEnumerator GetEnumerator(this Range range) => new(range);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void FillAscending(Span<int> span, int start = 0)
    {
#if NET8_0_OR_GREATER
        int vector512Count = Vector512<int>.Count;
        if (Vector512.IsHardwareAccelerated && span.Length > vector512Count)
        {
            var ascendingValueAdditions = Vector512.Create(0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
                10, 11, 12, 13, 14, 15);
            while (span.Length > vector512Count)
            {
                Vector512<int> startValues = Vector512.Create(start);
                Vector512<int> values = Vector512.Add(startValues, ascendingValueAdditions);
                values.StoreUnsafe(ref MemoryMarshal.GetReference(span));
                start += vector512Count;
                span = span[vector512Count..];
            }

            for (int i = 0; i < span.Length; i++)
            {
                span[i] = start + i;
            }

            return;
        }
#endif

        int vector256Count = Vector256<int>.Count;
        if (Vector256.IsHardwareAccelerated && span.Length > vector256Count)
        {
            Vector256<int> ascendingValueAdditions = Vector256.Create(0, 1, 2, 3, 4, 5, 6, 7);
            while (span.Length > vector256Count)
            {
                Vector256<int> startValues = Vector256.Create(start);
                Vector256<int> values = Vector256.Add(startValues, ascendingValueAdditions);
                values.StoreUnsafe(ref MemoryMarshal.GetReference(span));
                start += vector256Count;
                span = span[vector256Count..];
            }

            for (int i = 0; i < span.Length; i++)
            {
                span[i] = start + i;
            }

            return;
        }

        int vector128Count = Vector128<int>.Count;
        if (Vector128.IsHardwareAccelerated && span.Length > vector128Count)
        {
            Vector128<int> ascendingValueAdditions = Vector128.Create(0, 1, 2, 3);
            while (span.Length > vector128Count)
            {
                Vector128<int> startValues = Vector128.Create(start);
                Vector128<int> values = Vector128.Add(startValues, ascendingValueAdditions);
                values.StoreUnsafe(ref MemoryMarshal.GetReference(span));
                start += vector128Count;
                span = span[vector128Count..];
            }

            for (int i = 0; i < span.Length; i++)
            {
                span[i] = start + i;
            }

            return;
        }

        int vector64Count = Vector64<int>.Count;
        if (Vector64.IsHardwareAccelerated && span.Length > vector64Count)
        {
            Vector64<int> ascendingValueAdditions = Vector64.Create(0, 1);
            while (span.Length > vector64Count)
            {
                Vector64<int> startValues = Vector64.Create(start);
                Vector64<int> values = Vector64.Add(startValues, ascendingValueAdditions);
                values.StoreUnsafe(ref MemoryMarshal.GetReference(span));
                start += vector64Count;
                span = span[vector64Count..];
            }

            for (int i = 0; i < span.Length; i++)
            {
                span[i] = start + i;
            }

            return;
        }

        int spanLength = span.Length;
        ref int firstItem = ref MemoryMarshal.GetReference(span);
        for (int i = 0; i < spanLength; i++)
        {
            Unsafe.Add(ref firstItem, i) = start + i;
        }
    }
}
