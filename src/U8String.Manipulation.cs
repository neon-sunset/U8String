using System.Buffers;

using U8Primitives.InteropServices;

namespace U8Primitives;

#pragma warning disable IDE0046, IDE0057 // Why: range slicing and ternary expressions do not produce desired codegen
public readonly partial struct U8String
{
    // TODO: Optimize/deduplicate Concat variants
    // TODO: Investigate if it is possible fold validation for u8 literals
    public static U8String Concat(U8String left, U8String right)
    {
        if (!left.IsEmpty)
        {
            if (!right.IsEmpty)
            {
                var length = left.Length + right.Length;
                var value = new byte[length];

                left.UnsafeSpan.CopyTo(value);
                right.UnsafeSpan.CopyTo(value.AsSpan(left.Length));

                return new U8String(value, 0, length);
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
                var length = left.Length + right.Length;
                var value = new byte[length];

                left.UnsafeSpan.CopyTo(value);
                right.CopyTo(value.AsSpan(left.Length));

                return new U8String(value, 0, length);
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
                var length = left.Length + right.Length;
                var value = new byte[length];

                left.CopyTo(value);
                right.UnsafeSpan.CopyTo(value.AsSpan(left.Length));

                return new U8String(value, 0, length);
            }

            return new U8String(left, skipValidation: true);
        }

        return right;
    }

    public static U8String Concat(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var length = left.Length + right.Length;
        if (length != 0)
        {
            var value = new byte[length];

            left.CopyTo(value);
            right.CopyTo(value.SliceUnsafe(left.Length, right.Length));

            Validate(value);
            return new U8String(value, 0, length);
        }

        return default;
    }

    public U8String Replace(byte oldValue, byte newValue)
    {
        var source = this;
        if (!source.IsEmpty)
        {
            var current = source.UnsafeSpan;
            var firstReplace = current.IndexOf(oldValue);
            if (firstReplace < 0)
            {
                return this;
            }

            var replaced = new byte[source.Length];
            var destination = replaced.SliceUnsafe(
                firstReplace, source.Length - firstReplace);

            current[firstReplace..].Replace(destination, oldValue, newValue);

            // Pass to ctor which will perform validation.
            // Old and new bytes which individually are invalid unicode scalar values are allowed
            // if the replacement produces a valid UTF-8 sequence.
            return new U8String(replaced);
        }

        return default;
    }

    // Selectively inlining some overloads which are expected
    // to take byte or utf-8 constant literals.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (U8String Segment, U8String Remainder) SplitFirst(byte separator)
    {
        if (!Rune.IsValid(separator))
        {
            // TODO: EH UX
            ThrowHelpers.ArgumentOutOfRange();
        }

        var source = this;
        if (!source.IsEmpty)
        {
            var span = source.UnsafeSpan;
            var index = span.IndexOf(separator);
            if (index >= 0)
            {
                return (
                    U8Marshal.Slice(source, 0, index),
                    U8Marshal.Slice(source, index + 1));
            }

            return (this, default);
        }

        return default;
    }

    public (U8String Segment, U8String Remainder) SplitFirst(Rune separator)
    {
        var source = this;
        if (!source.IsEmpty)
        {
            var separatorBytes = (stackalloc byte[4]);
            var separatorLength = separator.EncodeToUtf8(separatorBytes);

            var span = source.UnsafeSpan;
            var index = span.IndexOf(separatorBytes[..separatorLength]);
            if (index >= 0)
            {
                return (
                    U8Marshal.Slice(source, 0, index),
                    U8Marshal.Slice(source, index + separatorLength));
            }

            return (source, default);
        }

        return default;
    }

    // TODO: Reconsider the behavior on empty separator - what do Rust and Go do?
    // Should an empty separator effectively match no bytes which would be at the
    // start of the string, putting source in the remainder? (same with SplitLast and ROS overloads)
    public (U8String Segment, U8String Remainder) SplitFirst(U8String separator)
    {
        var source = this;
        if (!source.IsEmpty)
        {
            if (!separator.IsEmpty)
            {
                var span = source.UnsafeSpan;
                var index = span.IndexOf(separator.UnsafeSpan);
                if (index >= 0)
                {
                    return (
                        U8Marshal.Slice(source, 0, index),
                        U8Marshal.Slice(source, index + separator.Length));
                }
            }

            return (source, default);
        }

        return default;
    }

    // TODO 1: Investigate if checked slicing can be avoided.
    // TODO 2: Write remarks noting that invalid separator is allowed
    // if the final split produces two valid UTF-8 sequences (SplitLast too).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (U8String Segment, U8String Remainder) SplitFirst(ReadOnlySpan<byte> separator)
    {
        var source = this;
        if (!source.IsEmpty)
        {
            if (!separator.IsEmpty)
            {
                var span = source.UnsafeSpan;
                var index = span.IndexOf(separator);
                if (index >= 0)
                {
                    // By design: validate both slices.
                    return (
                        source.Slice(0, index),
                        source.Slice(index + separator.Length));
                }
            }

            return (source, default);
        }

        return default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (U8String Segment, U8String Remainder) SplitLast(byte separator)
    {
        if (!Rune.IsValid(separator))
        {
            // TODO: EH UX
            ThrowHelpers.ArgumentOutOfRange();
        }

        var source = this;
        if (!source.IsEmpty)
        {
            var span = source.UnsafeSpan;
            var index = span.LastIndexOf(separator);
            if (index >= 0)
            {
                return (
                    U8Marshal.Slice(source, 0, index),
                    U8Marshal.Slice(source, index + 1));
            }

            return (source, default);
        }

        return default;
    }

    public (U8String Segment, U8String Remainder) SplitLast(Rune separator)
    {
        var source = this;
        if (!source.IsEmpty)
        {
            var separatorBytes = (stackalloc byte[4]);
            var separatorLength = separator.EncodeToUtf8(separatorBytes);

            var span = source.UnsafeSpan;
            var index = span.LastIndexOf(separatorBytes[..separatorLength]);
            if (index >= 0)
            {
                return (
                    U8Marshal.Slice(source, 0, index),
                    U8Marshal.Slice(source, index + separatorLength));
            }

            return (source, default);
        }

        return default;
    }

    public (U8String Segment, U8String Remainder) SplitLast(U8String separator)
    {
        var source = this;
        if (!source.IsEmpty)
        {
            if (!separator.IsEmpty)
            {
                var span = source.UnsafeSpan;
                var index = span.LastIndexOf(separator.UnsafeSpan);
                if (index >= 0)
                {
                    return (
                        U8Marshal.Slice(source, 0, index),
                        U8Marshal.Slice(source, index + separator.Length));
                }
            }

            return (source, default);
        }

        return default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (U8String Segment, U8String Remainder) SplitLast(ReadOnlySpan<byte> separator)
    {
        var source = this;
        if (!source.IsEmpty)
        {
            if (!separator.IsEmpty)
            {
                var span = source.UnsafeSpan;
                var index = span.LastIndexOf(separator);
                return index >= 0
                    // By design: validate both slices.
                    ? (source.Slice(0, index), source.Slice(index + separator.Length))
                    : (source, default);
            }

            return (source, default);
        }

        return default;
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
        // From ReadOnly/Span<T> Slice(int) implementation
        if ((ulong)(uint)start > (ulong)(uint)source.Length)
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
        if ((ulong)(uint)start + (ulong)(uint)length > (ulong)(uint)source.Length)
        {
            ThrowHelpers.ArgumentOutOfRange();
        }

        if (length > 0)
        {
            //ref var firstByte = ref source.FirstByte;
            if (U8Info.IsContinuationByte(source.UnsafeRefAdd(start)) ||
                U8Info.IsContinuationByte(source.UnsafeRefAdd(start + length)))
            {
                // TODO: Exception message UX
                ThrowHelpers.InvalidSplit();
            }

            return new(source._value, source.Offset + start, length);
        }

        return default;
    }

    /// <summary>
    /// Removes all leading and trailing ASCII white-space characters from the current string.
    /// </summary>
    /// <returns>
    /// A substring that remains after all ASCII white-space characters
    /// are removed from the start and end of the current string.
    /// </returns>
    public U8String TrimAscii()
    {
        var source = this;
        var range = Ascii.Trim(source);

        return !range.IsEmpty()
            ? U8Marshal.Slice(source, range)
            : default;
    }

    /// <summary>
    /// Removes all the leading ASCII white-space characters from the current string.
    /// </summary>
    /// <returns>
    /// A substring that remains after all white-space characters
    /// are removed from the start of the current string.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public U8String TrimStartAscii()
    {
        var source = this;
        var range = Ascii.TrimStart(source);

        return !range.IsEmpty()
            ? U8Marshal.Slice(source, range)
            : default;
    }

    /// <summary>
    /// Removes all the trailing ASCII white-space characters from the current string.
    /// </summary>
    /// <returns>
    /// A substring that remains after all white-space characters
    /// are removed from the end of the current string.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public U8String TrimEndAscii()
    {
        var source = this;
        var range = Ascii.TrimEnd(source);

        return !range.IsEmpty()
            ? U8Marshal.Slice(source, range)
            : default;
    }

    /// <summary>
    /// Returns a copy of this ASCII string converted to lower case.
    /// </summary>
    /// <returns>A lowercase equivalent of the current ASCII string.</returns>
    /// <exception cref="ArgumentException">
    /// The current string is not a valid ASCII sequence.
    /// </exception>
    public U8String ToLowerAscii()
    {
        var source = this;
        if (!source.IsEmpty)
        {
            var span = source.UnsafeSpan;
            var destination = new byte[span.Length];
            var result = Ascii.ToLower(span, destination, out _);
            if (result is OperationStatus.InvalidData)
            {
                ThrowHelpers.InvalidAscii();
            }

            return new U8String(destination, 0, span.Length);
        }

        return default;
    }

    /// <summary>
    /// Returns a copy of this ASCII string converted to upper case.
    /// </summary>
    /// <returns>The uppercase equivalent of the current ASCII string.</returns>
    /// <exception cref="ArgumentException">
    /// The current string is not a valid ASCII sequence.
    /// </exception>
    public U8String ToUpperAscii()
    {
        var source = this;
        if (!source.IsEmpty)
        {
            var span = source.UnsafeSpan;
            var destination = new byte[span.Length];
            var result = Ascii.ToUpper(span, destination, out _);
            if (result is OperationStatus.InvalidData)
            {
                ThrowHelpers.InvalidAscii();
            }

            return new U8String(destination, 0, span.Length);
        }

        return default;
    }
}
