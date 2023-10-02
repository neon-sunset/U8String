using System.Runtime.InteropServices;
using System.Text;

using U8Primitives.Abstractions;
using U8Primitives.InteropServices;

namespace U8Primitives;

#pragma warning disable IDE0046, IDE0057 // Why: range slicing and ternary expressions do not produce desired codegen
public readonly partial struct U8String
{
    public static U8String Concat(U8String left, byte right)
    {
        if (!U8Info.IsAsciiByte(right))
        {
            ThrowHelpers.ArgumentOutOfRange(nameof(right));
        }

        return U8Manipulation.ConcatUnchecked(left, right);
    }

    public static U8String Concat(U8String left, char right)
    {
        if (char.IsSurrogate(right))
        {
            ThrowHelpers.ArgumentOutOfRange(nameof(right));
        }

        return char.IsAscii(right)
            ? U8Manipulation.ConcatUnchecked(left, (byte)right)
            : U8Manipulation.ConcatUnchecked(left, U8Scalar.Create(right, checkAscii: false).AsSpan());
    }

    public static U8String Concat(U8String left, Rune right)
    {
        return right.IsAscii
            ? U8Manipulation.ConcatUnchecked(left, (byte)right.Value)
            : U8Manipulation.ConcatUnchecked(left, U8Scalar.Create(right, checkAscii: false).AsSpan());
    }

    public static U8String Concat(byte left, U8String right)
    {
        if (!U8Info.IsAsciiByte(left))
        {
            ThrowHelpers.ArgumentOutOfRange(nameof(left));
        }

        return U8Manipulation.ConcatUnchecked(left, right);
    }

    public static U8String Concat(char left, U8String right)
    {
        if (char.IsSurrogate(left))
        {
            ThrowHelpers.ArgumentOutOfRange(nameof(left));
        }

        return char.IsAscii(left)
            ? U8Manipulation.ConcatUnchecked((byte)left, right)
            : U8Manipulation.ConcatUnchecked(U8Scalar.Create(left, checkAscii: false).AsSpan(), right);
    }

    public static U8String Concat(Rune left, U8String right)
    {
        return left.IsAscii
            ? U8Manipulation.ConcatUnchecked((byte)left.Value, right)
            : U8Manipulation.ConcatUnchecked(U8Scalar.Create(left, checkAscii: false).AsSpan(), right);
    }

    // TODO: Optimize/deduplicate Concat variants
    // TODO: Investigate if it is possible fold validation for u8 literals
    public static U8String Concat(U8String left, U8String right)
    {
        if (!left.IsEmpty)
        {
            if (!right.IsEmpty)
            {
                return U8Manipulation.ConcatUnchecked(
                    left.UnsafeSpan,
                    right.UnsafeSpan);
            }

            return left;
        }

        return right;
    }

    public static U8String Concat(U8String left, ReadOnlySpan<byte> right)
    {
        if (!right.IsEmpty)
        {
            Validate(right);
            if (!left.IsEmpty)
            {
                return U8Manipulation.ConcatUnchecked(left.UnsafeSpan, right);
            }

            return new U8String(right, skipValidation: true);
        }

        return left;
    }

    public static U8String Concat(ReadOnlySpan<byte> left, U8String right)
    {
        if (!left.IsEmpty)
        {
            Validate(left);
            if (!right.IsEmpty)
            {
                return U8Manipulation.ConcatUnchecked(left, right.UnsafeSpan);
            }

            return new U8String(left, skipValidation: true);
        }

        return right;
    }

    public static U8String Concat(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var length = left.Length + right.Length;
        if (length > 0)
        {
            var value = new byte[length + 1];

            left.CopyTo(value);
            right.CopyTo(value.SliceUnsafe(left.Length, right.Length));

            Validate(value);
            return new U8String(value, 0, length);
        }

        return default;
    }

    public static U8String Concat(ReadOnlySpan<U8String> values)
    {
        if (values.Length > 1)
        {
            var length = 0;
            foreach (var value in values)
            {
                length += value.Length;
            }

            if (length > 0)
            {
                var value = new byte[length + 1];

                var offset = 0;
                ref var dst = ref value.AsRef();
                foreach (var source in values)
                {
                    source.AsSpan().CopyToUnsafe(ref dst.Add(offset));
                    offset += source.Length;
                }

                return new U8String(value, 0, length);
            }
        }

        return values.Length is 1 ? values[0] : default;
    }

    public static U8String Concat<T>(/* params */ ReadOnlySpan<T> values)
        where T : IUtf8SpanFormattable
    {
        return Concat(values, default, null);
    }

    public static U8String Concat<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null) where T : IUtf8SpanFormattable
    {
        // It would be nice to do this but 'struct' constraint on .Cast() does not allow for it
        // if (typeof(T) == typeof(U8String))
        // {
        //     return Concat(MemoryMarshal.Cast<T, U8String>(values));
        // }

        if (values.Length > 1)
        {
            using var builder = new ArrayBuilder();
            foreach (var value in values)
            {
                builder.Write(value, format, provider);
            }

            return new U8String(builder.Written, skipValidation: true);
        }

        return values.Length is 1 ? Create(values[0], format, provider) : default;
    }

    public static U8String Concat<T>(IEnumerable<T> values)
        where T : IUtf8SpanFormattable
    {
        return Concat(values, default, null);
    }

    public static U8String Concat<T>(
        IEnumerable<T> values,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null) where T : IUtf8SpanFormattable
    {
        if (values is T[] array)
        {
            return Concat(array, format, provider);
        }
        else if (values is List<T> list)
        {
            return Concat(list, format, provider);
        }
        else if (values.TryGetNonEnumeratedCount(out var count) && count is 1)
        {
            return Create(values.First(), format, provider);
        }

        using var builder = new ArrayBuilder();
        foreach (var value in values)
        {
            builder.Write(value, format, provider);
        }

        return new U8String(builder.Written, skipValidation: true);
    }

    /// <inheritdoc />
    public void CopyTo(byte[] destination, int index)
    {
        AsSpan().CopyTo(destination.AsSpan()[index..]);
    }

    public void CopyTo(Span<byte> destination)
    {
        AsSpan().CopyTo(destination);
    }

    public static U8String Join(byte separator, ReadOnlySpan<U8String> values)
    {
        if (!U8Info.IsAsciiByte(separator))
        {
            ThrowHelpers.ArgumentOutOfRange(nameof(separator));
        }

        if (values.Length > 1)
        {
            return U8Manipulation.JoinUnchecked(separator, values);
        }

        return values.Length is 1 ? values[0] : default;
    }

    public static U8String Join(char separator, ReadOnlySpan<U8String> values)
    {
        if (char.IsSurrogate(separator))
        {
            ThrowHelpers.ArgumentOutOfRange(nameof(separator));
        }

        return char.IsAscii(separator)
            ? Join((byte)separator, values)
            : JoinUnchecked(U8Scalar.Create(separator, checkAscii: false).AsSpan(), values);
    }

    public static U8String Join(Rune separator, ReadOnlySpan<U8String> values)
    {
        return separator.IsAscii
            ? Join((byte)separator.Value, values)
            : JoinUnchecked(U8Scalar.Create(separator, checkAscii: false).AsSpan(), values);
    }

    public static U8String Join(U8String separator, ReadOnlySpan<U8String> values)
    {
        return JoinUnchecked(separator, values);
    }

    public static U8String Join(ReadOnlySpan<byte> separator, ReadOnlySpan<U8String> values)
    {
        Validate(separator);

        return JoinUnchecked(separator, values);
    }

    public static U8String Join<T>(
        byte separator,
        ReadOnlySpan<T> values,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null) where T : IUtf8SpanFormattable
    {
        if (!U8Info.IsAsciiByte(separator))
        {
            ThrowHelpers.ArgumentOutOfRange(nameof(separator));
        }

        if (values.Length > 1)
        {
            return U8Manipulation.JoinUnchecked(separator, values, format, provider);
        }

        return values.Length is 1 ? Create(values[0]) : default;
    }

    public static U8String Join<T>(
        char separator,
        ReadOnlySpan<T> values,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null) where T : IUtf8SpanFormattable
    {
        if (char.IsSurrogate(separator))
        {
            ThrowHelpers.ArgumentOutOfRange(nameof(separator));
        }

        return char.IsAscii(separator)
            ? U8Manipulation.JoinUnchecked((byte)separator, values, format, provider)
            : U8Manipulation.JoinUnchecked(U8Scalar.Create(separator, checkAscii: false).AsSpan(), values, format, provider);
    }

    public static U8String Join<T>(
        Rune separator,
        ReadOnlySpan<T> values,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null) where T : IUtf8SpanFormattable
    {
        return separator.IsAscii
            ? U8Manipulation.JoinUnchecked((byte)separator.Value, values, format, provider)
            : U8Manipulation.JoinUnchecked(U8Scalar.Create(separator, checkAscii: false).AsSpan(), values, format, provider);
    }

    public static U8String Join<T>(
        ReadOnlySpan<byte> separator,
        ReadOnlySpan<T> values,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null) where T : IUtf8SpanFormattable
    {
        Validate(separator);

        if (values.Length > 1)
        {
            return separator.Length is 1
                ? U8Manipulation.JoinUnchecked(separator[0], values, format, provider)
                : U8Manipulation.JoinUnchecked(separator, values, format, provider);
        }

        return values.Length is 1 ? Create(values[0], format, provider) : default;
    }

    public static U8String Join<T>(
        byte separator,
        IEnumerable<T> values,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null) where T : IUtf8SpanFormattable
    {
        if (!U8Info.IsAsciiByte(separator))
        {
            ThrowHelpers.ArgumentOutOfRange(nameof(separator));
        }

        if (values is T[] array)
        {
            return Join<T>(separator, array.AsSpan(), format, provider);
        }
        else if (values is List<T> list)
        {
            return Join<T>(separator, CollectionsMarshal.AsSpan(list), format, provider);
        }
        else if (values.TryGetNonEnumeratedCount(out var count) && count is 1)
        {
            return Create(values.First(), format, provider);
        }
        else
        {
            return U8Manipulation.JoinUnchecked(separator, values, format, provider);
        }
    }

    public static U8String Join<T>(
        char separator,
        IEnumerable<T> values,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null) where T : IUtf8SpanFormattable
    {
        if (char.IsSurrogate(separator))
        {
            ThrowHelpers.ArgumentOutOfRange(nameof(separator));
        }

        return char.IsAscii(separator)
            ? Join((byte)separator, values, format, provider)
            : Join(U8Scalar.Create(separator, checkAscii: false).AsSpan(), values, format, provider);
    }

    public static U8String Join<T>(
        Rune separator,
        IEnumerable<T> values,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null) where T : IUtf8SpanFormattable
    {
        return separator.IsAscii
            ? Join((byte)separator.Value, values, format, provider)
            : Join(U8Scalar.Create(separator, checkAscii: false).AsSpan(), values, format, provider);
    }

    public static U8String Join<T>(
        ReadOnlySpan<byte> separator,
        IEnumerable<T> values,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null) where T : IUtf8SpanFormattable
    {
        Validate(separator);

        if (values is T[] array)
        {
            return Join<T>(separator, array.AsSpan(), format, provider);
        }
        else if (values is List<T> list)
        {
            return Join<T>(separator, CollectionsMarshal.AsSpan(list), format, provider);
        }
        else if (values.TryGetNonEnumeratedCount(out var count) && count is 1)
        {
            return Create(values.First(), format, provider);
        }
        else
        {
            return U8Manipulation.JoinUnchecked(separator, values, format, provider);
        }
    }

    internal static U8String JoinUnchecked(ReadOnlySpan<byte> separator, ReadOnlySpan<U8String> values)
    {
        if (values.Length > 1)
        {
            if (separator.Length > 1)
            {
                return U8Manipulation.JoinUnchecked(separator, values);
            }
            else if (separator.Length is 1)
            {
                return U8Manipulation.JoinUnchecked(separator[0], values);
            }

            return Concat(values);
        }

        return values.Length is 1 ? values[0] : default;
    }

    /// <summary>
    /// Normalizes current <see cref="U8String"/> to the specified Unicode normalization form (default: <see cref="NormalizationForm.FormC"/>).
    /// </summary>
    /// <returns>A new <see cref="U8String"/> normalized to the specified form.</returns>
    public U8String Normalize(NormalizationForm form = NormalizationForm.FormC)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc cref="Remove(U8String)"/>
    public U8String Remove(byte value) => U8Manipulation.Remove(this, value);

    /// <inheritdoc cref="Remove(U8String)"/>
    public U8String Remove(char value)
    {
        if (char.IsSurrogate(value))
        {
            ThrowHelpers.ArgumentOutOfRange(nameof(value));
        }

        return char.IsAscii(value)
            ? U8Manipulation.Remove(this, (byte)value)
            : U8Manipulation.Remove(this, U8Scalar.Create(value, checkAscii: false).AsSpan(), validate: false);
    }

    /// <inheritdoc cref="Remove(U8String)"/>
    public U8String Remove(Rune value) => value.IsAscii
        ? U8Manipulation.Remove(this, (byte)value.Value)
        : U8Manipulation.Remove(this, U8Scalar.Create(value, checkAscii: false).AsSpan());

    /// <inheritdoc cref="Remove(U8String)"/>
    public U8String Remove(ReadOnlySpan<byte> value) => value.Length is 1
        ? U8Manipulation.Remove(this, value[0])
        : U8Manipulation.Remove(this, value);

    /// <summary>
    /// Removes all occurrences of <paramref name="value"/> from the current <see cref="U8String"/>.
    /// </summary>
    /// <param name="value">The element to remove from the current <see cref="U8String"/>.</param>
    public U8String Remove(U8String value) => value.Length is 1
        ? U8Manipulation.Remove(this, value.UnsafeRef)
        : U8Manipulation.Remove(this, value, validate: false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public U8String Replace(byte oldValue, byte newValue)
    {
        return U8Manipulation.Replace(this, oldValue, newValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public U8String Replace(char oldValue, char newValue)
    {
        return U8Manipulation.Replace(this, oldValue, newValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public U8String Replace(Rune oldValue, Rune newValue)
    {
        return U8Manipulation.Replace(this, oldValue, newValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public U8String Replace(ReadOnlySpan<byte> oldValue, ReadOnlySpan<byte> newValue)
    {
        return U8Manipulation.Replace(this, oldValue, newValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public U8String Replace(U8String oldValue, U8String newValue)
    {
        return U8Manipulation.Replace(this, oldValue, newValue);
    }

    public U8String ReplaceLineEndings()
    {
        var source = this;
        if (!source.IsEmpty)
        {
            if (!OperatingSystem.IsWindows())
            {
                return U8Manipulation.ReplaceCore(
                    source, "\r\n"u8, "\n"u8, validate: false);
            }

            return ToCRLF();
        }

        return source;

        static U8String ToCRLF()
        {
            // TODO:
            // - Scan for LFs, find first one without CR
            // - Count LFs past first match and calculate max length
            // - Copy the first half before match, then split and insert segments from the second half
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Retrieves a substring from this instance. The substring starts at a specified
    /// character position and continues to the end of the string.
    /// </summary>
    /// <param name="start">The zero-based starting character position of a substring in this instance.</param>
    /// <returns>A substring view that begins at <paramref name="start"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="start"/> is less than zero or greater than the length of this instance.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The resulting substring splits at a UTF-8 code point boundary and would result in an invalid UTF-8 string.
    /// </exception>
    public U8String Slice(int start)
    {
        var source = this;
        if ((uint)start > (uint)source.Length)
        {
            ThrowHelpers.ArgumentOutOfRange();
        }

        var length = source.Length - start;
        if (length > 0)
        {
            if (U8Info.IsContinuationByte(in source.UnsafeRefAdd(start)))
            {
                ThrowHelpers.InvalidSplit();
            }

            return new(source._value, source.Offset + start, length);
        }

        return default;
    }

    /// <summary>
    /// Retrieves a substring from this instance. The substring starts at a specified
    /// character position and has a specified length.
    /// </summary>
    /// <param name="start">The zero-based starting character position of a substring in this instance.</param>
    /// <param name="length">The number of bytes in the substring.</param>
    /// <returns>A substring view that begins at <paramref name="start"/> and has <paramref name="length"/> bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="start"/> or <paramref name="length"/> is less than zero, or the sum of <paramref name="start"/> and <paramref name="length"/> is greater than the length of the current instance.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The resulting substring splits at a UTF-8 code point boundary and would result in an invalid UTF-8 string.
    /// </exception>
    public U8String Slice(int start, int length)
    {
        var source = this;
        // From ReadOnly/Span<T> Slice(int, int) implementation
        if ((uint)(start + length) > (uint)source.Length)
        {
            ThrowHelpers.ArgumentOutOfRange();
        }

        if (length > 0)
        {
            // TODO: Is there really no way to get rid of length < source.Length when checking the last+1 byte?
            if (U8Info.IsContinuationByte(source.UnsafeRefAdd(start)) || (
                length < source.Length && U8Info.IsContinuationByte(source.UnsafeRefAdd(start + length))))
            {
                // TODO: Exception message UX
                ThrowHelpers.InvalidSplit();
            }

            return new(source._value, source.Offset + start, length);
        }

        return default;
    }

    /// <summary>
    /// Removes all leading and trailing whitespace characters from the current string.
    /// </summary>
    /// <returns>
    /// A sub-slice that remains after all whitespace characters
    /// are removed from the start and end of the current string.
    /// </returns>
    public U8String Trim()
    {
        // TODO: Optimize fast path on no whitespace
        // TODO 2: Do not convert to runes and have proper
        // whitespace LUT to evaluate code points in a branchless way
        var source = this;
        if (!source.IsEmpty)
        {
            ref var ptr = ref source.UnsafeRef;

            var start = 0;
            while (start < source.Length)
            {
                if (!U8Info.IsWhitespaceRune(ref ptr.Add(start), out var size))
                {
                    break;
                }
                start += size;
            }

            var end = source.Length - 1;
            for (var endSearch = end; endSearch >= start; endSearch--)
            {
                var b = ptr.Add(endSearch);
                if (!U8Info.IsContinuationByte(b))
                {
                    if (U8Info.IsAsciiByte(b)
                        ? U8Info.IsAsciiWhitespace(b)
                        : U8Info.IsNonAsciiWhitespace(ref ptr.Add(end), out _))
                    {
                        // Save the last found whitespace code point offset and continue searching
                        // for more whitspace byte sequences from their end. If we don't do this,
                        // we will end up trimming away continuation bytes at the end of the string.
                        end = endSearch - 1;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return U8Marshal.Slice(source, start, end - start + 1);
        }

        return default;
    }

    /// <summary>
    /// Removes all leading whitespace characters from the current string.
    /// </summary>
    /// <returns>
    /// A sub-slice that remains after all whitespace characters
    /// are removed from the start of the current string.
    /// </returns>
    public U8String TrimStart()
    {
        var source = this;
        if (!source.IsEmpty)
        {
            ref var ptr = ref source.UnsafeRef;
            var b = ptr;

            if (U8Info.IsAsciiByte(b) && !U8Info.IsAsciiWhitespace(b))
            {
                return source;
            }

            var start = 0;
            while (start < source.Length)
            {
                if (!U8Info.IsWhitespaceRune(ref ptr.Add(start), out var size))
                {
                    break;
                }
                start += size;
            }

            return U8Marshal.Slice(source, start);
        }

        return default;
    }

    /// <summary>
    /// Removes all trailing whitespace characters from the current string.
    /// </summary>
    /// <returns>
    /// A sub-slice that remains after all whitespace characters
    /// are removed from the end of the current string.
    /// </returns>
    public U8String TrimEnd()
    {
        var source = this;
        if (!source.IsEmpty)
        {
            ref var ptr = ref source.UnsafeRef;

            var end = source.Length - 1;
            for (var endSearch = end; endSearch >= 0; endSearch--)
            {
                var b = ptr.Add(endSearch);
                if (!U8Info.IsContinuationByte(b))
                {
                    if (U8Info.IsAsciiByte(b)
                        ? U8Info.IsAsciiWhitespace(b)
                        : U8Info.IsNonAsciiWhitespace(ref ptr.Add(end), out _))
                    {
                        end = endSearch - 1;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return U8Marshal.Slice(source, 0, end + 1);
        }

        return default;
    }

    /// <summary>
    /// Removes all leading and trailing ASCII whitespace characters from the current string.
    /// </summary>
    /// <returns>
    /// A sub-slice that remains after all ASCII whitespace characters
    /// are removed from the start and end of the current string.
    /// </returns>
    public U8String TrimAscii()
    {
        var source = this;
        var range = Ascii.Trim(source);

        return U8Marshal.Slice(source, range);
    }

    /// <summary>
    /// Removes all the leading ASCII whitespace characters from the current string.
    /// </summary>
    /// <returns>
    /// A sub-slice that remains after all whitespace characters
    /// are removed from the start of the current string.
    /// </returns>
    public U8String TrimStartAscii()
    {
        var source = this;
        var range = Ascii.TrimStart(source);

        return U8Marshal.Slice(source, range);
    }

    /// <summary>
    /// Removes all the trailing ASCII whitespace characters from the current string.
    /// </summary>
    /// <returns>
    /// A sub-slice that remains after all whitespace characters
    /// are removed from the end of the current string.
    /// </returns>
    public U8String TrimEndAscii()
    {
        var source = this;
        var range = Ascii.TrimEnd(source);

        return U8Marshal.Slice(source, range);
    }

    // TODO:
    // - Complete impl. depends on porting of InlineArray-based array builder for letters
    // which have different lengths in upper/lower case.
    // - Remove/rename to ToLowerFallback or move to something like "FallbackInvariantComparer"
    // clearly indicating it being slower and inferior alternative to proper implementations
    // which call into ICU/NLS/Hybrid-provided case change exports.
    public U8String ToLower<T>(T converter)
        where T : IU8CaseConverter
    {
        // 1. Estimate the start offset of the conversion (first char requiring case change)
        // 2. Estimate the length of the conversion (the length of the resulting segment after case change)
        // 3. Allocate the resulting buffer and copy the pre-offset segment
        // 4. Perform the conversion which writes to the remainder segment of the buffer
        // 5. Return the resulting buffer as a new string

        var deref = this;
        if (!deref.IsEmpty)
        {
            var trusted = U8CaseConversion.IsTrustedImplementation(converter);
            var source = deref.UnsafeSpan;

            var (replaceStart, resultLength) = converter.LowercaseHint(source);

            int convertedLength;
            if ((uint)replaceStart < (uint)source.Length)
            {
                var lowercase = new byte[resultLength + 1];
                var destination = lowercase.AsSpan();

                if (trusted)
                {
                    source
                        .SliceUnsafe(0, replaceStart)
                        .CopyTo(destination.SliceUnsafe(0, source.Length));
                    source = source.SliceUnsafe(replaceStart);
                    destination = destination.SliceUnsafe(replaceStart, source.Length);

                    convertedLength = converter.ToLower(source, destination) + replaceStart;
                }
                else
                {
                    source[..replaceStart]
                        .CopyTo(destination.SliceUnsafe(0, source.Length));
                    source = source.Slice(replaceStart);
                    destination = destination.Slice(replaceStart, source.Length);

                    convertedLength = converter.ToLower(source, destination) + replaceStart;

                    if (convertedLength > resultLength)
                    {
                        // TODO: EH UX
                        ThrowHelpers.ArgumentOutOfRange();
                    }
                }

                return new U8String(lowercase, 0, convertedLength);
            }
        }

        return deref;
    }

    public U8String ToUpper<T>(T converter)
        where T : IU8CaseConverter
    {
        var deref = this;
        if (!deref.IsEmpty)
        {
            var trusted = U8CaseConversion.IsTrustedImplementation(converter);
            var source = deref.UnsafeSpan;

            var (replaceStart, resultLength) = converter.UppercaseHint(source);

            int convertedLength;
            if ((uint)replaceStart < (uint)source.Length)
            {
                var uppercase = new byte[resultLength + 1];
                var destination = uppercase.AsSpan();

                if (trusted)
                {
                    source
                        .SliceUnsafe(0, replaceStart)
                        .CopyTo(destination.SliceUnsafe(0, source.Length));
                    source = source.SliceUnsafe(replaceStart);
                    destination = destination.SliceUnsafe(replaceStart, source.Length);

                    convertedLength = converter.ToUpper(source, destination) + replaceStart;
                }
                else
                {
                    source[..replaceStart]
                        .CopyTo(destination.SliceUnsafe(0, source.Length));
                    source = source.Slice(replaceStart);
                    destination = destination.Slice(replaceStart, source.Length);

                    convertedLength = converter.ToUpper(source, destination) + replaceStart;

                    if (convertedLength > resultLength)
                    {
                        // TODO: EH UX
                        ThrowHelpers.ArgumentOutOfRange();
                    }
                }

                return new U8String(uppercase, 0, convertedLength);
            }
        }

        return deref;
    }

    // TODO: docs
    public U8String ToLowerAscii()
    {
        return ToLower(U8CaseConversion.Ascii);
    }

    public U8String ToUpperAscii()
    {
        return ToUpper(U8CaseConversion.Ascii);
    }
}
