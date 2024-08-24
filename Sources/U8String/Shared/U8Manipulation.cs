using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using U8.Abstractions;
using U8.Primitives;

namespace U8.Shared;

internal static class U8Manipulation
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static U8String ConcatUnchecked(ReadOnlySpan<byte> left, byte right)
    {
        Debug.Assert(U8Info.IsAsciiByte(in right));

        var length = left.Length + 1;
        var nullTerminate = right != 0;
        var value = new byte[(nint)(uint)length + (nullTerminate ? 1 : 0)];

        ref var dst = ref value.AsRef();
        left.CopyToUnsafe(ref dst);
        dst.Add(left.Length) = right;

        return new U8String(value, length, neverEmpty: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static U8String ConcatUnchecked(byte left, ReadOnlySpan<byte> right)
    {
        Debug.Assert(U8Info.IsAsciiByte(in left));

        var length = right.Length + 1;
        var value = new byte[length + 1];

        ref var dst = ref value.AsRef();
        dst = left;
        right.CopyToUnsafe(ref dst.Add(1));

        return new U8String(value, length, neverEmpty: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static U8String ConcatUnchecked(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        Debug.Assert(!left.IsEmpty || !right.IsEmpty);

        var length = left.Length + right.Length;
        var value = new byte[(nint)(uint)(length + 1)];

        ref var dst = ref value.AsRef();
        left.CopyToUnsafe(ref dst);
        right.CopyToUnsafe(ref dst.Add(left.Length));

        return new U8String(value, length, neverEmpty: true);
    }

    internal static U8String Join(byte separator, ReadOnlySpan<U8String> values)
    {
        if (values.Length > 1)
        {
            return JoinUnchecked(separator, values);
        }
        else if (values.Length is 1)
        {
            return values[0];
        }

        return default;
    }

    internal static U8String Join(byte separator, IEnumerable<U8String> values)
    {
        if (values.TryGetSpan(out var span))
        {
            return Join(separator, span);
        }
        else if (values.TryGetNonEnumeratedCount(out var count))
        {
            if (count is 1)
            {
                return values.First();
            }
            else if (count is 0)
            {
                return default;
            }
        }

        return JoinEnumerable(separator, values);

        static U8String JoinEnumerable(byte separator, IEnumerable<U8String> values)
        {
            using var enumerator = values.GetEnumerator();
            var builder = new ArrayBuilder();

            if (enumerator.MoveNext())
            {
                var first = enumerator.Current;
                builder.Write(first, first.Length + 1);

                while (enumerator.MoveNext())
                {
                    builder.WriteUnchecked(separator);

                    var current = enumerator.Current;
                    if (!current.IsEmpty)
                    {
                        builder.Write(current.UnsafeSpan, current.Length + 1);
                    }
                }
            }

            var result = new U8String(builder.Written, skipValidation: true);

            builder.Dispose();
            return result;
        }
    }

    internal static U8String Join(ReadOnlySpan<byte> separator, ReadOnlySpan<U8String> values)
    {
        if (values.Length > 1)
        {
            if (separator.Length > 1)
            {
                return JoinUnchecked(separator, values);
            }
            else if (separator.Length is 1)
            {
                return JoinUnchecked(separator[0], values);
            }

            return U8String.Concat(values);
        }
        else if (values.Length is 1)
        {
            return values[0];
        }

        return default;
    }

    internal static U8String Join(ReadOnlySpan<byte> separator, IEnumerable<U8String> values)
    {
        if (values.TryGetSpan(out var span))
        {
            return Join(separator, span);
        }
        else if (values.TryGetNonEnumeratedCount(out var count))
        {
            if (count is 1)
            {
                return values.First();
            }
            else if (count is 0)
            {
                return default;
            }
        }

        return JoinEnumerable(separator, values);

        static U8String JoinEnumerable(ReadOnlySpan<byte> separator, IEnumerable<U8String> values)
        {
            using var enumerator = values.GetEnumerator();
            var builder = new ArrayBuilder();

            if (enumerator.MoveNext())
            {
                builder.Write(enumerator.Current.AsSpan());

                while (enumerator.MoveNext())
                {
                    builder.Write(separator);

                    var current = enumerator.Current;
                    if (!current.IsEmpty)
                    {
                        builder.Write(current.UnsafeSpan);
                    }
                }
            }

            var result = new U8String(builder.Written, skipValidation: true);

            builder.Dispose();
            return result;
        }
    }

    internal static U8String Join<T>(
        byte separator,
        ReadOnlySpan<T> values,
        ReadOnlySpan<char> format,
        IFormatProvider? provider) where T : IUtf8SpanFormattable
    {
        if (typeof(T) == typeof(U8String))
        {
            return Join(separator, values.Cast<T, U8String>());
        }

        provider ??= CultureInfo.InvariantCulture;
        if (values.Length > 1)
        {
            return JoinUnchecked(separator, values, format, provider);
        }
        else if (values.Length is 1)
        {
            return U8String.Create(values[0], format, provider);
        }

        return default;
    }

    internal static U8String Join<T>(
        byte separator,
        IEnumerable<T> values,
        ReadOnlySpan<char> format,
        IFormatProvider? provider) where T : IUtf8SpanFormattable
    {
        if (typeof(T) == typeof(U8String))
        {
            return Join(separator, values.Cast<T, U8String>());
        }

        provider ??= CultureInfo.InvariantCulture;
        if (values.TryGetSpan(out var span))
        {
            return Join(separator, span, format, provider);
        }
        else if (values.TryGetNonEnumeratedCount(out var count))
        {
            if (count is 1)
            {
                return U8String.Create(values.First(), format, provider);
            }
            else if (count is 0)
            {
                return default;
            }
        }

        return JoinUnchecked(separator, values, format, provider);
    }

    internal static U8String Join<T>(
        ReadOnlySpan<byte> separator,
        ReadOnlySpan<T> values,
        ReadOnlySpan<char> format,
        IFormatProvider? provider) where T : IUtf8SpanFormattable
    {
        if (typeof(T) == typeof(U8String))
        {
            return Join(separator, values.Cast<T, U8String>());
        }

        provider ??= CultureInfo.InvariantCulture;
        if (values.Length > 1)
        {
            if (separator.Length > 1)
            {
                return JoinUnchecked(separator, values, format, provider);
            }
            else if (separator.Length is 1)
            {
                return JoinUnchecked(separator[0], values, format, provider);
            }

            return U8String.Concat(values, format, provider);
        }
        else if (values.Length is 1)
        {
            return U8String.Create(values[0], format, provider);
        }

        return default;
    }

    internal static U8String Join<T>(
        ReadOnlySpan<byte> separator,
        IEnumerable<T> values,
        ReadOnlySpan<char> format,
        IFormatProvider? provider) where T : IUtf8SpanFormattable
    {
        if (typeof(T) == typeof(U8String))
        {
            return Join(separator, values.Cast<T, U8String>());
        }

        provider ??= CultureInfo.InvariantCulture;
        if (values.TryGetSpan(out var span))
        {
            return Join(separator, span, format, provider);
        }
        else if (values.TryGetNonEnumeratedCount(out var count))
        {
            if (count is 1)
            {
                return U8String.Create(values.First(), format, provider);
            }
            else if (count is 0)
            {
                return default;
            }
        }

        return JoinUnchecked(separator, values, format, provider);
    }

    // Contract:
    // - values.Length is greater than 1
    // - separator is an ascii byte
    static U8String JoinUnchecked(byte separator, ReadOnlySpan<U8String> values)
    {
        Debug.Assert(values.Length > 1);

        var length = values.Length - 1;
        var llength = (long)(uint)length;
        foreach (var item in values)
        {
            llength += (uint)item.Length;
        }

        // Implicitly check if the total length is within Array.MaxLength,
        // without risking integer overflow.
        var result = new byte[llength + 1]; // null-terminate
        length = (int)(uint)llength;

        var first = values.AsRef();
        first.AsSpan().CopyTo(result);

        ref var src = ref values.AsRef(1);
        ref var dst = ref result.AsRef(first.Length);
        ref var end = ref dst.Add(length);
        var count = values.Length - 1;

        for (var i = 0; i < count; i++)
        {
            var item = src.Add(i);
            var segmentLength = item.Length + 1;

            if (dst.Add(segmentLength)
                   .GreaterThan(ref end))
            {
                ThrowHelpers.DestinationTooShort();
            }

            dst = separator;
            dst = ref dst.Add(1);

            if (!item.IsEmpty)
            {
                item.UnsafeSpan.CopyToUnsafe(ref dst);
                dst = ref dst.Add(item.Length);
            }
        }

        return new(result, 0, length);
    }

    // Contract:
    // - values.Length is greater than 1
    // - separator is a non-ascii byte, char, Rune or U8Scalar
    // - will not write past the end of the destination buffer
    // if values are mutated in the middle of the join, but
    // the length of the resulting string may exceed the written bytes count.
    static U8String JoinUnchecked(ReadOnlySpan<byte> separator, ReadOnlySpan<U8String> values)
    {
        Debug.Assert(values.Length > 1);
        Debug.Assert(separator.Length > 1);

        var length = separator.Length * (values.Length - 1);
        var llength = (long)(uint)length;
        foreach (var item in values)
        {
            llength += (uint)item.Length;
        }

        var result = new byte[llength + 1]; // null-terminate
        length = (int)(uint)llength;

        var first = values.AsRef();
        first.AsSpan().CopyTo(result);

        ref var src = ref values.AsRef(1);
        ref var dst = ref result.AsRef(first.Length);
        ref var end = ref dst.Add(length);
        var count = values.Length - 1;

        switch (separator.Length)
        {
            case 2:
                var twoBytes = separator.AsRef().Cast<byte, ushort>();
                JoinTwoBytes(ref src, ref dst, ref end, count, twoBytes);
                break;
            case 3:
                var b01 = separator.AsRef().Cast<byte, ushort>();
                var b2 = separator.AsRef(2);
                JoinThreeBytes(ref src, ref dst, ref end, count, b01, b2);
                break;
            case 4:
                var fourBytes = separator.AsRef().Cast<byte, uint>();
                JoinFourBytes(ref src, ref dst, ref end, count, fourBytes);
                break;
            default:
                JoinSpan(ref src, ref dst, ref end, count, separator);
                break;
        }

        return new(result, 0, length);

        static void JoinTwoBytes(
            ref U8String src,
            ref byte dst,
            ref byte end,
            int count,
            ushort separator)
        {
            for (var i = 0; i < count; i++)
            {
                var item = src.Add(i);
                var segmentLength = item.Length + 2;

                if (dst.Add(segmentLength)
                       .GreaterThan(ref end))
                {
                    ThrowHelpers.DestinationTooShort();
                }

                dst.Cast<byte, ushort>() = separator;
                dst = ref dst.Add(2);

                if (!item.IsEmpty)
                {
                    item.UnsafeSpan.CopyToUnsafe(ref dst);
                    dst = ref dst.Add(item.Length);
                }
            }
        }

        static void JoinThreeBytes(
            ref U8String src,
            ref byte dst,
            ref byte end,
            int count,
            ushort b01,
            byte b2)
        {
            for (var i = 0; i < count; i++)
            {
                var item = src.Add(i);
                var segmentLength = item.Length + 3;

                if (dst.Add(segmentLength)
                       .GreaterThan(ref end))
                {
                    ThrowHelpers.DestinationTooShort();
                }

                dst.Cast<byte, ushort>() = b01;
                dst.Add(2) = b2;
                dst = ref dst.Add(3);

                if (!item.IsEmpty)
                {
                    item.UnsafeSpan.CopyToUnsafe(ref dst);
                    dst = ref dst.Add(item.Length);
                }
            }
        }

        static void JoinFourBytes(
            ref U8String src,
            ref byte dst,
            ref byte end,
            int count,
            uint separator)
        {
            for (var i = 0; i < count; i++)
            {
                var item = src.Add(i);
                var segmentLength = item.Length + 4;

                if (dst.Add(segmentLength)
                       .GreaterThan(ref end))
                {
                    ThrowHelpers.DestinationTooShort();
                }

                dst.Cast<byte, uint>() = separator;
                dst = ref dst.Add(4);

                if (!item.IsEmpty)
                {
                    item.UnsafeSpan.CopyToUnsafe(ref dst);
                    dst = ref dst.Add(item.Length);
                }
            }
        }

        static void JoinSpan(
            ref U8String src,
            ref byte dst,
            ref byte end,
            int count,
            ReadOnlySpan<byte> separator)
        {
            for (var i = 0; i < count; i++)
            {
                var item = src.Add(i);
                var segmentLength = item.Length + separator.Length;

                if (dst.Add(segmentLength)
                       .GreaterThan(ref end))
                {
                    ThrowHelpers.DestinationTooShort();
                }

                separator.CopyToUnsafe(ref dst);
                dst = ref dst.Add(separator.Length);

                if (!item.IsEmpty)
                {
                    item.UnsafeSpan.CopyToUnsafe(ref dst);
                    dst = ref dst.Add(item.Length);
                }
            }
        }
    }

    static U8String JoinUnchecked<T>(
        byte separator,
        ReadOnlySpan<T> values,
        ReadOnlySpan<char> format,
        IFormatProvider provider) where T : IUtf8SpanFormattable
    {
        Debug.Assert(values.Length > 1);
        Debug.Assert(typeof(T) != typeof(U8String));

        var builder = new ArrayBuilder();
        var first = MemoryMarshal.GetReference(values);
        builder.Write(first, format, provider);

        foreach (var value in values[1..])
        {
            builder.Write(separator);
            builder.Write(value, format, provider);
        }

        var result = new U8String(builder.Written, skipValidation: true);

        builder.Dispose();
        return result;
    }

    static U8String JoinUnchecked<T>(
        ReadOnlySpan<byte> separator,
        ReadOnlySpan<T> values,
        ReadOnlySpan<char> format,
        IFormatProvider provider) where T : IUtf8SpanFormattable
    {
        Debug.Assert(separator.Length > 1);
        Debug.Assert(values.Length > 1);
        Debug.Assert(typeof(T) != typeof(U8String));

        var builder = new ArrayBuilder();
        var first = MemoryMarshal.GetReference(values);
        builder.Write(first, format, provider);

        foreach (var value in values[1..])
        {
            builder.Write(separator);
            builder.Write(value, format, provider);
        }

        var result = new U8String(builder.Written, skipValidation: true);

        builder.Dispose();
        return result;
    }

    static U8String JoinUnchecked<T>(
        byte separator,
        IEnumerable<T> values,
        ReadOnlySpan<char> format,
        IFormatProvider provider) where T : IUtf8SpanFormattable
    {
        Debug.Assert(typeof(T) != typeof(U8String));
        Debug.Assert(values is not (T[] or List<T>));

        using var enumerator = values.GetEnumerator();
        var builder = new ArrayBuilder();

        if (enumerator.MoveNext())
        {
            builder.Write(enumerator.Current, format, provider);

            while (enumerator.MoveNext())
            {
                builder.Write(separator);
                builder.Write(enumerator.Current, format, provider);
            }
        }

        var result = new U8String(builder.Written, skipValidation: true);

        builder.Dispose();
        return result;
    }

    static U8String JoinUnchecked<T>(
        ReadOnlySpan<byte> separator,
        IEnumerable<T> values,
        ReadOnlySpan<char> format,
        IFormatProvider provider) where T : IUtf8SpanFormattable
    {
        Debug.Assert(separator.Length > 1);
        Debug.Assert(typeof(T) != typeof(U8String));
        Debug.Assert(values is not (T[] or List<T>));

        using var enumerator = values.GetEnumerator();
        var builder = new ArrayBuilder();

        if (enumerator.MoveNext())
        {
            builder.Write(enumerator.Current, format, provider);

            while (enumerator.MoveNext())
            {
                builder.Write(separator);
                builder.Write(enumerator.Current, format, provider);
            }
        }

        var result = new U8String(builder.Written, skipValidation: true);

        builder.Dispose();
        return result;
    }

    internal static U8String Join<T, E>(byte separator, T values)
        where T : struct, IU8Enumerable<E>
        where E : struct, IU8Enumerator
    {
        var builder = new ArrayBuilder();
        var enumerator = values.GetEnumerator();

        var notEmpty = enumerator.MoveNext();
        var first = enumerator.Current;
        Debug.Assert(notEmpty);

        builder.Write(first, first.Length + 1);
        while (enumerator.MoveNext())
        {
            builder.WriteUnchecked(separator);
            var current = enumerator.Current;
            builder.Write(current, current.Length + 1);
        }

        var result = new U8String(builder.Written, skipValidation: true);
        builder.Dispose();
        return result;
    }

    internal static U8String Join<T, E>(ReadOnlySpan<byte> separator, T values)
        where T : struct, IU8Enumerable<E>
        where E : struct, IU8Enumerator
    {
        Debug.Assert(separator.Length > 1);

        var builder = new ArrayBuilder();
        var enumerator = values.GetEnumerator();

        var notEmpty = enumerator.MoveNext();
        var first = enumerator.Current;
        Debug.Assert(notEmpty);

        builder.Write(first);
        while (enumerator.MoveNext())
        {
            // TODO: Optimize further
            builder.Write(separator);
            builder.Write(enumerator.Current);
        }

        var result = new U8String(builder.Written, skipValidation: true);
        builder.Dispose();
        return result;
    }

    internal static U8String JoinSized<T, E>(byte separator, T values, int length)
        where T : struct, IU8Enumerable<E>
        where E : struct, IU8Enumerator
    {
        Debug.Assert(length > 0);

        var bytes = new byte[(nint)(uint)length + 1];
        var enumerator = values.GetEnumerator();

        var notEmpty = enumerator.MoveNext();
        var first = enumerator.Current;
        Debug.Assert(notEmpty);

        ref var dst = ref bytes.AsRef();
        if (!first.IsEmpty)
        {
            first.UnsafeSpan.CopyToUnsafe(ref dst);
            dst = ref dst.Add(first.Length);
        }

        while (enumerator.MoveNext())
        {
            dst = separator;
            dst = ref dst.Add(1);

            var current = enumerator.Current;
            if (!current.IsEmpty)
            {
                current.UnsafeSpan.CopyToUnsafe(ref dst);
                dst = ref dst.Add(current.Length);
            }
        }

        return new(bytes, length, neverEmpty: true);
    }

    internal static U8String JoinSized<T, E>(ReadOnlySpan<byte> separator, T values, int lenght)
        where T : struct, IU8Enumerable<E>
        where E : struct, IU8Enumerator
    {
        Debug.Assert(separator.Length > 1);
        Debug.Assert(lenght > 0);

        var bytes = new byte[(nint)(uint)lenght + 1];
        var enumerator = values.GetEnumerator();

        var notEmpty = enumerator.MoveNext();
        var first = enumerator.Current;
        Debug.Assert(notEmpty);

        ref var dst = ref bytes.AsRef();
        if (!first.IsEmpty)
        {
            first.UnsafeSpan.CopyToUnsafe(ref dst);
            dst = ref dst.Add(first.Length);
        }

        switch (separator.Length)
        {
            case 2:
                var twoBytes = separator.AsRef().Cast<byte, ushort>();
                JoinTwoBytes(enumerator, ref dst, twoBytes);
                break;
            case 3:
                var b01 = separator.AsRef().Cast<byte, ushort>();
                var b2 = separator.AsRef(2);
                JoinThreeBytes(enumerator, ref dst, b01, b2);
                break;
            case 4:
                var fourBytes = separator.AsRef().Cast<byte, uint>();
                JoinFourBytes(enumerator, ref dst, fourBytes);
                break;
            default:
                JoinSpan(enumerator, ref dst, separator);
                break;
        }

        return new(bytes, lenght, neverEmpty: true);

        static void JoinTwoBytes(E enumerator, ref byte dst, ushort separator)
        {
            while (enumerator.MoveNext())
            {
                dst.Cast<byte, ushort>() = separator;
                dst = ref dst.Add(2);

                var current = enumerator.Current;
                if (!current.IsEmpty)
                {
                    current.UnsafeSpan.CopyToUnsafe(ref dst);
                    dst = ref dst.Add(current.Length);
                }
            }
        }

        static void JoinThreeBytes(E enumerator, ref byte dst, ushort b01, byte b2)
        {
            while (enumerator.MoveNext())
            {
                dst.Cast<byte, ushort>() = b01;
                dst.Add(2) = b2;
                dst = ref dst.Add(3);

                var current = enumerator.Current;
                if (!current.IsEmpty)
                {
                    current.UnsafeSpan.CopyToUnsafe(ref dst);
                    dst = ref dst.Add(current.Length);
                }
            }
        }

        static void JoinFourBytes(E enumerator, ref byte dst, uint separator)
        {
            while (enumerator.MoveNext())
            {
                dst.Cast<byte, uint>() = separator;
                dst = ref dst.Add(4);

                var current = enumerator.Current;
                if (!current.IsEmpty)
                {
                    current.UnsafeSpan.CopyToUnsafe(ref dst);
                    dst = ref dst.Add(current.Length);
                }
            }
        }

        static void JoinSpan(E enumerator, ref byte dst, ReadOnlySpan<byte> separator)
        {
            while (enumerator.MoveNext())
            {
                separator.CopyToUnsafe(ref dst);
                dst = ref dst.Add(separator.Length);

                var current = enumerator.Current;
                if (!current.IsEmpty)
                {
                    current.UnsafeSpan.CopyToUnsafe(ref dst);
                    dst = ref dst.Add(current.Length);
                }
            }
        }
    }

    internal interface IRunesSource
    {
        static abstract void SurrogateCheck();
    }

    internal readonly struct RunesSource : IRunesSource
    {
        public static void SurrogateCheck() { }
    }

    internal readonly struct CharsSource : IRunesSource
    {
        [DoesNotReturn, StackTraceHidden]
        public static void SurrogateCheck()
        {
            throw new ArgumentOutOfRangeException(
                paramName: "values",
                "A surrogate char has been encountered in the sequence. " +
                "Surrogate chars are not representable in UTF-8.");
        }
    }

    internal static U8String JoinRunes<TSource>(byte separator, U8String value)
        where TSource : struct, IRunesSource
    {
        Debug.Assert(U8Info.IsAsciiByte(separator));

        // I *think* this could be vectorized given sufficiently clever
        // branchless code and good SIMD throughput.
        var count = value.RuneCount;
        if (count > 1)
        {
            ref var src = ref value.UnsafeRef;
            ref var end = ref src.Add(value.Length);

            var length = value.Length + (count - 1);
            var nullTerminate = end.Substract(1) != 0;
            var bytes = new byte[(nint)(uint)length + (nullTerminate ? 1 : 0)];

            ref var dst = ref bytes.AsRef();

            // Copy the first rune
            var b = src;
            do
            {
                dst = b;
                src = ref src.Add(1);
                dst = ref dst.Add(1);
                b = src;
            } while (U8Info.IsContinuationByte(b));

            while (src.LessThan(ref end))
            {
                switch (U8Info.RuneLength(in src))
                {
                    case 1:
                        dst.Cast<byte, ushort>() = (ushort)(separator | ((uint)src << 8));
                        src = ref src.Add(1);
                        dst = ref dst.Add(2);
                        continue;

                    case 2:
                        dst = separator;
                        dst.Add(1).Cast<byte, ushort>() = src.Cast<byte, ushort>();
                        src = ref src.Add(2);
                        dst = ref dst.Add(3);
                        continue;

                    case 3:
                        dst.Cast<byte, uint>() =
                            separator |
                            ((uint)src.Cast<byte, ushort>() << 8) |
                            ((uint)src.Add(2) << 24);
                        src = ref src.Add(3);
                        dst = ref dst.Add(4);
                        continue;

                    default:
                        TSource.SurrogateCheck();
                        dst = separator;
                        dst.Add(1).Cast<byte, uint>() = src.Cast<byte, uint>();
                        src = ref src.Add(4);
                        dst = ref dst.Add(5);
                        continue;
                }
            }

            return new(bytes, 0, length);
        }
        else if (count is 1)
        {
            return value;
        }

        return default;
    }

    internal static U8String JoinRunes<TSource>(ReadOnlySpan<byte> separator, U8String value)
        where TSource : struct, IRunesSource
    {
        Debug.Assert(separator.Length > 1);

        var count = value.RuneCount;
        if (count > 1)
        {
            ref var src = ref value.UnsafeRef;
            ref var end = ref src.Add(value.Length);

            var length = value.Length + ((count - 1) * separator.Length);
            var nullTerminate = end.Substract(1) != 0;
            var bytes = new byte[(nint)(uint)length + (nullTerminate ? 1 : 0)];

            ref var dst = ref bytes.AsRef();

            // Copy the first rune
            var b = src;
            do
            {
                dst = b;
                src = ref src.Add(1);
                dst = ref dst.Add(1);
                b = src;
            } while (U8Info.IsContinuationByte(b));

            switch (separator.Length)
            {
                case 2:
                    var twoBytes = separator.AsRef().Cast<byte, ushort>();
                    JoinTwoBytes(ref src, ref end, ref dst, twoBytes);
                    break;
                case 3:
                    var b01 = separator.AsRef().Cast<byte, ushort>();
                    var b2 = separator.AsRef(2);
                    JoinThreeBytes(ref src, ref end, ref dst, b01, b2);
                    break;
                case 4:
                    var fourBytes = separator.AsRef().Cast<byte, uint>();
                    JoinFourBytes(ref src, ref end, ref dst, fourBytes);
                    break;
                default:
                    JoinSpan(ref src, ref end, ref dst, separator);
                    break;
            }

            return new(bytes, 0, length);
        }
        else if (count is 1)
        {
            return value;
        }

        return default;

        static void JoinTwoBytes(
            ref byte src,
            ref byte end,
            ref byte dst,
            ushort separator)
        {
            while (src.LessThan(ref end))
            {
                switch (U8Info.RuneLength(in src))
                {
                    case 1:
                        dst.Cast<byte, ushort>() = separator;
                        dst.Add(2) = src;
                        src = ref src.Add(1);
                        dst = ref dst.Add(3);
                        continue;

                    case 2:
                        dst.Cast<byte, uint>() =
                            separator | ((uint)src.Cast<byte, ushort>() << 16);
                        src = ref src.Add(2);
                        dst = ref dst.Add(4);
                        continue;

                    case 3:
                        dst.Cast<byte, uint>() =
                            separator | ((uint)src.Cast<byte, ushort>() << 16);
                        dst.Add(4) = src.Add(2);
                        src = ref src.Add(3);
                        dst = ref dst.Add(5);
                        continue;

                    default:
                        TSource.SurrogateCheck();
                        dst.Cast<byte, ushort>() = separator;
                        dst.Add(2).Cast<byte, uint>() = src.Cast<byte, uint>();
                        src = ref src.Add(4);
                        dst = ref dst.Add(6);
                        continue;
                }
            }
        }

        static void JoinThreeBytes(
            ref byte src,
            ref byte end,
            ref byte dst,
            ushort b01,
            byte b2)
        {
            while (src.LessThan(ref end))
            {
                switch (U8Info.RuneLength(in src))
                {
                    case 1:
                        dst.Cast<byte, ushort>() = b01;
                        dst.Add(2).Cast<byte, ushort>() = (ushort)(b2 | ((uint)src << 8));
                        src = ref src.Add(1);
                        dst = ref dst.Add(4);
                        continue;

                    case 2:
                        dst.Cast<byte, uint>() =
                            b01 | ((uint)b2 << 16) | ((uint)src << 24);
                        dst.Add(4) = src.Add(1);
                        src = ref src.Add(2);
                        dst = ref dst.Add(5);
                        continue;

                    case 3:
                        dst.Cast<byte, uint>() =
                            b01 | ((uint)b2 << 16) | ((uint)src << 24);
                        dst.Add(4).Cast<byte, ushort>() = src.Add(1).Cast<byte, ushort>();
                        src = ref src.Add(3);
                        dst = ref dst.Add(6);
                        continue;

                    default:
                        TSource.SurrogateCheck();
                        dst.Cast<byte, uint>() = b01;
                        dst.Add(2) = b2;
                        dst.Add(3).Cast<byte, uint>() = src.Cast<byte, uint>();
                        src = ref src.Add(4);
                        dst = ref dst.Add(7);
                        continue;
                }
            }
        }

        static void JoinFourBytes(
            ref byte src,
            ref byte end,
            ref byte dst,
            uint separator)
        {
            while (src.LessThan(ref end))
            {
                switch (U8Info.RuneLength(in src))
                {
                    case 1:
                        dst.Cast<byte, uint>() = separator;
                        dst.Add(4) = src;
                        src = ref src.Add(1);
                        dst = ref dst.Add(5);
                        continue;

                    case 2:
                        dst.Cast<byte, uint>() = separator;
                        dst.Add(4).Cast<byte, ushort>() = src.Cast<byte, ushort>();
                        src = ref src.Add(2);
                        dst = ref dst.Add(6);
                        continue;

                    case 3:
                        dst.Cast<byte, uint>() = separator;
                        dst.Add(4).Cast<byte, ushort>() = src.Cast<byte, ushort>();
                        dst.Add(6) = src.Add(2);
                        src = ref src.Add(3);
                        dst = ref dst.Add(7);
                        continue;

                    default:
                        TSource.SurrogateCheck();
                        dst.Cast<byte, ulong>() =
                            separator | ((ulong)src.Cast<byte, uint>() << 32);
                        src = ref src.Add(4);
                        dst = ref dst.Add(8);
                        continue;
                }
            }
        }

        static void JoinSpan(
            ref byte src,
            ref byte end,
            ref byte dst,
            ReadOnlySpan<byte> separator)
        {
            while (src.LessThan(ref end))
            {
                separator.CopyToUnsafe(ref dst);
                dst = ref dst.Add(separator.Length);

                switch (U8Info.RuneLength(in src))
                {
                    case 1:
                        dst = src;
                        src = ref src.Add(1);
                        dst = ref dst.Add(1);
                        continue;

                    case 2:
                        dst.Cast<byte, ushort>() = src.Cast<byte, ushort>();
                        src = ref src.Add(2);
                        dst = ref dst.Add(2);
                        continue;

                    case 3:
                        dst.Cast<byte, ushort>() = src.Cast<byte, ushort>();
                        dst.Add(2) = src.Add(2);
                        src = ref src.Add(3);
                        dst = ref dst.Add(3);
                        continue;

                    default:
                        TSource.SurrogateCheck();
                        dst.Cast<byte, uint>() = src.Cast<byte, uint>();
                        src = ref src.Add(4);
                        dst = ref dst.Add(4);
                        continue;
                }
            }
        }
    }

    internal static U8String Replace(
        U8String source,
        byte oldValue,
        byte newValue,
        bool validate = true)
    {
        if (!source.IsEmpty)
        {
            var current = source.UnsafeSpan;
            var firstReplace = current.IndexOf(oldValue);
            if (firstReplace >= 0)
            {
                var replaced = new byte[source.Length + 1];
                var destination = replaced.AsSpan();

                current
                    .SliceUnsafe(0, firstReplace)
                    .CopyTo(destination.SliceUnsafe(0, firstReplace));

                destination = destination.SliceUnsafe(firstReplace);
                current = current.SliceUnsafe(firstReplace);

                current.Replace(
                    destination.SliceUnsafe(0, current.Length),
                    oldValue,
                    newValue);

                // Old and new bytes which individually are invalid unicode scalar values
                // are allowed if the replacement produces a valid UTF-8 sequence.
                if (validate && (
                    !U8Info.IsAsciiByte(oldValue) ||
                    !U8Info.IsAsciiByte(newValue)))
                {
                    U8String.Validate(destination);
                }
                return new(replaced, 0, source.Length);
            }

            return source;
        }

        return default;
    }

    internal static U8String Replace(U8String source, char oldValue, char newValue)
    {
        ThrowHelpers.CheckSurrogate(oldValue);

        return char.IsAscii(oldValue) && char.IsAscii(newValue)
            ? Replace(source, (byte)oldValue, (byte)newValue, validate: false)
            : ReplaceCore(
                source,
                new U8Scalar(oldValue).AsSpan(),
                new U8Scalar(newValue).AsSpan());
    }

    internal static U8String Replace(U8String source, Rune oldValue, Rune newValue)
    {
        return oldValue.IsAscii && newValue.IsAscii
            ? Replace(source, (byte)oldValue.Value, (byte)newValue.Value, validate: false)
            : ReplaceCore(
                source,
                new U8Scalar(oldValue).AsSpan(),
                new U8Scalar(newValue).AsSpan());
    }

    internal static U8String Replace(
        U8String source,
        ReadOnlySpan<byte> oldValue,
        ReadOnlySpan<byte> newValue,
        bool validate = true)
    {
        if (oldValue.Length is 0)
        {
            ThrowHelpers.ArgumentOutOfRange();
        }

        if (!source.IsEmpty)
        {
            if (newValue.Length is 0)
            {
                return oldValue.Length is 1
                    ? Remove(source, oldValue[0])
                    : Remove(source, oldValue);
            }

            if (oldValue.Length is 1 && newValue.Length is 1)
            {
                return Replace(source, oldValue[0], newValue[0]);
            }

            return ReplaceCore(source, oldValue, newValue, validate);
        }

        return default;
    }

    internal static U8String Replace(U8String source, U8String oldValue, U8String newValue)
    {
        if (oldValue.IsEmpty)
        {
            ThrowHelpers.ArgumentOutOfRange();
        }

        if (!source.IsEmpty)
        {
            return ReplaceCore(
                source,
                oldValue.UnsafeSpan,
                newValue.AsSpan(),
                validate: false);
        }

        return default;
    }

    /// <summary>
    /// Assumes the following checks are performed outside of this method:
    /// <para>- <paramref name="source"/> is not empty</para>
    /// <para>- <paramref name="oldValue"/> is not empty</para>
    /// <para>- <paramref name="newValue"/> is not empty</para>
    /// <para>- <paramref name="oldValue"/> and <paramref name="newValue"/> lengths are greater than 1</para>
    /// </summary>
    internal static U8String ReplaceCore(
        U8String source,
        ReadOnlySpan<byte> oldValue,
        ReadOnlySpan<byte> newValue,
        bool validate = true)
    {
        Debug.Assert(!oldValue.IsEmpty);
        Debug.Assert(!newValue.IsEmpty);
        Debug.Assert(oldValue.Length != 0 && newValue.Length != 0);
        Debug.Assert(oldValue.Length >= 1 || newValue.Length >= 1);

        var count = source.AsSpan().Count(oldValue);
        if (count > 0)
        {
            var length = source.Length - (oldValue.Length * count) + (newValue.Length * count);
            var result = new byte[length + 1];

            var offset = 0;
            ref var dst = ref result.AsRef();
            foreach (var segment in new U8RefSplit(source, oldValue))
            {
                segment.AsSpan().CopyToUnsafe(ref dst.Add(offset));

                if ((offset += segment.Length) >= length)
                {
                    break;
                }

                newValue.CopyToUnsafe(ref dst.Add(offset));
                offset += newValue.Length;
            }

            if (validate)
            {
                U8String.Validate(result);
            }

            return new(result, 0, result.Length);
        }

        return source;
    }

    internal static U8String Remove(U8String source, byte value)
    {
        var count = U8Searching.CountByte(value, ref source.DangerousRef, (uint)source.Length);
        if (count > 0)
        {
            var length = (uint)source.Length - count;
            var result = new byte[length + 1];

            var offset = 0;
            ref var dst = ref result.AsRef();
            foreach (var segment in new U8Split<byte>(source, value))
            {
                segment.AsSpan().CopyToUnsafe(ref dst.Add(offset));
                offset += segment.Length;
            }

            if (!U8Info.IsAsciiByte(value))
            {
                U8String.Validate(result);
            }

            return new(result, 0, (int)(uint)length);
        }

        return source;
    }

    internal static U8String Remove(U8String source, ReadOnlySpan<byte> value, bool validate = true)
    {
        var count = source.AsSpan().Count(value);
        if (count > 0)
        {
            var length = source.Length - (value.Length * count);
            var result = new byte[length + 1];

            var offset = 0;
            ref var dst = ref result.AsRef();
            foreach (var segment in new U8RefSplit(source, value))
            {
                segment.AsSpan().CopyToUnsafe(ref dst.Add(offset));
                offset += segment.Length;
            }

            if (validate && !U8String.IsValid(value))
            {
                U8String.Validate(result);
            }

            return new(result, 0, length);
        }

        return source;
    }

    internal static U8String StripLineEndings(U8String source)
    {
        var lines = source.Lines;
        if (lines.Count > 1)
        {
            var builder = new ArrayBuilder();
            foreach (var line in lines)
            {
                if (!line.IsEmpty)
                {
                    builder.Write(line.UnsafeSpan);
                }
            }

            var result = new U8String(builder.Written, skipValidation: true);

            builder.Dispose();
            return result;
        }

        return source;
    }

    internal static U8String LineEndingsToLF(U8String source)
    {
        return ReplaceCore(source, "\r\n"u8, "\n"u8, validate: false);
    }

    internal static U8String LineEndingsToCRLF(U8String source)
    {
        if (!source.IsEmpty)
        {
            // This method operates on absolute offsets
            var array = source._value!;
            var range = source._inner;

            while (true)
            {
                var span = array.SliceUnsafe(range);
                var offset = span.IndexOf((byte)'\n');

                if ((uint)offset < (uint)span.Length)
                {
                    range = new(
                        range.Offset + offset + 1,
                        range.Length - offset - 1);

                    if (offset > 0 && span[offset - 1] is (byte)'\r')
                    {
                        continue;
                    }
                    // Range is now the slice after the first LF -> CRLF replacement
                    else
                    {
                        goto Replace;
                    }
                }

                return source;
            }

        Replace:
            var firstReplace = range.Offset - 1;
            var builder = new ArrayBuilder();

            // Copy the first part of the string before the first LF -> CRLF
            builder.Write(array.SliceUnsafe(
                source._inner.Offset, firstReplace - source._inner.Offset));
            builder.Write("\r\n"u8);

            foreach (var line in new U8String(array, range).Lines)
            {
                if (!line.IsEmpty)
                    builder.Write(line.UnsafeSpan);
                builder.Write("\r\n"u8);
            }

            var result = new U8String(builder.Written, skipValidation: true);

            builder.Dispose();
            return result;
        }

        return default;
    }

    internal static U8String LineEndingsToCustom(U8String source, byte lineEnding)
    {
        var lines = source.Lines;
        if (lines.Count > 1)
        {
            var crlfOffset = source.IndexOf("\r\n"u8);
            if (crlfOffset >= 0)
            {
                var builder = new ArrayBuilder();
                var enumerator = lines.GetEnumerator();

                var notEmpty = enumerator.MoveNext();
                Debug.Assert(notEmpty);

                var line = enumerator.Current;
                builder.Write(line, line.Length + 1);

                while (enumerator.MoveNext())
                {
                    builder.WriteUnchecked(lineEnding);
                    line = enumerator.Current;
                    builder.Write(line, line.Length + 1);
                }

                var result = new U8String(builder.Written, skipValidation: true);

                builder.Dispose();
                return result;
            }

            return Replace(source, (byte)'\n', lineEnding, validate: false);
        }

        return source;
    }

    internal static U8String LineEndingsToCustom(U8String source, ReadOnlySpan<byte> lineEnding)
    {
        Debug.Assert(lineEnding.Length > 0);

        var lines = source.Lines;
        if (lines.Count > 1)
        {
            var builder = new ArrayBuilder();
            var enumerator = lines.GetEnumerator();

            enumerator.MoveNext();
            var line = enumerator.Current;
            if (!line.IsEmpty)
            {
                builder.Write(line.UnsafeSpan);
            }

            while (enumerator.MoveNext())
            {
                builder.Write(lineEnding);
                line = enumerator.Current;
                if (!line.IsEmpty)
                {
                    builder.Write(line.UnsafeSpan);
                }
            }

            var result = new U8String(builder.Written, skipValidation: true);

            builder.Dispose();
            return result;
        }

        return source;
    }
}
