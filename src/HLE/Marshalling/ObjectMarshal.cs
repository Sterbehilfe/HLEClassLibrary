using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HLE.Marshalling;

public static unsafe class ObjectMarshal
{
    public static uint BaseObjectSize
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (uint)(sizeof(nuint) + sizeof(nuint)); // object header + method table
    }

    [SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields")]
    private static readonly delegate*<object, nint> s_getRawObjectSize = (delegate*<object, nint>)typeof(RuntimeHelpers)
        .GetMethod("GetRawObjectDataSize", BindingFlags.NonPublic | BindingFlags.Static)!
        .MethodHandle
        .GetFunctionPointer();

    private static readonly ConcurrentDictionary<Type, object> s_uninitializedObjectCache = new();
    private static readonly ConcurrentDictionary<Type, bool> s_isReferenceOrContainsReferencesCache = new();

    /// <summary>
    /// Gets the amount of bytes allocated for the instance of the object (physical size).
    /// </summary>
    /// <param name="obj">The object whose instance size will be returned.</param>
    /// <returns>The amount of bytes allocated for the object.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint GetObjectSize(object obj) => BaseObjectSize + (nuint)s_getRawObjectSize(obj);

    [Pure]
    public static nuint GetObjectSize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>()
        where T : class
    {
        if (!s_uninitializedObjectCache.TryGetValue(typeof(T), out object? obj))
        {
            obj = GetUninitializedObject();
        }

        return GetObjectSize(obj);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static object GetUninitializedObject()
        {
            object obj = RuntimeHelpers.GetUninitializedObject(typeof(T));
            s_uninitializedObjectCache.TryAdd(typeof(T), obj);
            return obj;
        }
    }

    [Pure]
    [RequiresDynamicCode(NativeAotMessages.RequiresDynamicCode)]
    public static bool IsReferenceOrContainsReferences(MethodTable* methodTable)
        => IsReferenceOrContainsReferences(Type.GetTypeFromHandle(RuntimeTypeHandle.FromIntPtr((nint)methodTable))!);

    [Pure]
    [RequiresDynamicCode(NativeAotMessages.RequiresDynamicCode)]
    public static bool IsReferenceOrContainsReferences(Type type)
    {
        return s_isReferenceOrContainsReferencesCache.TryGetValue(type, out bool isReferenceOrContainsReferences)
            ? isReferenceOrContainsReferences
            : IsReferenceOrContainsReferencesCore(type);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool IsReferenceOrContainsReferencesCore(Type type)
        {
            MethodInfo method = typeof(RuntimeHelpers)
                .GetMethod(nameof(RuntimeHelpers.IsReferenceOrContainsReferences), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(type);

            bool isReferenceOrContainsReferences = (bool)method.Invoke(null, null)!;
            s_isReferenceOrContainsReferencesCache.TryAdd(type, isReferenceOrContainsReferences);
            return isReferenceOrContainsReferences;
        }
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref TRef GetMethodTableReference<TRef>(object obj) where TRef : allows ref struct
        => ref Unsafe.AsRef<TRef>(*(nuint**)&obj);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref nuint GetMethodTableReference(object obj) => ref Unsafe.AsRef<nuint>(*(nuint**)&obj);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPointer* GetMethodTablePointer<TPointer>(object obj) where TPointer : allows ref struct
        => *(TPointer**)&obj;

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint* GetMethodTablePointer(object obj) => *(nuint**)&obj;

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodTable* GetMethodTable(object obj) => **(MethodTable***)&obj;

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodTable* GetMethodTable<T>() where T : allows ref struct
        => GetMethodTableFromType(typeof(T));

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodTable* GetMethodTableFromType(Type type) => (MethodTable*)type.TypeHandle.Value;

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ReadObject<T>(ref nuint methodTablePointer) where T : class
        => ReadObject<T, nuint>(ref methodTablePointer);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TObject ReadObject<TObject, TRef>(ref TRef methodTableReference) where TObject : class
    {
        void* ptr = Unsafe.AsPointer(ref methodTableReference);
        return *(TObject*)&ptr;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ReadObject<T>(void* methodTablePointer) where T : class
        => ReadObject<T>((nuint)methodTablePointer);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ReadObject<T>(MethodTable** methodTablePointer) where T : class
        => ReadObject<T>((nuint)methodTablePointer);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ReadObject<T>(nuint methodTablePointer) where T : class
        => ReadObject<T>(ref Unsafe.AsRef<nuint>((void*)methodTablePointer));

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetField<T>(object obj, nuint byteOffset)
    {
        ref nuint fieldRef = ref Unsafe.AddByteOffset(ref GetMethodTableReference(obj), (uint)sizeof(nuint) + byteOffset);
        return ref Unsafe.As<nuint, T>(ref fieldRef);
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref RawStringData GetRawStringData(string str) => ref Unsafe.AsRef<RawStringData>(*(RawStringData**)&str);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetString(ref RawStringData data) => ReadObject<string, RawStringData>(ref data);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetString(RawStringData* data) => ReadObject<string>(data);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint GetRawStringSize(string str) => GetRawStringSize(str.Length);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint GetRawStringSize(int stringLength) =>
        BaseObjectSize +
        sizeof(int) /* string length */ +
        (uint)stringLength * sizeof(char) /* chars */ +
        sizeof(char) /* zero-char */;

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref RawArrayData<byte> GetRawArrayData(Array array) => ref Unsafe.AsRef<RawArrayData<byte>>(*(RawArrayData<byte>**)&array);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref RawArrayData<T> GetRawArrayData<T>(T[] array) => ref Unsafe.AsRef<RawArrayData<T>>(*(RawArrayData<T>**)&array);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] GetArray<T>(ref RawArrayData<T> data) => ReadObject<T[], RawArrayData<T>>(ref data);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] GetArray<T>(RawArrayData<T>* data) => ReadObject<T[]>(data);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint GetRawArraySize<T>(T[] array) => GetRawArraySize<T>(array.Length);

    [Pure]
    public static ulong GetRawArraySize(Array array)
    {
        ushort componentSize = GetMethodTable(array)->ComponentSize;
        return BaseObjectSize + (uint)sizeof(nuint) + (ulong)array.LongLength * componentSize;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint GetRawArraySize<T>(int arrayLength) =>
        BaseObjectSize +
        (nuint)sizeof(nuint) /* array length */ +
        (uint)arrayLength * (uint)sizeof(T); /* items */

    /// <summary>
    /// Boxes a struct on the stack, so that no heap allocation occurs.
    /// </summary>
    /// <param name="value">A reference to the struct that should be boxed.</param>
    /// <param name="box">The box.</param>
    /// <typeparam name="T">The type of the boxed value.</typeparam>
    /// <returns>The boxed struct.</returns>
    /// <remarks>
    /// The box should always be discarded, i.e.: <c>object box = ObjectMarshal.BoxOnStack(ref myStruct, out _);</c>.
    /// The parameter is only needed to reserve the stack space for the box.
    /// </remarks>
    [Pure]
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object BoxOnStack<T>(ref T value, out Box<T> box)
    {
        Debug.Assert(typeof(T).IsValueType);

        box = new(ref value);
        return ReadObject<object, Box<T>>(ref box);
    }

    /// <summary>
    /// Unboxes a struct without a type check.
    /// It will not be checked if the type of the boxed struct inside <paramref name="obj"/> matches the type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="obj">The box.</param>
    /// <typeparam name="T">The type of the struct that will be read from the box.</typeparam>
    /// <returns>A reference to the struct in the box.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T Unbox<T>(object obj)
    {
        ref nuint ptr = ref Unsafe.Add(ref GetMethodTableReference(obj), 1);
        return ref Unsafe.As<nuint, T>(ref ptr);
    }
}
