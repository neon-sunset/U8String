using System.Diagnostics;
using System.Runtime.InteropServices;

namespace U8.Primitives;

/// <summary>
/// Represents a portion of UTF-8 code units in a <see cref="U8Source"/>.
/// </summary>
/// <remarks>
/// This type is intended to be used as a lightweight slice of <see cref="U8String.Source"/> and
/// allows to re-construct the <see cref="U8String"/> by calling <see cref="U8Source.SliceUnchecked(U8Range)"/>.
/// <para/>
/// It is particularly useful when a type needs to expose multiple slices that originate from the same
/// <see cref="U8String.Source"/> without having to spend memory on storing the entire <see cref="U8String"/>.
/// </remarks>
/// <seealso cref="U8Source"/>
/// <seealso cref="U8String.Source"/>
[StructLayout(LayoutKind.Sequential)]
public readonly struct U8Range: IEquatable<U8Range> {
    internal readonly int Offset;
    public readonly int Length;

    internal long Packed {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Unsafe.BitCast<U8Range, long>(this);
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public U8Range(int offset, int length) {
        Debug.Assert((uint)offset <= int.MaxValue);
        Debug.Assert((uint)length <= int.MaxValue);

        Offset = offset;
        Length = length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(U8Range other) {
        return Packed == other.Packed;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) {
        return obj is U8Range range && Equals(range);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => Packed.GetHashCode();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(U8Range left, U8Range right) {
        return left.Equals(right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(U8Range left, U8Range right) {
        return !(left == right);
    }
}
