using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using HLE.Collections;

namespace HLE.Memory;

public sealed unsafe class NativeMemoryManager<T>(T* memory, int length) : MemoryManager<T>, IEquatable<NativeMemoryManager<T>>, ISpanProvider<T>
    where T : unmanaged, IEquatable<T>
{
    private readonly T* _memory = memory;
    private readonly int _length = length;

    public NativeMemoryManager(NativeMemory<T> memory) : this(memory.Pointer, memory.Length)
    {
    }

    [Pure]
    public override Span<T> GetSpan() => new(_memory, _length);

    /// <summary>
    /// Throws a <see cref="NotSupportedException"/>.<br/>
    /// The <see cref="NativeMemoryManager{T}"/> manages native memory, thus does not require nor support pinning."
    /// </summary>
    [DoesNotReturn]
    public override MemoryHandle Pin(int elementIndex = 0)
    {
        ThrowNativeMemoryRequiresNoPinning();
        return default;
    }

    /// <summary>
    /// Throws a <see cref="NotSupportedException"/>.<br/>
    /// The <see cref="NativeMemoryManager{T}"/> manages native memory, thus does not require nor support pinning."
    /// </summary>
    [DoesNotReturn]
    public override void Unpin() => ThrowNativeMemoryRequiresNoPinning();

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNativeMemoryRequiresNoPinning()
        => throw new NotSupportedException($"The {typeof(NativeMemoryManager<T>)} manages native memory, thus does not require nor support pinning.");

    protected override void Dispose(bool disposing)
    {
    }

    public bool Equals([NotNullWhen(true)] NativeMemoryManager<T>? other) => ReferenceEquals(this, other);

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is NativeMemoryManager<T> other && Equals(other);

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

    public static bool operator ==(NativeMemoryManager<T>? left, NativeMemoryManager<T>? right) => Equals(left, right);

    public static bool operator !=(NativeMemoryManager<T>? left, NativeMemoryManager<T>? right) => !(left == right);
}
