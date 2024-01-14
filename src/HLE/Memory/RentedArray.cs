using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HLE.Collections;
using HLE.Strings;
using JetBrains.Annotations;
using PureAttribute = System.Diagnostics.Contracts.PureAttribute;

namespace HLE.Memory;

/// <summary>
/// Wraps an <see cref="Array"/> rented from an <see cref="ArrayPool{T}"/>
/// to allow declaration with a <see langword="using"/> statement and to remove the need of nesting in a <see langword="try"/>-<see langword="finally"/> block.
/// </summary>
/// <typeparam name="T">The type the rented array contains.</typeparam>
// ReSharper disable once UseNameofExpressionForPartOfTheString
[DebuggerDisplay("Length = {Length}")]
public struct RentedArray<T> :
    IDisposable,
    ICollection<T>,
    ICopyable<T>,
    IEquatable<RentedArray<T>>,
    IEquatable<T[]>,
    IIndexAccessible<T>,
    IReadOnlyCollection<T>,
    ISpanProvider<T>,
    ICollectionProvider<T>,
    IMemoryProvider<T>
{
    public readonly ref T this[int index]
    {
        get
        {
            T[] array = Array;
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)array.Length);
            return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index);
        }
    }

    readonly T IIndexAccessible<T>.this[int index] => this[index];

    public readonly ref T this[Index index] => ref this[index.GetOffset(Length)];

    public readonly Span<T> this[Range range] => AsSpan(range);

    public readonly ref T Reference => ref MemoryMarshal.GetArrayDataReference(Array);

    internal readonly ref T UncheckedReference => ref MemoryMarshal.GetArrayDataReference(_array!);

    public readonly int Length => Array.Length;

    public readonly bool IsDisposed => _array is null;

    readonly int ICountable.Count => Length;

    readonly int ICollection<T>.Count => Length;

    readonly int IReadOnlyCollection<T>.Count => Length;

    readonly bool ICollection<T>.IsReadOnly => false;

    [SuppressMessage("Performance", "CA1819:Properties should not return arrays")]
    internal readonly T[] Array
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_array is null)
            {
                ThrowHelper.ThrowObjectDisposedException<RentedArray<T>>();
            }

            return _array;
        }
    }

    public static RentedArray<T> Empty => new();

    internal T[]? _array = [];
    internal readonly ArrayPool<T> _pool = ArrayPool<T>.Shared;

    public RentedArray()
    {
    }

    [MustDisposeResource]
    public RentedArray(T[] array, ArrayPool<T> pool)
    {
        _array = array;
        _pool = pool;
    }

    public void Dispose()
    {
        _pool.Return(_array);
        _array = null;
    }

    [Pure]
    public readonly Span<T> AsSpan() => Array;

    [Pure]
    public readonly Span<T> AsSpan(int start) => Array.AsSpan(start);

    [Pure]
    public readonly Span<T> AsSpan(int start, int length) => Array.AsSpan(start, length);

    [Pure]
    public readonly Span<T> AsSpan(Range range) => Array.AsSpan(range);

    [Pure]
    public readonly Memory<T> AsMemory() => Array;

    [Pure]
    public readonly Memory<T> AsMemory(int start) => Array.AsMemory(start);

    [Pure]
    public readonly Memory<T> AsMemory(int start, int length) => Array.AsMemory(start, length);

    [Pure]
    public readonly Memory<T> AsMemory(Range range) => Array.AsMemory(range);

    readonly Span<T> ISpanProvider<T>.GetSpan() => AsSpan();

    readonly ReadOnlySpan<T> IReadOnlySpanProvider<T>.GetReadOnlySpan() => AsSpan();

    readonly Memory<T> IMemoryProvider<T>.GetMemory() => AsMemory();

    readonly ReadOnlyMemory<T> IReadOnlyMemoryProvider<T>.GetReadOnlyMemory() => AsMemory();

    [Pure]
    public readonly T[] ToArray()
    {
        T[] array = Array;
        if (array.Length == 0)
        {
            return [];
        }

        T[] result = GC.AllocateUninitializedArray<T>(array.Length);
        CopyWorker<T>.Copy(array, result);
        return result;
    }

    [Pure]
    public readonly T[] ToArray(int start) => AsSpan().ToArray(start);

    [Pure]
    public readonly T[] ToArray(int start, int length) => AsSpan().ToArray(start, length);

    [Pure]
    public readonly T[] ToArray(Range range) => AsSpan().ToArray(range);

    [Pure]
    public readonly List<T> ToList()
    {
        T[] array = Array;
        if (array.Length == 0)
        {
            return [];
        }

        List<T> result = new(array.Length);
        CopyWorker<T> copyWorker = new(array);
        copyWorker.CopyTo(result);
        return result;
    }

    public readonly void CopyTo(List<T> destination, int offset = 0)
    {
        CopyWorker<T> copyWorker = new(Array);
        copyWorker.CopyTo(destination, offset);
    }

    public readonly void CopyTo(T[] destination, int offset = 0)
    {
        CopyWorker<T> copyWorker = new(Array);
        copyWorker.CopyTo(destination, offset);
    }

    public readonly void CopyTo(Memory<T> destination)
    {
        CopyWorker<T> copyWorker = new(Array);
        copyWorker.CopyTo(destination);
    }

    public readonly void CopyTo(Span<T> destination)
    {
        CopyWorker<T> copyWorker = new(Array);
        copyWorker.CopyTo(destination);
    }

    public readonly void CopyTo(ref T destination)
    {
        CopyWorker<T> copyWorker = new(Array);
        copyWorker.CopyTo(ref destination);
    }

    public readonly unsafe void CopyTo(T* destination)
    {
        CopyWorker<T> copyWorker = new(Array);
        copyWorker.CopyTo(destination);
    }

    readonly void ICollection<T>.Add(T item) => throw new NotSupportedException();

    public readonly void Clear() => System.Array.Clear(Array);

    readonly bool ICollection<T>.Contains(T item) => Array.Contains(item);

    readonly bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

    [Pure]
    public override readonly bool Equals([NotNullWhen(true)] object? obj) =>
        obj switch
        {
            RentedArray<T> rentedArray => Equals(rentedArray.Array),
            T[] array => Equals(array),
            _ => false
        };

    [Pure]
    public readonly bool Equals(RentedArray<T> other) => ReferenceEquals(Array, other.Array);

    [Pure]
    public readonly bool Equals([NotNullWhen(true)] T[]? other) => ReferenceEquals(Array, other);

    [Pure]
    public override readonly int GetHashCode() => Array.GetHashCode();

    /// <inheritdoc/>
    [Pure]
    public override readonly string ToString()
    {
        if (typeof(T) != typeof(char))
        {
            return ToStringHelpers.FormatCollection(this);
        }

        T[] array = Array;
        ReadOnlySpan<char> chars = Unsafe.As<T[], char[]>(ref array);
        return new(chars);
    }

    public readonly ArrayEnumerator<T> GetEnumerator() => new(Array, 0, Array.Length);

    readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static bool operator ==(RentedArray<T> left, RentedArray<T> right) => left.Equals(right);

    public static bool operator !=(RentedArray<T> left, RentedArray<T> right) => !(left == right);
}
