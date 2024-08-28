using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HLE.Collections;
using HLE.Memory;

namespace HLE;

public readonly partial struct EnvironmentVariables :
    IReadOnlyDictionary<string, string>,
    IIndexable<string>,
    ICollection<EnvironmentVariable>,
    ICopyable<EnvironmentVariable>,
    IEquatable<EnvironmentVariables>
{
    // ReSharper disable once CanSimplifyDictionaryTryGetValueWithGetValueOrDefault
    public string? this[string name] => _environmentVariables.TryGetValue(name, out string? value) ? value : null;

    string IReadOnlyDictionary<string, string>.this[string key] => _environmentVariables[key];

    public ImmutableArray<string> Names => _environmentVariables.Keys;

    public ImmutableArray<string> Values => _environmentVariables.Values;

    IEnumerable<string> IReadOnlyDictionary<string, string>.Keys => Names;

    IEnumerable<string> IReadOnlyDictionary<string, string>.Values => Values;

    string IIndexable<string>.this[int index] => ImmutableCollectionsMarshal.AsArray(_environmentVariables.Values)![index];

    public int Count => _environmentVariables.Count;

    public bool IsReadOnly => true;

    private readonly FrozenDictionary<string, string> _environmentVariables = FrozenDictionary<string, string>.Empty;

    public static EnvironmentVariables Empty => new();

    public EnvironmentVariables()
    {
    }

    public EnvironmentVariables(FrozenDictionary<string, string> environmentVariables) => _environmentVariables = environmentVariables;

    public static EnvironmentVariables Create() => OperatingSystemEnvironmentVariableProvider.GetEnvironmentVariables();

    void ICollection<EnvironmentVariable>.Add(EnvironmentVariable item) => throw new NotSupportedException();

    void ICollection<EnvironmentVariable>.Clear() => throw new NotSupportedException();

    bool ICollection<EnvironmentVariable>.Contains(EnvironmentVariable item)
        => _environmentVariables.TryGetValue(item.Name, out string? value) && item.Value == value;

    bool ICollection<EnvironmentVariable>.Remove(EnvironmentVariable item) => throw new NotSupportedException();

    void ICollection<EnvironmentVariable>.CopyTo(EnvironmentVariable[] array, int arrayIndex) => throw new NotSupportedException();

    public void CopyTo(List<EnvironmentVariable> destination, int offset = 0)
    {
        // TODO: not optimal copying into a temporary buffer and then into the list
        using RentedArray<EnvironmentVariable> buffer = ArrayPool<EnvironmentVariable>.Shared.RentAsRentedArray(Count);
        CopyTo(buffer.AsSpan());
        CopyWorker<EnvironmentVariable> copyWorker = new(buffer[..Count]);
        copyWorker.CopyTo(destination, offset);
    }

    public void CopyTo(EnvironmentVariable[] destination, int offset = 0) => CopyTo(ref MemoryMarshal.GetReference(destination.AsSpan(offset)));

    public void CopyTo(Memory<EnvironmentVariable> destination) => CopyTo(ref MemoryMarshal.GetReference(destination.Span));

    public void CopyTo(Span<EnvironmentVariable> destination) => CopyTo(ref MemoryMarshal.GetReference(destination));

    public void CopyTo(ref EnvironmentVariable destination)
    {
        int writeIndex = 0;
        foreach (KeyValuePair<string, string> pair in _environmentVariables)
        {
            KeyValuePair<string, string> p = pair;
            EnvironmentVariable variable = Unsafe.As<KeyValuePair<string, string>, EnvironmentVariable>(ref p);
            Debug.Assert(p.Key == variable.Name && p.Value == variable.Value);
            Unsafe.Add(ref destination, writeIndex++) = variable;
        }
    }

    public unsafe void CopyTo(EnvironmentVariable* destination) => CopyTo(ref Unsafe.AsRef<EnvironmentVariable>(destination));

    [Pure]
    public EnvironmentVariable[] ToArray()
    {
        EnvironmentVariable[] result = new EnvironmentVariable[Count];
        CopyTo(result);
        return result;
    }

    [Pure]
    public List<EnvironmentVariable> ToList()
    {
        List<EnvironmentVariable> result = new(Count);
        CopyTo(result);
        return result;
    }

    bool IReadOnlyDictionary<string, string>.ContainsKey(string key) => _environmentVariables.ContainsKey(key);

    bool IReadOnlyDictionary<string, string>.TryGetValue(string key, [MaybeNullWhen(false)] out string value)
        => _environmentVariables.TryGetValue(key, out value);

    public Enumerator GetEnumerator() => new(this);

    IEnumerator<EnvironmentVariable> IEnumerable<EnvironmentVariable>.GetEnumerator() => GetEnumerator();

    IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator() => _environmentVariables.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    [Pure]
    public bool Equals(EnvironmentVariables other) => ReferenceEquals(_environmentVariables, other._environmentVariables);

    [Pure]
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is EnvironmentVariables other && Equals(other);

    [Pure]
    public override int GetHashCode() => _environmentVariables.GetHashCode();

    public static bool operator ==(EnvironmentVariables left, EnvironmentVariables right) => left.Equals(right);

    public static bool operator !=(EnvironmentVariables left, EnvironmentVariables right) => !(left == right);
}
