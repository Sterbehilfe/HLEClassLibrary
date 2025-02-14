using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using HLE.Collections;
using HLE.Marshalling;
using HLE.Text;

namespace HLE.Memory;

/// <summary>
/// Wraps an <see cref="System.Array"/> rented from an <see cref="ArrayPool{T}"/>
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
    IIndexable<T>,
    IReadOnlyCollection<T>,
    ISpanProvider<T>,
    IReadOnlySpanProvider<T>,
    IMemoryProvider<T>,
    IReadOnlyMemoryProvider<T>,
    ICollectionProvider<T>
{
    public readonly ref T this[int index]
    {
        get
        {
            T[] array = Array;
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)array.Length);
            return ref ArrayMarshal.GetUnsafeElementAt(array, index);
        }
    }

    readonly T IIndexable<T>.this[int index] => this[index];

    readonly T IIndexable<T>.this[Index index] => this[index];

    public readonly ref T this[Index index] => ref this[index.GetOffset(Length)];

    public readonly Span<T> this[Range range] => AsSpan(range);

    public readonly ref T Reference => ref MemoryMarshal.GetArrayDataReference(Array);

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
            T[]? array = _array;
            if (array is null)
            {
                ThrowHelper.ThrowObjectDisposedException<RentedArray<T>>();
            }

            return array;
        }
    }

    public static RentedArray<T> Empty => new();

    internal T[]? _array;
    internal readonly ArrayPool<T> _pool;

    public RentedArray()
    {
        _array = [];
        _pool = ArrayPool<T>.Shared;
    }

    public RentedArray(T[] array, ArrayPool<T> pool)
    {
        _array = array;
        _pool = pool;
    }

    public void Dispose()
    {
        T[]? array = Interlocked.Exchange(ref _array, null);
        if (array is null)
        {
            return;
        }

        _pool.Return(array);
    }

    [Pure]
    public readonly Span<T> AsSpan() => Array;

    [Pure]
    public readonly Span<T> AsSpan(int start) => Array.AsSpan(start);

    [Pure]
    public readonly Span<T> AsSpan(int start, int length) => Array.AsSpan(start, length);

    [Pure]
    public readonly Span<T> AsSpan(Range range) => Array.AsSpan(range);

    readonly ReadOnlySpan<T> IReadOnlySpanProvider<T>.AsSpan() => AsSpan();

    readonly ReadOnlySpan<T> IReadOnlySpanProvider<T>.AsSpan(int start) => AsSpan(start..);

    readonly ReadOnlySpan<T> IReadOnlySpanProvider<T>.AsSpan(int start, int length) => AsSpan(start, length);

    readonly ReadOnlySpan<T> IReadOnlySpanProvider<T>.AsSpan(Range range) => AsSpan(range);

    [Pure]
    public readonly Memory<T> AsMemory() => Array;

    [Pure]
    public readonly Memory<T> AsMemory(int start) => Array.AsMemory(start);

    [Pure]
    public readonly Memory<T> AsMemory(int start, int length) => Array.AsMemory(start, length);

    [Pure]
    public readonly Memory<T> AsMemory(Range range) => Array.AsMemory(range);

    readonly ReadOnlyMemory<T> IReadOnlyMemoryProvider<T>.AsMemory() => AsMemory();

    readonly ReadOnlyMemory<T> IReadOnlyMemoryProvider<T>.AsMemory(int start) => AsMemory(start..);

    readonly ReadOnlyMemory<T> IReadOnlyMemoryProvider<T>.AsMemory(int start, int length) => AsMemory(start, length);

    readonly ReadOnlyMemory<T> IReadOnlyMemoryProvider<T>.AsMemory(Range range) => AsMemory(range);

    [Pure]
    public readonly T[] ToArray()
    {
        T[] array = Array;
        if (array.Length == 0)
        {
            return [];
        }

        T[] result = GC.AllocateUninitializedArray<T>(array.Length);
        SpanHelpers.Copy(array, result);
        return result;
    }

    [Pure]
    public readonly T[] ToArray(int start) => AsSpan().ToArray(start);

    [Pure]
    public readonly T[] ToArray(int start, int length) => AsSpan().ToArray(start, length);

    [Pure]
    public readonly T[] ToArray(Range range) => AsSpan().ToArray(range);

    [Pure]
    public readonly List<T> ToList() => AsSpan().ToList();

    [Pure]
    public readonly List<T> ToList(int start) => AsSpan(start..).ToList();

    [Pure]
    public readonly List<T> ToList(int start, int length) => AsSpan(start, length).ToList();

    [Pure]
    public readonly List<T> ToList(Range range) => AsSpan(range).ToList();

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
    public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is RentedArray<T> other && Equals(other);

    [Pure]
    public readonly bool Equals(RentedArray<T> other) => ReferenceEquals(Array, other.Array) && _pool == other._pool;

    [Pure]
    public override readonly int GetHashCode() => HashCode.Combine(Array, _pool);

    /// <inheritdoc/>
    [Pure]
    public override readonly string ToString()
    {
        if (typeof(T) != typeof(char))
        {
            return ToStringHelpers.FormatCollection(this);
        }

        T[] array = Array;
        return array.Length == 0 ? string.Empty : new(Unsafe.As<char[]>(array));
    }

    public readonly ArrayEnumerator<T> GetEnumerator() => new(Array);

    // ReSharper disable once NotDisposedResourceIsReturned
    readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => Length == 0 ? EmptyEnumeratorCache<T>.Enumerator : GetEnumerator();

    readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static bool operator ==(RentedArray<T> left, RentedArray<T> right) => left.Equals(right);

    public static bool operator !=(RentedArray<T> left, RentedArray<T> right) => !(left == right);
}
