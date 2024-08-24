using System.Diagnostics;
using System.Runtime.InteropServices;

using U8.Primitives;

namespace U8;

static class UnsafeExtensions
{
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T AsRef<T>(this Span<T> value)
        where T : struct
    {
        return ref MemoryMarshal.GetReference(value);
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T AsRef<T>(this ReadOnlySpan<T> value)
        where T : struct
    {
        return ref MemoryMarshal.GetReference(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T AsRef<T>(this ReadOnlySpan<T> value, int offset)
        where T : struct
    {
        Debug.Assert((uint)offset < (uint)value.Length);
        return ref Unsafe.Add(ref MemoryMarshal.GetReference(value), (nint)(uint)offset);
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T AsRef<T>(this T[] value)
        where T : struct
    {
        Debug.Assert(value != null);
        return ref MemoryMarshal.GetArrayDataReference(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T AsRef<T>(this T[] value, int offset)
        where T : struct
    {
        Debug.Assert(value != null);
        Debug.Assert((uint)offset < (uint)value.Length);
        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(value), (nint)(uint)offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T AsRef<T>(this T[] value, nint offset)
        where T : struct
    {
        Debug.Assert(value != null);
        Debug.Assert((uint)offset < (uint)value.Length);
        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(value), offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T AsRef<T>(this Span<T> value, int offset)
        where T : struct
    {
        Debug.Assert((uint)offset < (uint)value.Length);
        return ref Unsafe.Add(ref MemoryMarshal.GetReference(value), (nint)(uint)offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<byte> AsSpan(this ref byte value, int length)
    {
        Debug.Assert(Unsafe.IsNullRef(ref value) || (uint)length >= 0);
        return MemoryMarshal.CreateReadOnlySpan(ref value, length);
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T Add<T>(this ref T value, int offset)
        where T : struct
    {
        Debug.Assert(!Unsafe.IsNullRef(ref value));
        Debug.Assert(offset >= 0);
        return ref Unsafe.Add(ref value, (nint)(uint)offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T Add<T>(this ref T value, nuint offset)
        where T : struct
    {
        Debug.Assert(!Unsafe.IsNullRef(ref value));
        return ref Unsafe.Add(ref value, offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T Substract<T>(this ref T value, int offset)
        where T : struct
    {
        Debug.Assert(!Unsafe.IsNullRef(ref value));
        Debug.Assert(offset >= 0);
        return ref Unsafe.Subtract(ref value, (nint)(uint)offset);
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref U Cast<T, U>(this ref T value)
        where T : unmanaged
        where U : unmanaged
    {
        Debug.Assert(!Unsafe.IsNullRef(ref value));
        return ref Unsafe.As<T, U>(ref value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<U> Cast<T, U>(this ReadOnlySpan<T> value)
    {
        Debug.Assert(Unsafe.SizeOf<T>() == Unsafe.SizeOf<U>());
        Debug.Assert(
            RuntimeHelpers.IsReferenceOrContainsReferences<T>() ==
            RuntimeHelpers.IsReferenceOrContainsReferences<U>());

        return MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.As<T, U>(ref MemoryMarshal.GetReference(value)), value.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IEnumerable<U> Cast<T, U>(this IEnumerable<T> value)
    {
        // Safe - restrict to specializing for now
        Debug.Assert(typeof(T) == typeof(U));

        return Unsafe.As<IEnumerable<U>>(value);
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CopyToUnsafe(this ReadOnlySpan<byte> source, ref byte destination)
    {
        Debug.Assert(!Unsafe.IsNullRef(ref destination));
        source.CopyTo(MemoryMarshal.CreateSpan(ref destination, source.Length));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CopyToUnsafe(this Span<byte> source, ref byte destination)
    {
        Debug.Assert(!Unsafe.IsNullRef(ref destination));
        source.CopyTo(MemoryMarshal.CreateSpan(ref destination, source.Length));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool GreaterThan<T>(this ref T left, ref T right)
        where T : unmanaged
    {
        Debug.Assert(!Unsafe.IsNullRef(ref left));
        Debug.Assert(!Unsafe.IsNullRef(ref right));
        return Unsafe.IsAddressGreaterThan(ref left, ref right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool LessThan<T>(this ref T left, ref T right)
        where T : unmanaged
    {
        Debug.Assert(!Unsafe.IsNullRef(ref left));
        Debug.Assert(!Unsafe.IsNullRef(ref right));
        return Unsafe.IsAddressLessThan(ref left, ref right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool LessThanOrEqual<T>(this ref T left, ref T right)
        where T : unmanaged
    {
        Debug.Assert(!Unsafe.IsNullRef(ref left));
        Debug.Assert(!Unsafe.IsNullRef(ref right));
        return !Unsafe.IsAddressGreaterThan(ref left, ref right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Span<T> SliceUnsafe<T>(this T[] value, int start)
        where T : struct
    {
        Debug.Assert(value != null);
        Debug.Assert((uint)start <= (uint)value.Length);

        return MemoryMarshal.CreateSpan(
            ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(value), (nint)(uint)start),
            value.Length - start);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Span<T> SliceUnsafe<T>(this T[] value, int offset, int length)
        where T : struct
    {
        Debug.Assert(value != null);
        Debug.Assert((uint)offset + (uint)length <= (uint)value.Length);

        return MemoryMarshal.CreateSpan(
            ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(value), (nint)(uint)offset), length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Span<byte> SliceUnsafe(this byte[] value, U8Range range)
    {
        Debug.Assert(value != null);
        Debug.Assert((uint)range.Offset + (uint)range.Length <= (uint)value.Length);

        return MemoryMarshal.CreateSpan(
            ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(value), (nint)(uint)range.Offset),
            range.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Span<T> SliceUnsafe<T>(this Span<T> value, int start)
        where T : struct
    {
        Debug.Assert((uint)start <= (uint)value.Length);

        return MemoryMarshal.CreateSpan(
            ref Unsafe.Add(ref MemoryMarshal.GetReference(value), (nint)(uint)start),
            value.Length - start);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Span<T> SliceUnsafe<T>(this Span<T> value, int offset, int length)
        where T : struct
    {
        Debug.Assert((uint)offset + (uint)length <= (uint)value.Length);

        return MemoryMarshal.CreateSpan(
            ref Unsafe.Add(ref MemoryMarshal.GetReference(value), (nint)(uint)offset), length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<T> SliceUnsafe<T>(this ReadOnlySpan<T> value, int start)
        where T : struct
    {
        Debug.Assert((uint)start <= (uint)value.Length);

        return MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.Add(ref MemoryMarshal.GetReference(value), (nint)(uint)start),
            value.Length - start);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<T> SliceUnsafe<T>(this ReadOnlySpan<T> value, int offset, int length)
        where T : struct
    {
        Debug.Assert((uint)offset + (uint)length <= (uint)value.Length);

        return MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.Add(ref MemoryMarshal.GetReference(value), (nint)(uint)offset), length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteUnchecked(
        this Span<byte> destination, ReadOnlySpan<byte> source, int offset = 0)
    {
        Debug.Assert(destination.Length >= source.Length);
        source.CopyToUnsafe(ref destination.AsRef(offset));
    }
}
