using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using U8.Abstractions;
using U8.Primitives;
using U8.Shared;

#pragma warning disable RCS1085, RCS1085FadeOut, IDE0032 // Use auto-implemented property. Why: readable fields.
namespace U8;

public static class U8SplitOptions {
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct TrimOptions: IU8SplitOptions {
        public static bool Trim => true;
        public static bool RemoveEmpty => false;
    }

    public static TrimOptions Trim => default;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct RemoveEmptyOptions: IU8SplitOptions {
        public static bool Trim => false;
        public static bool RemoveEmpty => true;
    }

    public static RemoveEmptyOptions RemoveEmpty => default;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct TrimRemoveEmptyOptions: IU8SplitOptions {
        public static bool Trim => true;
        public static bool RemoveEmpty => true;
    }

    public static TrimRemoveEmptyOptions TrimRemoveEmpty => default;
}

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IU8SplitOptions {
    static abstract bool Trim { get; }
    static abstract bool RemoveEmpty { get; }
}

public readonly partial struct U8String {
    // Does not get inlined on NativeAOT otherwise which is a codegen size regression.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public U8SplitPair SplitFirst(byte separator) {
        ThrowHelpers.CheckAscii(separator);

        var source = this;
        var segment = source._inner;
        var remainder = default(U8Range);

        if (!source.IsEmpty) {
            var span = source.UnsafeSpan;
            var index = span.IndexOf(separator);
            if (index >= 0) {
                remainder = new(
                    segment.Offset + index + 1,
                    segment.Length - index - 1);
                segment = new(segment.Offset, index);
            }
        }

        return new(source._value, segment, remainder);
    }

    public U8SplitPair SplitFirst(char separator) {
        ThrowHelpers.CheckSurrogate(separator);

        if (char.IsAscii(separator)) {
            return SplitFirst((byte)separator);
        }

        return SplitFirstUnchecked(
            separator <= 0x7FF ? separator.AsTwoBytes() : separator.AsThreeBytes());
    }

    public U8SplitPair SplitFirst(Rune separator) {
        if (separator.IsAscii) {
            return SplitFirst((byte)separator.Value);
        }

        return SplitFirstUnchecked(separator.Value switch {
            <= 0x7FF => separator.AsTwoBytes(),
            <= 0xFFFF => separator.AsThreeBytes(),
            _ => separator.AsFourBytes(),
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public U8SplitPair SplitFirst(U8String separator) {
        var source = this;
        var segment = source._inner;
        var remainder = default(U8Range);

        if (!separator.IsEmpty) {
            if (!source.IsEmpty) {
                var span = source.UnsafeSpan;
                var index = span.IndexOf(separator.UnsafeSpan);
                if (index >= 0) {
                    remainder = new(
                        segment.Offset + index + separator.Length,
                        segment.Length - index - separator.Length);
                    segment = new(segment.Offset, index);
                }
            }
        }
        else {
            (segment, remainder) = (default, segment);
        }

        return new(source._value, segment, remainder);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public U8SplitPair SplitFirst(ReadOnlySpan<byte> separator) {
        Validate(separator);

        var source = this;
        var segment = source._inner;
        var remainder = default(U8Range);

        if (!source.IsEmpty) {
            var index = source.UnsafeSpan.IndexOf(separator);
            if (index >= 0) {
                remainder = new(
                    segment.Offset + index + separator.Length,
                    segment.Length - index - separator.Length);
                segment = new(segment.Offset, index);
            }
        }

        return new(source._value, segment, remainder);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal U8SplitPair SplitFirstUnchecked(ReadOnlySpan<byte> separator) {
        Debug.Assert(!separator.IsEmpty);

        var source = this;
        var segment = source._inner;
        var remainder = default(U8Range);

        if (!source.IsEmpty) {
            var span = source.UnsafeSpan;
            var index = span.IndexOf(separator);
            if (index >= 0) {
                remainder = new(
                    segment.Offset + index + separator.Length,
                    segment.Length - index - separator.Length);
                segment = new(segment.Offset, index);
            }
        }

        return new(source._value, segment, remainder);
    }

    public U8SplitPair SplitFirst<T>(byte separator, T comparer)
        where T : IU8IndexOfOperator {
        ThrowHelpers.CheckAscii(separator);

        var source = this;
        if (!source.IsEmpty) {
            var (index, stride) = comparer.IndexOf(source.UnsafeSpan, separator);

            if (index >= 0) {
                return new(source, index, stride);
            }
        }

        return U8SplitPair.NotFound(source);
    }

    public U8SplitPair SplitFirst<T>(char separator, T comparer)
    where T : IU8IndexOfOperator {
        ThrowHelpers.CheckSurrogate(separator);

        if (char.IsAscii(separator)) {
            return SplitFirst((byte)separator, comparer);
        }

        return SplitFirstUnchecked(new U8Scalar(separator, checkAscii: false).AsSpan(), comparer);
    }

    public U8SplitPair SplitFirst<T>(Rune separator, T comparer)
    where T : IU8IndexOfOperator {
        if (separator.IsAscii) {
            return SplitFirst((byte)separator.Value, comparer);
        }

        return SplitFirstUnchecked(new U8Scalar(separator, checkAscii: false).AsSpan(), comparer);
    }

    internal U8SplitPair SplitFirst<T>(U8String separator, T comparer)
    where T : IU8IndexOfOperator {
        var source = this;
        if (!source.IsEmpty && !separator.IsEmpty) {
            var (index, stride) = U8Searching.IndexOf(source.UnsafeSpan, separator.UnsafeSpan, comparer);

            if (index >= 0) {
                return new(source, index, stride);
            }
        }

        return U8SplitPair.NotFound(source);
    }

    public U8SplitPair SplitFirst<T>(ReadOnlySpan<byte> separator, T comparer)
    where T : IU8IndexOfOperator {
        var source = this;
        if (!source.IsEmpty && separator.Length > 0) {
            var (index, stride) = U8Searching.IndexOf(source.UnsafeSpan, separator, comparer);

            if (index >= 0) {
                // Same as with Slice(int, int), this might dereference past the end of the string.
                // TODO: Do something about it if it's ever an issue.
                var end = index + stride;
                if (U8Info.IsContinuationByte(source.UnsafeRefAdd(index)) || (end < source.Length &&
                    U8Info.IsContinuationByte(source.UnsafeRefAdd(end)))) {
                    ThrowHelpers.InvalidSplit();
                }

                return new(source, index, stride);
            }
        }

        return U8SplitPair.NotFound(source);
    }

    // TODO: SplitFirst/Last optimization treatment; consider \0 + .Empty edge cases
    internal U8SplitPair SplitFirstUnchecked<T>(ReadOnlySpan<byte> separator, T comparer)
    where T : IU8IndexOfOperator {
        Debug.Assert(separator.Length > 0);

        var source = this;
        if (!source.IsEmpty) {
            var (index, stride) = U8Searching.IndexOf(source.UnsafeSpan, separator, comparer);

            if (index >= 0) {
                return new(source, index, stride);
            }
        }

        return U8SplitPair.NotFound(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public U8SplitPair SplitLast(byte separator) {
        ThrowHelpers.CheckAscii(separator);

        var source = this;
        var segment = source._inner;
        var remainder = default(U8Range);

        if (!source.IsEmpty) {
            var span = source.UnsafeSpan;
            var index = span.LastIndexOf(separator);
            if (index >= 0) {
                remainder = new(
                    segment.Offset + index + 1,
                    segment.Length - index - 1);
                segment = new(segment.Offset, index);
            }
        }

        return new(source._value, segment, remainder);
    }

    public U8SplitPair SplitLast(char separator) {
        ThrowHelpers.CheckSurrogate(separator);

        if (char.IsAscii(separator)) {
            return SplitLast((byte)separator);
        }

        return SplitLastUnchecked(
            separator <= 0x7FF ? separator.AsTwoBytes() : separator.AsThreeBytes());
    }

    public U8SplitPair SplitLast(Rune separator) {
        if (separator.IsAscii) {
            return SplitLast((byte)separator.Value);
        }

        return SplitLastUnchecked(separator.Value switch {
            <= 0x7FF => separator.AsTwoBytes(),
            <= 0xFFFF => separator.AsThreeBytes(),
            _ => separator.AsFourBytes(),
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public U8SplitPair SplitLast(U8String separator) {
        var source = this;
        var segment = source._inner;
        var remainder = default(U8Range);

        if (!separator.IsEmpty) {
            if (!source.IsEmpty) {
                var span = source.UnsafeSpan;
                var index = span.LastIndexOf(separator.UnsafeSpan);
                if (index >= 0) {
                    remainder = new(
                        segment.Offset + index + separator.Length,
                        segment.Length - index - separator.Length);
                    segment = new(segment.Offset, index);
                }
            }
        }
        else {
            (segment, remainder) = (default, segment);
        }

        return new(source._value, segment, remainder);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public U8SplitPair SplitLast(ReadOnlySpan<byte> separator) {
        Validate(separator);

        var source = this;
        var segment = source._inner;
        var remainder = default(U8Range);

        if (!source.IsEmpty) {
            var index = source.UnsafeSpan.LastIndexOf(separator);
            if (index >= 0) {
                remainder = new(
                    segment.Offset + index + separator.Length,
                    segment.Length - index - separator.Length);
                segment = new(segment.Offset, index);
            }
        }

        return new(source._value, segment, remainder);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal U8SplitPair SplitLastUnchecked(ReadOnlySpan<byte> separator) {
        var source = this;
        var segment = source._inner;
        var remainder = default(U8Range);

        if (!source.IsEmpty) {
            var span = source.UnsafeSpan;
            var index = span.LastIndexOf(separator);
            if (index >= 0) {
                remainder = new(
                    segment.Offset + index + separator.Length,
                    segment.Length - index - separator.Length);
                segment = new(segment.Offset, index);
            }
        }

        return new(source._value, segment, remainder);
    }

    public U8SplitPair SplitLast<T>(byte separator, T comparer)
    where T : IU8LastIndexOfOperator {
        ThrowHelpers.CheckAscii(separator);

        var source = this;
        if (!source.IsEmpty) {
            var (index, stride) = comparer.LastIndexOf(source.UnsafeSpan, separator);

            if (index >= 0) {
                return new(source, index, stride);
            }
        }

        return U8SplitPair.NotFound(source);
    }

    public U8SplitPair SplitLast<T>(char separator, T comparer)
    where T : IU8LastIndexOfOperator {
        ThrowHelpers.CheckSurrogate(separator);

        if (char.IsAscii(separator)) {
            return SplitLast((byte)separator, comparer);
        }

        return SplitLastUnchecked(new U8Scalar(separator, checkAscii: false).AsSpan(), comparer);
    }

    public U8SplitPair SplitLast<T>(Rune separator, T comparer)
    where T : IU8LastIndexOfOperator {
        if (separator.IsAscii) {
            return SplitLast((byte)separator.Value, comparer);
        }

        return SplitLastUnchecked(new U8Scalar(separator, checkAscii: false).AsSpan(), comparer);
    }

    internal U8SplitPair SplitLast<T>(U8String separator, T comparer)
    where T : IU8LastIndexOfOperator {
        var source = this;
        if (!source.IsEmpty) {
            if (!separator.IsEmpty) {
                var (index, stride) = U8Searching.LastIndexOf(source.UnsafeSpan, separator.UnsafeSpan, comparer);

                if (index >= 0) {
                    return new(source, index, stride);
                }
            }
        }

        return U8SplitPair.NotFound(source);
    }

    public U8SplitPair SplitLast<T>(ReadOnlySpan<byte> separator, T comparer)
    where T : IU8LastIndexOfOperator {
        var source = this;
        if (!source.IsEmpty && separator.Length > 0) {
            var (index, stride) = U8Searching.LastIndexOf(source.UnsafeSpan, separator, comparer);

            if (index >= 0) {
                var end = index + stride;
                if (U8Info.IsContinuationByte(source.UnsafeRefAdd(index)) || (end < source.Length &&
                    U8Info.IsContinuationByte(source.UnsafeRefAdd(end)))) {
                    ThrowHelpers.InvalidSplit();
                }

                return new(source, index, stride);
            }
        }

        return U8SplitPair.NotFound(source);
    }

    internal U8SplitPair SplitLastUnchecked<T>(ReadOnlySpan<byte> separator, T comparer)
    where T : IU8LastIndexOfOperator {
        Debug.Assert(separator.Length > 0);

        var source = this;
        if (!source.IsEmpty) {
            var (index, stride) = U8Searching.LastIndexOf(source.UnsafeSpan, separator, comparer);

            if (index >= 0) {
                return new(source, index, stride);
            }
        }

        return U8SplitPair.NotFound(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public U8SplitPair SplitAt(int index) {
        var source = this;
        if ((uint)index > (uint)source.Length) {
            ThrowHelpers.ArgumentOutOfRange();
        }

        if (index < source.Length
            && U8Info.IsContinuationByte(source.UnsafeRefAdd(index))) {
            ThrowHelpers.ArgumentOutOfRange();
        }

        return new(source, index, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public U8SplitPair SplitAt(Index index) {
        var source = this;
        var offset = index.GetOffset(source.Length);
        if ((uint)offset > (uint)source.Length) {
            ThrowHelpers.ArgumentOutOfRange();
        }

        if (offset < source.Length
            && U8Info.IsContinuationByte(source.UnsafeRefAdd(offset))) {
            ThrowHelpers.ArgumentOutOfRange();
        }

        return new(source, offset, 0);
    }

    public U8Split<byte> Split(byte separator) {
        ThrowHelpers.CheckAscii(separator);

        return new(this, separator);
    }

    public U8Split<char> Split(char separator) {
        ThrowHelpers.CheckSurrogate(separator);

        return new(this, separator);
    }

    public U8Split<Rune> Split(Rune separator) {
        return new(this, separator);
    }

    public U8Split Split(U8String separator) {
        return new(this, separator);
    }

    public U8RefSplit Split(ReadOnlySpan<byte> separator) {
        Validate(separator);

        return new(this, separator);
    }

    // TODO: Consider aggregating multiple interfaces into a single IU8Searcher (better name???) interface.
    public U8Split<byte, T> Split<T>(byte separator, T comparer)
    where T : IU8Comparer {
        ThrowHelpers.CheckAscii(separator);

        return new(this, separator, comparer);
    }

    public U8Split<char, T> Split<T>(char separator, T comparer)
    where T : IU8Comparer {
        ThrowHelpers.CheckSurrogate(separator);

        return new(this, separator, comparer);
    }

    public U8Split<Rune, T> Split<T>(Rune separator, T comparer)
    where T : IU8Comparer {
        return new(this, separator, comparer);
    }

    public U8Split<U8String, T> Split<T>(U8String separator, T comparer)
    where T : IU8Comparer {
        return new(this, separator, comparer);
    }

    public U8RefSplit<T> Split<T>(ReadOnlySpan<byte> separator, T comparer)
    where T : IU8Comparer {
        Validate(separator);

        return new(this, separator, comparer);
    }

    public U8RefAnySplit SplitAny(ReadOnlySpan<byte> separators) {
        if (!Ascii.IsValid(separators)) {
            ThrowHelpers.ArgumentException();
        }

        return new(this, separators);
    }
}

[SuppressMessage("", "IDE0060: Unused paramter 'options'", Justification = "It is used for generic signature inference.")]
[SuppressMessage("", "RCS1163: Unused paramter 'options'", Justification = "It is used for generic signature inference.")]
public static class U8SplitExtensions {
    // Behold, overload resolution oriented programming
    public static ConfiguredU8Split<byte, TOptions> Split<TOptions>(this U8String value, byte separator, TOptions options)
        where TOptions : unmanaged, IU8SplitOptions {
        ThrowHelpers.CheckAscii(separator);

        return new(value, separator);
    }

    public static ConfiguredU8Split<char, TOptions> Split<TOptions>(this U8String value, char separator, TOptions options)
        where TOptions : unmanaged, IU8SplitOptions {
        ThrowHelpers.CheckSurrogate(separator);

        return new(value, separator);
    }

    public static ConfiguredU8Split<Rune, TOptions> Split<TOptions>(this U8String value, Rune separator, TOptions options)
        where TOptions : unmanaged, IU8SplitOptions {
        return new(value, separator);
    }

    public static ConfiguredU8Split<U8String, TOptions> Split<TOptions>(this U8String value, U8String separator, TOptions options)
        where TOptions : unmanaged, IU8SplitOptions {
        return new(value, separator);
    }

    public static ConfiguredU8Split<byte, TOptions, TComparer> Split<TComparer, TOptions>(
        this U8String value, byte separator, TOptions options, TComparer comparer)
    where TOptions : unmanaged, IU8SplitOptions
    where TComparer : IU8ContainsOperator, IU8CountOperator, IU8IndexOfOperator {
        ThrowHelpers.CheckAscii(separator);

        return new(value, separator, comparer);
    }

    public static ConfiguredU8Split<char, TOptions, TComparer> Split<TComparer, TOptions>(
        this U8String value, char separator, TOptions options, TComparer comparer)
    where TOptions : unmanaged, IU8SplitOptions
    where TComparer : IU8ContainsOperator, IU8CountOperator, IU8IndexOfOperator {
        ThrowHelpers.CheckSurrogate(separator);

        return new(value, separator, comparer);
    }

    public static ConfiguredU8Split<Rune, TOptions, TComparer> Split<TComparer, TOptions>(
        this U8String value, Rune separator, TOptions options, TComparer comparer)
    where TOptions : unmanaged, IU8SplitOptions
    where TComparer : IU8ContainsOperator, IU8CountOperator, IU8IndexOfOperator {
        return new(value, separator, comparer);
    }

    public static ConfiguredU8Split<U8String, TOptions, TComparer> Split<TComparer, TOptions>(
        this U8String value, U8String separator, TOptions options, TComparer comparer)
    where TOptions : unmanaged, IU8SplitOptions
    where TComparer : IU8ContainsOperator, IU8CountOperator, IU8IndexOfOperator {
        return new(value, separator, comparer);
    }

    public static ConfiguredU8RefSplit<TOptions> Split<TOptions>(
        this U8String value, ReadOnlySpan<byte> separator, TOptions options)
    where TOptions : unmanaged, IU8SplitOptions {
        U8String.Validate(separator);

        return new(value, separator);
    }

    public static ConfiguredU8RefSplit<TOptions, TComparer> Split<TComparer, TOptions>(
        this U8String value, ReadOnlySpan<byte> separator, TOptions options, TComparer comparer)
    where TOptions : unmanaged, IU8SplitOptions
    where TComparer : IU8ContainsOperator, IU8CountOperator, IU8IndexOfOperator {
        U8String.Validate(separator);

        return new(value, separator, comparer);
    }
}
