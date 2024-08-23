using System.Diagnostics;
using System.Runtime.Intrinsics;

using U8.Abstractions;

namespace U8.CaseConversion;

public readonly struct U8AsciiCaseConverter: IU8CaseConverter {
    public static U8AsciiCaseConverter Instance => default;

    public bool IsFixedLength {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FindToLowerStart(ReadOnlySpan<byte> source) {
        return source.IndexOfAnyInRange((byte)'A', (byte)'Z');
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToLower(ReadOnlySpan<byte> source, Span<byte> destination) {
        if (destination.Length < source.Length) {
            ThrowHelpers.DestinationTooShort();
        }

        ToLowerCore(ref source.AsRef(), ref destination.AsRef(), (uint)source.Length);
        return source.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ToLower(ReadOnlySpan<byte> source, ref InlineU8Builder destination) {
        destination.EnsureCapacity(source.Length);

        ToLowerCore(ref source.AsRef(), ref destination.Free.AsRef(), (uint)source.Length);
        destination.BytesWritten += source.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector256<byte> ToLower(Vector256<byte> utf8) {
        var mask = Vector256.Create((sbyte)0x20);
        var overflow = Vector256.Create<sbyte>(128 - 'A');
        var bound = Vector256.Create<sbyte>(-127 + ('Z' - 'A'));

        return utf8 | ((utf8.AsSByte() + overflow).Lt(bound) & mask).AsByte();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<byte> ToLower(Vector128<byte> utf8) {
        var mask = Vector128.Create((sbyte)0x20);
        var overflow = Vector128.Create<sbyte>(128 - 'A');
        var bound = Vector128.Create<sbyte>(-127 + ('Z' - 'A'));

        return utf8 | ((utf8.AsSByte() + overflow).Lt(bound) & mask).AsByte();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector64<byte> ToLower(Vector64<byte> utf8) {
        var mask = Vector64.Create((sbyte)0x20);
        var overflow = Vector64.Create<sbyte>(128 - 'A');
        var bound = Vector64.Create<sbyte>(-127 + ('Z' - 'A'));

        return utf8 | (Vector64.LessThan(utf8.AsSByte() + overflow, bound) & mask).AsByte();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static void ToLowerCore(ref byte src, ref byte dst, nuint length) {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static byte ToLower1(byte b) => U8Info.IsAsciiLetter(b) ? (byte)(b | 0x20) : b;

        switch ((int)uint.CreateTruncating(length)) {
            case 15: dst.Add(14) = ToLower1(src.Add(14)); goto case 14;
            case 14: dst.Add(13) = ToLower1(src.Add(13)); goto case 13;
            case 13: dst.Add(12) = ToLower1(src.Add(12)); goto case 12;
            case 12: dst.Add(11) = ToLower1(src.Add(11)); goto case 11;
            case 11: dst.Add(10) = ToLower1(src.Add(10)); goto case 10;
            case 10: dst.Add(9) = ToLower1(src.Add(9)); goto case 9;
            case 9: dst.Add(8) = ToLower1(src.Add(8)); goto case 8;
            case 8:
                if (Vector64.IsHardwareAccelerated) {
                    ToLower(Vector64.LoadUnsafe(ref src)).StoreUnsafe(ref dst);
                    goto case 0;
                }
                dst.Add(7) = ToLower1(src.Add(7)); goto case 7;
            case 7: dst.Add(6) = ToLower1(src.Add(6)); goto case 6;
            case 6: dst.Add(5) = ToLower1(src.Add(5)); goto case 5;
            case 5: dst.Add(4) = ToLower1(src.Add(4)); goto case 4;
            case 4: dst.Add(3) = ToLower1(src.Add(3)); goto case 3;
            case 3: dst.Add(2) = ToLower1(src.Add(2)); goto case 2;
            case 2: dst.Add(1) = ToLower1(src.Add(1)); goto case 1;
            case 1: dst.Add(0) = ToLower1(src.Add(0)); goto case 0;
            case 0: return;
        }

        if (length < 32) {
            Debug.Assert(length >= 16);
            var first = Vector128.LoadUnsafe(ref src);
            var last = Vector128.LoadUnsafe(ref src, length - 16);

            first = ToLower(first);
            last = ToLower(last);

            first.StoreUnsafe(ref dst);
            last.StoreUnsafe(ref dst, length - 16);

            return;
        }

        nuint offset = 0;
        var mask = Vector256.Create((sbyte)0x20);
        var overflow = Vector256.Create<sbyte>(128 - 'A');
        var bound = Vector256.Create<sbyte>(-127 + ('Z' - 'A'));
        var lastvec = length - (nuint)Vector256<byte>.Count;
        do {
            var utf8 = Vector256.LoadUnsafe(ref src, offset).AsSByte();
            var changeCase = (utf8 + overflow).Lt(bound) & mask;

            (utf8 | changeCase).AsByte().StoreUnsafe(ref dst, offset);

            offset += (nuint)Vector256<byte>.Count;
        } while (offset <= lastvec);

        var tail = Vector256.LoadUnsafe(ref src, lastvec).AsSByte();
        var tailChangeCase = (tail.AsSByte() + overflow).Lt(bound) & mask;

        (tail | tailChangeCase).AsByte().StoreUnsafe(ref dst, lastvec);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FindToUpperStart(ReadOnlySpan<byte> source) {
        return source.IndexOfAnyInRange((byte)'a', (byte)'z');
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToUpper(ReadOnlySpan<byte> source, Span<byte> destination) {
        if (destination.Length < source.Length) {
            ThrowHelpers.DestinationTooShort();
        }

        ToUpperCore(ref source.AsRef(), ref destination.AsRef(), (uint)source.Length);
        return source.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ToUpper(ReadOnlySpan<byte> source, ref InlineU8Builder destination) {
        destination.EnsureCapacity(source.Length);

        ToUpperCore(ref source.AsRef(), ref destination.Free.AsRef(), (uint)source.Length);
        destination.BytesWritten += source.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector256<byte> ToUpper(Vector256<byte> utf) {
        var mask = Vector256.Create((sbyte)0x20);
        var overflow = Vector256.Create<sbyte>(128 - 'a');
        var bound = Vector256.Create<sbyte>(-127 + ('z' - 'a'));

        return utf ^ ((utf.AsSByte() + overflow).Lt(bound) & mask).AsByte();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<byte> ToUpper(Vector128<byte> utf) {
        var mask = Vector128.Create((sbyte)0x20);
        var overflow = Vector128.Create<sbyte>(128 - 'a');
        var bound = Vector128.Create<sbyte>(-127 + ('z' - 'a'));

        return utf ^ ((utf.AsSByte() + overflow).Lt(bound) & mask).AsByte();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector64<byte> ToUpper(Vector64<byte> ascii) {
        var mask = Vector64.Create((sbyte)0x20);
        var overflow = Vector64.Create<sbyte>(128 - 'a');
        var bound = Vector64.Create<sbyte>(-127 + ('z' - 'a'));

        return ascii ^ (Vector64.LessThan(ascii.AsSByte() + overflow, bound) & mask).AsByte();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static void ToUpperCore(ref byte src, ref byte dst, nuint length) {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static byte ToUpper1(byte b) => U8Info.IsAsciiLetter(b) ? (byte)(b & ~0x20) : b;

        switch ((int)uint.CreateTruncating(length)) {
            case 15: dst.Add(14) = ToUpper1(src.Add(14)); goto case 14;
            case 14: dst.Add(13) = ToUpper1(src.Add(13)); goto case 13;
            case 13: dst.Add(12) = ToUpper1(src.Add(12)); goto case 12;
            case 12: dst.Add(11) = ToUpper1(src.Add(11)); goto case 11;
            case 11: dst.Add(10) = ToUpper1(src.Add(10)); goto case 10;
            case 10: dst.Add(9) = ToUpper1(src.Add(9)); goto case 9;
            case 9: dst.Add(8) = ToUpper1(src.Add(8)); goto case 8;
            case 8:
                if (Vector64.IsHardwareAccelerated) {
                    ToUpper(Vector64.LoadUnsafe(ref src)).StoreUnsafe(ref dst);
                    goto case 0;
                }
                dst.Add(7) = ToUpper1(src.Add(7)); goto case 7;
            case 7: dst.Add(6) = ToUpper1(src.Add(6)); goto case 6;
            case 6: dst.Add(5) = ToUpper1(src.Add(5)); goto case 5;
            case 5: dst.Add(4) = ToUpper1(src.Add(4)); goto case 4;
            case 4: dst.Add(3) = ToUpper1(src.Add(3)); goto case 3;
            case 3: dst.Add(2) = ToUpper1(src.Add(2)); goto case 2;
            case 2: dst.Add(1) = ToUpper1(src.Add(1)); goto case 1;
            case 1: dst.Add(0) = ToUpper1(src.Add(0)); goto case 0;
            case 0: return;
        }

        if (length < 32) {
            Debug.Assert(length >= 16);
            var first = Vector128.LoadUnsafe(ref src);
            var last = Vector128.LoadUnsafe(ref src, length - 16);

            first = ToUpper(first);
            last = ToUpper(last);

            first.StoreUnsafe(ref dst);
            last.StoreUnsafe(ref dst, length - 16);

            return;
        }

        nuint offset = 0;
        var mask = Vector256.Create((sbyte)0x20);
        var overflow = Vector256.Create<sbyte>(128 - 'a');
        var bound = Vector256.Create<sbyte>(-127 + ('z' - 'a'));
        var lastvec = length - (nuint)Vector256<byte>.Count;
        do {
            var utf8 = Vector256.LoadUnsafe(ref src, offset).AsSByte();
            var changeCase = (utf8 + overflow).Lt(bound) & mask;

            (utf8 ^ changeCase).AsByte().StoreUnsafe(ref dst, offset);

            offset += (nuint)Vector256<byte>.Count;
        } while (offset <= lastvec);

        var tail = Vector256.LoadUnsafe(ref src, lastvec).AsSByte();
        var tailChangeCase = (tail.AsSByte() + overflow).Lt(bound) & mask;

        (tail ^ tailChangeCase).AsByte().StoreUnsafe(ref dst, lastvec);
    }
}
