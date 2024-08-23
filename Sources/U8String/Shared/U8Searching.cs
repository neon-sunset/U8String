using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Text;

using U8.Abstractions;
using U8.Primitives;

namespace U8.Shared;

// TODO: Better name?
static class U8Searching {
    /// <summary>
    /// Returns the index of the first occurrence of a specified value in a span.
    /// </summary>
    /// <remarks>
    /// Designed to be inlined into the caller and optimized away on constants.
    /// <para>
    /// Contract: when T is char, it must never be a surrogate.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Contains<T>(T value, ref byte src, int length)
        where T : unmanaged {
        Debug.Assert(value is not char s || !char.IsSurrogate(s));
        Debug.Assert(value is byte or char or Rune);

        return value switch {
            byte b => ContainsByte(b, ref src, length),
            char c => ContainsChar(c, ref src, length),
            Rune r => ContainsRune(r, ref src, length),
            _ => ThrowHelpers.Unreachable<bool>()
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool ContainsByte(byte value, ref byte src, int length) {
            return MemoryMarshal.CreateReadOnlySpan(ref src, length).Contains(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool ContainsChar(char value, ref byte src, int length) {
            var span = MemoryMarshal.CreateReadOnlySpan(ref src, length);
            return char.IsAscii(value)
                ? span.Contains((byte)value)
                : span.IndexOf(value <= 0x7FF ? value.AsTwoBytes() : value.AsThreeBytes()) >= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool ContainsRune(Rune value, ref byte src, int length) {
            var span = MemoryMarshal.CreateReadOnlySpan(ref src, length);
            return value.IsAscii
                ? span.Contains((byte)value.Value)
                : span.IndexOf(value.Value switch {
                    <= 0x7FF => value.AsTwoBytes(),
                    <= 0xFFFF => value.AsThreeBytes(),
                    _ => value.AsFourBytes()
                }) >= 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Contains(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value) {
        return value.Length is 1
            ? source.Contains(value[0])
            : source.IndexOf(value) >= 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Contains<T, C>(ReadOnlySpan<byte> source, T value, C comparer)
        where T : struct
        where C : IU8ContainsOperator {
        Debug.Assert(value is not char s || !char.IsSurrogate(s));
        Debug.Assert(value is byte or char or Rune or U8String);

        return value switch {
            byte b => comparer.Contains(source, b),

            char c => char.IsAscii(c)
                ? comparer.Contains(source, (byte)c)
                : comparer.Contains(source, new U8Scalar(c, checkAscii: false).AsSpan()),

            Rune r => r.IsAscii
                ? comparer.Contains(source, (byte)r.Value)
                : comparer.Contains(source, new U8Scalar(r, checkAscii: false).AsSpan()),

            U8String str => Contains(source, str.AsSpan(), comparer),

            _ => ThrowHelpers.Unreachable<bool>()
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Contains<T>(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value, T comparer)
        where T : IU8ContainsOperator {
        Debug.Assert(!source.IsEmpty);

        return value.Length is 1
            ? comparer.Contains(source, value[0]) // TODO: Verify if this bounds checks
            : comparer.Contains(source, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool ContainsSegment<T>(
        ReadOnlySpan<byte> haystack,
        ReadOnlySpan<byte> needle,
        T separator) where T : unmanaged {
        Debug.Assert(separator is byte or char or Rune);
        Debug.Assert(separator is not char s || !char.IsSurrogate(s));

        return separator switch {
            byte b => ContainsSegment(haystack, needle, b),

            char c => char.IsAscii(c)
                ? ContainsSegment(haystack, needle, (byte)c)
                : ContainsSegment(haystack, needle, c <= 0x7FF ? c.AsTwoBytes() : c.AsThreeBytes()),

            Rune r => r.IsAscii
                ? ContainsSegment(haystack, needle, (byte)r.Value)
                : ContainsSegment(haystack, needle, r.Value switch {
                    <= 0x7FF => r.AsTwoBytes(),
                    <= 0xFFFF => r.AsThreeBytes(),
                    _ => r.AsFourBytes()
                }),

            _ => ThrowHelpers.Unreachable<bool>()
        };
    }

    internal static bool ContainsSegment(
        ReadOnlySpan<byte> haystack,
        ReadOnlySpan<byte> needle,
        byte separator) {
        var found = false;
        if (!needle.Contains(separator)) {
            while (true) {
                var matchOffset = haystack.IndexOf(needle);
                // Remaining search space contains no more candidates.
                if (matchOffset < 0) {
                    break;
                }
                // Candidate is at the start of the search space.
                else if (matchOffset is 0) {
                    // Candidate either equals the search space or is followed by the separator.
                    if (haystack.Length == needle.Length ||
                        haystack.AsRef(needle.Length) == separator) {
                        found = true;
                        break;
                    }
                }
                // Candidate is at the end of the search space.
                else if (matchOffset == haystack.Length - needle.Length) {
                    // Candidate is preceded by the separator.
                    if (haystack.AsRef(matchOffset - 1) == separator) {
                        found = true;
                        break;
                    }
                }
                // Candidate is in the middle of the search space.
                else if (
                    haystack.AsRef(matchOffset - 1) == separator &&
                    haystack.AsRef(matchOffset + needle.Length) == separator) {
                    found = true;
                    break;
                }

                // Candidate was not at the end of the search space and was
                // not followed by the separator, so we can skip an extra byte.
                haystack = haystack.SliceUnsafe(matchOffset + needle.Length + 1);
            }
        }

        return found;
    }

    internal static bool ContainsSegment(
        ReadOnlySpan<byte> haystack,
        ReadOnlySpan<byte> needle,
        ReadOnlySpan<byte> separator) {
        var found = false;
        if (!Contains(needle, separator)) {
            while (true) {
                var matchOffset = haystack.IndexOf(needle);
                // Remaining search space contains no more candidates.
                if (matchOffset < 0) {
                    break;
                }
                // Candidate is at the start of the search space.
                else if (matchOffset is 0) {
                    // Candidate either equals the search space or is followed by the separator.
                    if (haystack.Length == needle.Length ||
                        haystack.SliceUnsafe(needle.Length)
                                .StartsWith(separator)) {
                        found = true;
                        break;
                    }
                }
                // Candidate is at the end of the search space.
                else if (matchOffset == haystack.Length - needle.Length) {
                    // Candidate is preceded by the separator.
                    if (haystack.SliceUnsafe(0, matchOffset)
                                .EndsWith(separator)) {
                        found = true;
                        break;
                    }
                }
                // Candidate is in the middle of the search space.
                else if (
                    haystack.SliceUnsafe(0, matchOffset)
                            .EndsWith(separator) &&
                    haystack.SliceUnsafe(matchOffset + needle.Length)
                            .StartsWith(separator)) {
                    found = true;
                    break;
                }

                // Candidate was not at the end of the search space and was
                // not followed by the separator, so we can skip separator.Length too.
                haystack = haystack.SliceUnsafe(
                    matchOffset + needle.Length + separator.Length);
            }
        }

        return found;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool ContainsSegment<T, C>(
        ReadOnlySpan<byte> haystack,
        ReadOnlySpan<byte> needle,
        T separator,
        C comparer)
            where T : struct
            where C : IU8Comparer {
        Debug.Assert(separator is byte or char or Rune or U8String);
        Debug.Assert(separator is not char s || !char.IsSurrogate(s));

        return separator switch {
            byte b => ContainsSegment(haystack, needle, b, comparer),

            char c => char.IsAscii(c)
                ? ContainsSegment(haystack, needle, (byte)c, comparer)
                : SplitContains(haystack, needle, new U8Scalar(c, checkAscii: false).AsSpan(), comparer),

            Rune r => r.IsAscii
                ? ContainsSegment(haystack, needle, (byte)r.Value, comparer)
                : SplitContains(haystack, needle, new U8Scalar(r, checkAscii: false).AsSpan(), comparer),

            U8String str => SplitContains(haystack, needle, str.AsSpan(), comparer),

            _ => ThrowHelpers.Unreachable<bool>()
        };
    }

    internal static bool ContainsSegment<C>(
        ReadOnlySpan<byte> haystack,
        ReadOnlySpan<byte> needle,
        byte separator,
        C comparer) where C : IU8Comparer {
        if (!comparer.Contains(needle, separator)) {
            while (true) {
                var (matchOffset, matchLength) = comparer.IndexOf(haystack, needle);
                // Remaining search space contains no more candidates.
                if (matchOffset < 0) {
                    break;
                }
                // Candidate is at the start of the search space.
                else if (matchOffset is 0) {
                    // Candidate either equals the search space or is followed by the separator.
                    if (haystack.Length == matchLength ||
                        comparer.StartsWith(haystack.SliceUnsafe(matchLength), separator)) {
                        goto Match;
                    }
                }
                // Candidate is at the end of the search space.
                else if (matchOffset == haystack.Length - matchLength) {
                    // Candidate is preceded by the separator.
                    if (comparer.EndsWith(haystack.SliceUnsafe(0, matchOffset), separator)) {
                        goto Match;
                    }
                }
                // Candidate is in the middle of the search space.
                else if (
                    comparer.EndsWith(haystack.SliceUnsafe(0, matchOffset), separator) &&
                    comparer.StartsWith(haystack.SliceUnsafe(matchOffset + matchLength), separator)) {
                    goto Match;
                }

                haystack = haystack.SliceUnsafe(matchOffset + matchLength);
            }
        }

        return false;

        // Merging return blocks manually because compiler doesn't want to.
        Match:
        return true;
    }

    internal static bool SplitContains<T>(
        ReadOnlySpan<byte> haystack,
        ReadOnlySpan<byte> needle,
        ReadOnlySpan<byte> separator,
        T comparer) where T : IU8Comparer {
        if (!comparer.Contains(needle, separator)) {
            while (true) {
                var (matchOffset, matchLength) = comparer.IndexOf(haystack, needle);
                // Remaining search space contains no more candidates.
                if (matchOffset < 0) {
                    break;
                }
                // Candidate is at the start of the search space.
                else if (matchOffset is 0) {
                    // Candidate either equals the search space or is followed by the separator.
                    if (haystack.Length == matchLength ||
                        comparer.StartsWith(haystack.SliceUnsafe(matchLength), separator)) {
                        goto Match;
                    }
                }
                // Candidate is at the end of the search space.
                else if (matchOffset == haystack.Length - matchLength) {
                    // Candidate is preceded by the separator.
                    if (comparer.EndsWith(haystack.SliceUnsafe(0, matchOffset), separator)) {
                        goto Match;
                    }
                }
                // Candidate is in the middle of the search space.
                else if (
                    comparer.EndsWith(haystack.SliceUnsafe(0, matchOffset), separator) &&
                    comparer.StartsWith(haystack.SliceUnsafe(matchOffset + matchLength), separator)) {
                    goto Match;
                }

                haystack = haystack.SliceUnsafe(matchOffset + matchLength);
            }
        }

        return false;

        // Merging return blocks manually because compiler doesn't want to.
        Match:
        return true;
    }

    /// <summary>
    /// Contract: when T is char, it must never be a surrogate.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Count<T>(ReadOnlySpan<byte> source, T value)
        where T : struct {
        Debug.Assert(value is not char i || !char.IsSurrogate(i));
        Debug.Assert(value is byte or char or Rune or U8String);

        return value switch {
            byte b => (int)(uint)CountByte(b, ref source.AsRef(), (uint)source.Length),

            char c => char.IsAscii(c)
                ? (int)(uint)CountByte((byte)c, ref source.AsRef(), (uint)source.Length)
                : source.Count((ushort)c switch {
                    <= 0x7FF => c.AsTwoBytes(),
                    _ => c.AsThreeBytes()
                }),

            Rune r => r.IsAscii
                ? (int)(uint)CountByte((byte)r.Value, ref source.AsRef(), (uint)source.Length)
                : source.Count(r.Value switch {
                    <= 0x7FF => r.AsTwoBytes(),
                    <= 0xFFFF => r.AsThreeBytes(),
                    _ => r.AsFourBytes()
                }),

            U8String str => Count(source, str.AsSpan()),

            _ => ThrowHelpers.Unreachable<int>()
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Count(ReadOnlySpan<byte> value, ReadOnlySpan<byte> item) {
        // Although span.Count checks internally for Length == 1, the way it is written
        // is not inlineable due to a loop in the default arm of its switch.
        // Therefore, we have to pre-check this here in order to improve the case
        // where item is a single-byte literal, which is expected to be relatively common.
        return item.Length is 1
            ? (int)(uint)CountByte(item[0], ref value.AsRef(), (uint)value.Length)
            : value.Count(item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Count<T, C>(ReadOnlySpan<byte> source, T value, C comparer)
        where T : struct
        where C : IU8CountOperator {
        Debug.Assert(value is not char i || !char.IsSurrogate(i));
        Debug.Assert(value is byte or char or Rune or U8String);

        return value switch {
            byte b => comparer.Count(source, b),

            char c => char.IsAscii(c)
                ? comparer.Count(source, (byte)c)
                : comparer.Count(source, new U8Scalar(c, checkAscii: false).AsSpan()),

            Rune r => r.IsAscii
                ? comparer.Count(source, (byte)r.Value)
                : comparer.Count(source, new U8Scalar(r, checkAscii: false).AsSpan()),

            U8String str => Count(source, str.AsSpan(), comparer),

            _ => ThrowHelpers.Unreachable<int>()
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Count<T>(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value, T comparer)
        where T : IU8CountOperator {
        return comparer.Count(source, value);
    }

    // This works around NativeAOT not inlining CountByte with further dispatch to Arm64 version.
    // Moving the impl. to CountByteCore and hoisting the dispatch to small CountByte method fixes it.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static nuint CountByte(byte value, ref byte src, nuint length) {
        return AdvSimd.Arm64.IsSupported
            ? CountByteArm64(value, ref src, length)
            : CountByteCore(value, ref src, length);
    }

    internal static nuint CountByteCore(byte value, ref byte src, nuint length) {
        var count = (nuint)0;
        if (length is 0) goto Empty;

        ref var end = ref src.Add(length);
        if (Vector256.IsHardwareAccelerated &&
            length >= (nuint)Vector512<byte>.Count) {
            var needle = Vector512.Create(value);
            ref var lastvec = ref end.Substract(Vector512<byte>.Count);
            do {
                count += Vector512
                    .LoadUnsafe(ref src)
                    .Eq(needle)
                    .GetMatchCount();

                src = ref src.Add(Vector512<byte>.Count);
            } while (src.LessThanOrEqual(ref lastvec));
        }

        // All platforms targeted by .NET 8+ are supposed to support 128b SIMD.
        // If this is not the case, please file an issue (it will work but slowly).
        if (src.Add(Vector256<byte>.Count).LessThanOrEqual(ref end)) {
            var needle = Vector256.Create(value);
            ref var lastvec = ref end.Substract(Vector256<byte>.Count);
            do {
                count += Vector256
                    .LoadUnsafe(ref src)
                    .Eq(needle)
                    .GetMatchCount();

                src = ref src.Add(Vector256<byte>.Count);

                // Skip this loop if we took the V512 path above
                // since we can only do a single iteration at most.
            } while (!Vector256.IsHardwareAccelerated && src.LessThanOrEqual(ref lastvec));
        }

        if (src.Add(Vector128<byte>.Count).LessThanOrEqual(ref end)) {
            var needle = Vector128.Create(value);
            count += Vector128
                .LoadUnsafe(ref src)
                .Eq(needle)
                .GetMatchCount();

            src = ref src.Add(Vector128<byte>.Count);
        }

        while (src.LessThan(ref end)) {
            // Branchless: x86_64: cmp + setge; arm64: cmn + cset
            count += (nuint)(src == value ? 1 : 0);
            src = ref src.Add(1);
        }

        Empty:
        return count;
    }

    // TODO: Consolidate this back to CounByte - the initial optimization was invalid,
    // and now its adapted form in VectorExtensions can be handled by .NET in a good enough way.
    internal static nuint CountByteArm64(byte value, ref byte src, nuint length) {
        Debug.Assert(AdvSimd.Arm64.IsSupported);

        var count = (nuint)0;
        if (length is 0) goto Empty;

        ref var end = ref src.Add(length);
        if (length >= (nuint)Vector256<byte>.Count) {
            var needle = Vector256.Create(value);
            ref var lastvec = ref end.Substract(Vector256<byte>.Count);
            do {
                count += Vector256
                    .LoadUnsafe(ref src)
                    .Eq(needle)
                    .GetMatchCount();

                src = ref src.Add(Vector256<byte>.Count);
            } while (src.LessThanOrEqual(ref lastvec));
        }

        if (src.Add(Vector128<byte>.Count).LessThanOrEqual(ref end)) {
            var needle = Vector128.Create(value);
            count += Vector128
                .LoadUnsafe(ref src)
                .Eq(needle)
                .GetMatchCount();

            src = ref src.Add(Vector128<byte>.Count);
        }

        if (src.Add(Vector64<byte>.Count).LessThanOrEqual(ref end)) {
            var needle = Vector64.Create(value);
            count += Vector64
                .LoadUnsafe(ref src)
                .Eq(needle)
                .GetMatchCount();

            src = ref src.Add(Vector64<byte>.Count);
        }

        while (src.LessThan(ref end)) {
            // Branchless: x86_64: cmp + setge; arm64: cmn + cset
            count += (nuint)(src == value ? 1 : 0);
            src = ref src.Add(1);
        }

        Empty:
        return count;
    }

    internal static nuint CountEitherByte(byte a, byte b, ref byte src, nuint length) {
        var count = (nuint)0;
        if (length is 0) goto Empty;

        ref var end = ref src.Add(length);
        if (Vector256.IsHardwareAccelerated &&
            length >= (nuint)Vector512<byte>.Count) {
            var needle1 = Vector512.Create(a);
            var needle2 = Vector512.Create(b);
            ref var lastvec = ref end.Substract(Vector512<byte>.Count);
            do {
                var chunk = Vector512.LoadUnsafe(ref src);
                var matches1 = chunk.Eq(needle1);
                var matches2 = chunk.Eq(needle2);
                count += (matches1 | matches2).GetMatchCount();

                src = ref src.Add(Vector512<byte>.Count);
            } while (src.LessThanOrEqual(ref lastvec));
        }

        // All platforms targeted by .NET 8+ are supposed to support 128b SIMD.
        // If this is not the case, please file an issue (it will work but slowly).
        if (src.Add(Vector256<byte>.Count).LessThanOrEqual(ref end)) {
            var needle1 = Vector256.Create(a);
            var needle2 = Vector256.Create(b);
            ref var lastvec = ref end.Substract(Vector256<byte>.Count);
            do {
                var chunk = Vector256.LoadUnsafe(ref src);
                var matches1 = chunk.Eq(needle1);
                var matches2 = chunk.Eq(needle2);
                count += (matches1 | matches2).GetMatchCount();

                src = ref src.Add(Vector256<byte>.Count);

                // Skip this loop if we took the V512 path above
                // since we can only do a single iteration at most.
            } while (!Vector256.IsHardwareAccelerated && src.LessThanOrEqual(ref lastvec));
        }

        if (src.Add(Vector128<byte>.Count).LessThanOrEqual(ref end)) {
            var needle1 = Vector128.Create(a);
            var needle2 = Vector128.Create(b);
            var chunk = Vector128.LoadUnsafe(ref src);
            var matches1 = chunk.Eq(needle1);
            var matches2 = chunk.Eq(needle2);
            count += (matches1 | matches2).GetMatchCount();

            src = ref src.Add(Vector128<byte>.Count);
        }

        while (src.LessThan(ref end)) {
            // Branchless: x86_64: cmp + setge; arm64: cmn + cset
            if (src == a | src == b) {
                count++;
            }
            src = ref src.Add(1);
        }

        Empty:
        return count;
    }

    // Bypass DynamicPGO because it sometimes moves VectorXXX.Create
    // into the loops which is very much not what we want. In this case
    // PGO wins are minor compared to regressions for some of its decisions.
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static nuint CountRunes(ref byte src, nuint length) {
        Debug.Assert(length > 0);

        // Adopted from https://github.com/simdutf/simdutf/blob/master/src/generic/utf8.h#L10
        // This method achieves width x2 unrolling by relying on new struct promotion and
        // helpers in VectorExtensions. Operations on 512b and 256b are intentional.
        var count = (nuint)0;
        ref var ptr = ref Unsafe.As<byte, sbyte>(ref src);
        ref var end = ref ptr.Add(length);

        if (Vector256.IsHardwareAccelerated &&
            length >= (nuint)Vector512<byte>.Count) {
            ref var lastvec = ref end.Substract(Vector512<byte>.Count);
            var continuations = Vector512.Create((sbyte)-64);
            do {
                var chunk = Vector512.LoadUnsafe(ref ptr);
                var matches = Vector512.LessThan(chunk, continuations);

                count += 64 - matches.AsByte().GetMatchCount();
                ptr = ref ptr.Add(Vector512<byte>.Count);
            } while (ptr.LessThanOrEqual(ref lastvec));
        }

        // All platforms targeted by .NET 8+ are supposed to support 128b SIMD.
        // If this is not the case, please file an issue (it will work but slowly).
        if (ptr.Add(Vector256<byte>.Count).LessThanOrEqual(ref end)) {
            ref var lastvec = ref end.Substract(Vector256<byte>.Count);
            var continuations = Vector256.Create((sbyte)-64);
            do {
                var chunk = Vector256.LoadUnsafe(ref ptr);
                var matches = Vector256.LessThan(chunk, continuations);

                count += 32 - matches.AsByte().GetMatchCount();
                ptr = ref ptr.Add(Vector256<byte>.Count);

                // Skip this loop if we took the V512 path above
                // since we can only do a single iteration at most.
            } while (!Vector256.IsHardwareAccelerated && ptr.LessThanOrEqual(ref lastvec));
        }

        if (Vector128.IsHardwareAccelerated &&
            ptr.Add(Vector128<byte>.Count).LessThanOrEqual(ref end)) {
            var continuations = Vector128.Create((sbyte)-64);
            var chunk = Vector128.LoadUnsafe(ref ptr);
            var matches = Vector128.LessThan(chunk, continuations);

            count += 16 - matches.AsByte().GetMatchCount();
            ptr = ref ptr.Add(Vector128<byte>.Count);
        }

        if (AdvSimd.Arm64.IsSupported &&
            ptr.Add(Vector64<byte>.Count).LessThanOrEqual(ref end)) {
            var continuations = Vector64.Create((sbyte)-64);
            var chunk = Vector64.LoadUnsafe(ref ptr);
            var matches = Vector64.LessThan(chunk, continuations);

            count += 8 - matches.AsByte().GetMatchCount();
            ptr = ref ptr.Add(Vector64<byte>.Count);
        }

        while (ptr.LessThan(ref end)) {
            // Branchless: x86_64: cmp + setge; arm64: cmn + cset
            count += (nuint)(ptr < -64 ? 0 : 1);
            ptr = ref ptr.Add(1);
        }

        return count;
    }

    /// <summary>
    /// Contract: when T is char, it must never be a surrogate.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (int Offset, int Length) IndexOf<T>(ReadOnlySpan<byte> source, T value)
        where T : struct {
        Debug.Assert(value is not char i || !char.IsSurrogate(i));
        Debug.Assert(value is byte or char or Rune or U8String /* or ReadOnlyMemory<byte> */);

        switch (value) {
            case byte b:
                return (source.IndexOf(b), 1);

            case char c:
                if (c <= 0x7F) {
                    return (source.IndexOf((byte)c), 1);
                }

                bytes scalar;
                int scalarLength;

                switch ((ushort)c) {
                    case <= 0x7FF:
                        scalar = c.AsTwoBytes().AsSpan();
                        scalarLength = 2;
                        break;

                    default:
                        scalar = c.AsThreeBytes().AsSpan();
                        scalarLength = 3;
                        break;
                }

                return (source.IndexOf(scalar), scalarLength);

            case Rune r:
                if (r.IsAscii) {
                    return (source.IndexOf((byte)r.Value), 1);
                }

                ReadOnlySpan<byte> rune;
                int runeLength;

                switch (r.Value) {
                    case <= 0x7FF:
                        rune = r.AsTwoBytes().AsSpan();
                        runeLength = 2;
                        break;

                    case <= 0xFFFF:
                        rune = r.AsThreeBytes().AsSpan();
                        runeLength = 3;
                        break;

                    default:
                        rune = r.AsFourBytes().AsSpan();
                        runeLength = 4;
                        break;
                }

                return (source.IndexOf(rune), runeLength);

            case U8String str:
                var span = str.AsSpan();
                return (IndexOf(source, span), span.Length);

            // TODO: Investigate the impact on inlining regressions due to IL size.
            // case ReadOnlyMemory<byte> mem:
            //     return (IndexOf(source, mem.Span), mem.Length);

            default:
                return ThrowHelpers.Unreachable<(int, int)>();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int IndexOf(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value) {
        return value.Length is 1 ? source.IndexOf(value[0]) : source.IndexOf(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (int Offset, int Length) IndexOf<T, C>(ReadOnlySpan<byte> source, T value, C comparer)
        where T : struct
        where C : IU8IndexOfOperator {
        Debug.Assert(value is not char i || !char.IsSurrogate(i));
        Debug.Assert(value is byte or char or Rune or U8String);

        switch (value) {
            case byte b:
                return comparer.IndexOf(source, b);

            case char c:
                if (char.IsAscii(c)) {
                    return comparer.IndexOf(source, (byte)c);
                }

                var scalar = new U8Scalar(c, checkAscii: false);
                return comparer.IndexOf(source, scalar.AsSpan());

            case Rune r:
                if (r.IsAscii) {
                    return comparer.IndexOf(source, (byte)r.Value);
                }

                var rune = new U8Scalar(r, checkAscii: false);
                return comparer.IndexOf(source, rune.AsSpan());

            case U8String str:
                var span = str.AsSpan();
                return IndexOf(source, span, comparer);

            default:
                return ThrowHelpers.Unreachable<(int, int)>();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (int Offset, int Length) IndexOf<T>(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value, T comparer)
        where T : IU8IndexOfOperator {
        return value.Length is 1
            ? comparer.IndexOf(source, value[0])
            : comparer.IndexOf(source, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (int Offset, int Length) LastIndexOf<T>(ReadOnlySpan<byte> source, T value)
        where T : struct {
        Debug.Assert(value is not char i || !char.IsSurrogate(i));
        Debug.Assert(value is byte or char or Rune or U8String);

        switch (value) {
            case byte b:
                return (source.LastIndexOf(b), 1);

            case char c:
                if (char.IsAscii(c)) {
                    return (source.LastIndexOf((byte)c), 1);
                }

                ReadOnlySpan<byte> scalar;
                int scalarLength;

                switch ((ushort)c) {
                    case <= 0x7FF:
                        scalar = c.AsTwoBytes().AsSpan();
                        scalarLength = 2;
                        break;

                    default:
                        scalar = c.AsThreeBytes().AsSpan();
                        scalarLength = 3;
                        break;
                }

                return (source.LastIndexOf(scalar), scalarLength);

            case Rune r:
                if (r.IsAscii) {
                    return (source.LastIndexOf((byte)r.Value), 1);
                }

                ReadOnlySpan<byte> rune;
                int runeLength;

                switch (r.Value) {
                    case <= 0x7FF:
                        rune = r.AsTwoBytes().AsSpan();
                        runeLength = 2;
                        break;

                    case <= 0xFFFF:
                        rune = r.AsThreeBytes().AsSpan();
                        runeLength = 3;
                        break;

                    default:
                        rune = r.AsFourBytes().AsSpan();
                        runeLength = 4;
                        break;
                }

                return (source.LastIndexOf(rune), runeLength);

            case U8String str:
                var span = str.AsSpan();
                return (LastIndexOf(source, span), span.Length);

            default:
                return ThrowHelpers.Unreachable<(int, int)>();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int LastIndexOf(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value) {
        return value.Length is 1 ? source.LastIndexOf(value[0]) : source.LastIndexOf(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (int Offset, int Length) LastIndexOf<T, C>(ReadOnlySpan<byte> source, T value, C comparer)
        where T : struct
        where C : IU8LastIndexOfOperator {
        Debug.Assert(value is not char i || !char.IsSurrogate(i));
        Debug.Assert(value is byte or char or Rune or U8String);

        switch (value) {
            case byte b:
                return comparer.LastIndexOf(source, b);

            case char c:
                if (char.IsAscii(c)) {
                    return comparer.LastIndexOf(source, (byte)c);
                }

                var scalar = new U8Scalar(c, checkAscii: false);
                return comparer.LastIndexOf(source, scalar.AsSpan());

            case Rune r:
                if (r.IsAscii) {
                    return comparer.LastIndexOf(source, (byte)r.Value);
                }

                var rune = new U8Scalar(r, checkAscii: false);
                return comparer.LastIndexOf(source, rune.AsSpan());

            case U8String str:
                var span = str.AsSpan();
                return LastIndexOf(source, span, comparer);

            default:
                return ThrowHelpers.Unreachable<(int, int)>();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (int Offset, int Length) LastIndexOf<T>(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value, T comparer)
        where T : IU8LastIndexOfOperator {
        return value.Length is 1
            ? comparer.LastIndexOf(source, value[0])
            : comparer.LastIndexOf(source, value);
    }

    // TODO: Make this return index of segment and offset of segment - right now
    // public methods calling into this don't do that and there is no convenient way
    // to work around that should the user need it.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int IndexOfSegment<T>(
        ReadOnlySpan<byte> haystack,
        ReadOnlySpan<byte> needle,
        T separator)
            where T : unmanaged {
        Debug.Assert(separator is byte or char or Rune);
        Debug.Assert(separator is not char s || !char.IsSurrogate(s));

        return separator switch {
            byte b => IndexOfSegment(haystack, needle, b),

            char c => char.IsAscii(c)
                ? IndexOfSegment(haystack, needle, (byte)c)
                : IndexOfSegment(haystack, needle, c <= 0x7FF ? c.AsTwoBytes() : c.AsThreeBytes()),

            Rune r => r.IsAscii
                ? IndexOfSegment(haystack, needle, (byte)r.Value)
                : IndexOfSegment(haystack, needle, r.Value switch {
                    <= 0x7FF => r.AsTwoBytes(),
                    <= 0xFFFF => r.AsThreeBytes(),
                    _ => r.AsFourBytes()
                }),

            _ => ThrowHelpers.Unreachable<int>()
        };
    }

    internal static int IndexOfSegment(
        ReadOnlySpan<byte> haystack,
        ReadOnlySpan<byte> needle,
        byte separator) {
        var index = 0;
        if (!needle.Contains(separator)) {
            while (true) {
                var matchOffset = haystack.IndexOf(needle);
                index += matchOffset;

                // Remaining search space contains no more candidates.
                if (matchOffset < 0) {
                    index = -1;
                    break;
                }
                // Candidate is at the start of the search space.
                else if (matchOffset is 0) {
                    // Candidate either equals the search space or is followed by the separator.
                    if (haystack.Length == needle.Length ||
                        haystack.AsRef(needle.Length) == separator) {
                        break;
                    }
                }
                // Candidate is at the end of the search space.
                else if (matchOffset == haystack.Length - needle.Length) {
                    // Candidate is preceded by the separator.
                    if (haystack.AsRef(matchOffset - 1) == separator) {
                        break;
                    }
                }
                // Candidate is in the middle of the search space.
                else if (
                    haystack.AsRef(matchOffset - 1) == separator &&
                    haystack.AsRef(matchOffset + needle.Length) == separator) {
                    break;
                }

                // Candidate was not at the end of the search space and was
                // not followed by the separator, so we can skip an extra byte.
                index += needle.Length + 1;
                haystack = haystack.SliceUnsafe(matchOffset + needle.Length + 1);
            }
        }
        else if (needle != []) {
            index = -1;
        }

        return index;
    }

    internal static int IndexOfSegment(
        ReadOnlySpan<byte> haystack,
        ReadOnlySpan<byte> needle,
        ReadOnlySpan<byte> separator) {
        var index = -1;
        var skipLength = needle.Length + separator.Length;

        if (!Contains(needle, separator)) {
            while (true) {
                var matchOffset = haystack.IndexOf(needle);
                index += matchOffset;

                // Remaining search space contains no more candidates.
                if (matchOffset < 0) {
                    index = -1;
                    break;
                }
                // Candidate is at the start of the search space.
                else if (matchOffset is 0) {
                    // Candidate either equals the search space or is followed by the separator.
                    if (haystack.Length == needle.Length ||
                        haystack.SliceUnsafe(needle.Length)
                                .StartsWith(separator)) {
                        break;
                    }
                }
                // Candidate is at the end of the search space.
                else if (matchOffset == haystack.Length - needle.Length) {
                    // Candidate is preceded by the separator.
                    if (haystack.SliceUnsafe(0, matchOffset)
                                .EndsWith(separator)) {
                        break;
                    }
                }
                // Candidate is in the middle of the search space.
                else if (
                    haystack.SliceUnsafe(0, matchOffset)
                            .EndsWith(separator) &&
                    haystack.SliceUnsafe(matchOffset + needle.Length)
                            .StartsWith(separator)) {
                    break;
                }

                // Candidate was not at the end of the search space and was
                // not followed by the separator, so we can skip separator.Length too.
                index += skipLength;
                haystack = haystack.SliceUnsafe(matchOffset + skipLength);
            }
        }

        return index;
    }
}
