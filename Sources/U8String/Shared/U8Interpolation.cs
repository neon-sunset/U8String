using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Unicode;

using U8.Primitives;

namespace U8.Shared;

interface IInterpolatedHandler
{
    IFormatProvider Provider { get; }
    int BytesWritten { get; set; }
    ReadOnlySpan<byte> Written { get; }
    Span<byte> Free { get; }

    void Grow();
    void Grow(int hint);
    void Reset();
}

// This interface exists in order to ensure interpolated handlers do not have
// inconsistent handling of all the 293464301 AppendFormatted overloads.
interface IInterpolatedHandlerImplementation : IInterpolatedHandler, IDisposable
{
    void AppendLiteral([ConstantExpected] string s);
    void AppendFormatted(bool value);
    void AppendFormatted(char value);
    void AppendFormatted(Rune value);
    void AppendFormatted(U8String value);
    void AppendFormatted(U8String? value);
    void AppendFormatted(ReadOnlySpan<byte> value);
    void AppendFormatted(string? value);
    void AppendFormatted(ReadOnlySpan<char> value);
    void AppendFormatted<T>(T value);
    void AppendFormatted<T>(T value, ReadOnlySpan<char> format)
        where T : IUtf8SpanFormattable;
    void AppendFormatted<T>(T value, ReadOnlySpan<char> format, IFormatProvider? provider)
        where T : IUtf8SpanFormattable;
}

static class U8Interpolation
{
    // Reference: https://github.com/dotnet/runtime/issues/93501
    // Refactor once inlined TryGetBytes gains UTF8EncodingSealed.ReadUtf8 call
    // which JIT/AOT can optimize away for string literals, eliding the transcoding.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AppendLiteral<T>(ref T handler, [ConstantExpected] string s)
        where T : struct, IInterpolatedHandler
    {
        if (s.Length > 0)
        {
            if (s.Length is 1 && char.IsAscii(s[0]))
            {
                AppendByte(ref handler, (byte)s[0]);
            }
            else if (s.Length is 2
                && char.IsAscii(s[0])
                && char.IsAscii(s[1]))
            {
                AppendTwoBytes(ref handler, (ushort)(s[0] | ((uint)s[1] << 8)));
            }
            else
            {
                AppendConstantString(ref handler, s);
            }
        }
    }

    internal static void AppendFormatted<T>(ref T handler, bool value)
        where T : struct, IInterpolatedHandler
    {
        AppendBytes(ref handler, value ? "True"u8 : "False"u8);
    }

    internal static void AppendFormatted<T>(ref T handler, char value)
        where T : struct, IInterpolatedHandler
    {
        ThrowHelpers.CheckSurrogate(value);

        if (char.IsAscii(value))
        {
            AppendByte(ref handler, (byte)value);
            return;
        }

        AppendBytes(ref handler, value <= 0x7FF ? value.AsTwoBytes() : value.AsThreeBytes());
    }

    internal static void AppendFormatted<T>(ref T handler, Rune value)
        where T : struct, IInterpolatedHandler
    {
        if (value.IsAscii)
        {
            AppendByte(ref handler, (byte)value.Value);
            return;
        }

        AppendBytes(ref handler, value.Value switch
        {
            <= 0x7FF => value.AsTwoBytes(),
            <= 0xFFFF => value.AsThreeBytes(),
            _ => value.AsFourBytes()
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AppendFormatted<T>(ref T handler, U8String value)
        where T : struct, IInterpolatedHandler
    {
        AppendBytes(ref handler, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AppendFormatted<T>(ref T handler, U8String? value)
        where T : struct, IInterpolatedHandler
    {
        if (value is U8String text)
        {
            AppendBytes(ref handler, text);
        }
    }

    internal static void AppendFormatted<T>(ref T handler, ReadOnlySpan<byte> value)
        where T : struct, IInterpolatedHandler
    {
        U8String.Validate(value);
        AppendBytes(ref handler, value);
    }

    internal static void AppendFormatted<T>(ref T handler, string? value)
        where T : struct, IInterpolatedHandler
    {
        AppendFormatted(ref handler, value.AsSpan());
    }

    internal static void AppendFormatted<T>(ref T handler, ReadOnlySpan<char> value)
        where T : struct, IInterpolatedHandler
    {
    Retry:
        if (Encoding.UTF8.TryGetBytes(value, handler.Free, out var written))
        {
            handler.BytesWritten += written;
            return;
        }

        // We can't use the length * 2 or * 3 hint here because
        // it will fail interpolation for 1-1.5GiB strings which
        // is otherwise a legal operation.
        handler.Grow();
        goto Retry;
    }

    internal static void AppendFormatted<T, U>(ref T handler, U value)
        where T : struct, IInterpolatedHandler
    {
    Retry:
        if (typeof(U) == typeof(U8String))
        {
            AppendFormatted(ref handler, (U8String)(object)value!);
            return;
        }
        else if (typeof(U) == typeof(U8Builder))
        {
            AppendBytes(ref handler, ((U8Builder)(object)value!).Written);
            return;
        }
        else if (typeof(U) == typeof(U8String?))
        {
            AppendFormatted(ref handler, (U8String?)(object)value!);
            return;
        }
        else if (value is IUtf8SpanFormattable)
        {
            if (((IUtf8SpanFormattable)value)
                .TryFormat(handler.Free, out var written, default, handler.Provider))
            {
                handler.BytesWritten += written;
                return;
            }
        }
        else if (typeof(U).IsEnum)
        {
#nullable disable
            var formattable = new U8EnumFormattable<U>(value);
#nullable restore
            if (formattable.TryFormat(handler.Free, out var written))
            {
                handler.BytesWritten += written;
                return;
            }
        }
        else if (typeof(U) == typeof(ImmutableArray<byte>))
        {
            AppendFormatted(ref handler, ((ImmutableArray<byte>)(object)value!).AsSpan());
            return;
        }
        else if (typeof(U) == typeof(byte[]))
        {
            AppendFormatted(ref handler, Unsafe.As<byte[]?>(value).AsSpan());
            return;
        }
        else if (typeof(U) == typeof(string))
        {
            AppendFormatted(ref handler, Unsafe.As<string?>(value));
            return;
        }
        else
        {
            UnsupportedAppend<T>();
        }

        handler.Grow();
        goto Retry;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void AppendFormatted<T, U>(ref T handler, U value, ReadOnlySpan<char> format)
        where T : IInterpolatedHandler
        where U : IUtf8SpanFormattable
    {
    Retry:
        if (value.TryFormat(handler.Free, out var written, format, handler.Provider))
        {
            handler.BytesWritten += written;
            return;
        }

        handler.Grow();
        goto Retry;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void AppendFormatted<T, U>(ref T handler, U value, ReadOnlySpan<char> format, IFormatProvider? provider)
        where T : IInterpolatedHandler
        where U : IUtf8SpanFormattable
    {
    Retry:
        if (value.TryFormat(handler.Free, out var written, format, provider))
        {
            handler.BytesWritten += written;
            return;
        }

        handler.Grow();
        goto Retry;
    }

    // TODO: Refactor for inlining-friendly layout
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AppendConstantString<T>(ref T handler, [ConstantExpected] string s)
        where T : struct, IInterpolatedHandler
    {
    Retry:
        var free = handler.Free;
        var inner = new Utf8.TryWriteInterpolatedStringHandler(s.Length, 0, free, out _);

        inner.AppendLiteral(s);
        if (Utf8.TryWrite(free, ref inner, out var written))
        {
            handler.BytesWritten += written;
            return;
        }

        handler.Grow();
        goto Retry;

        // var literal = U8Literals.Utf16.GetLiteral(s);
        // AppendBytes(ref handler, literal.SliceUnsafe(0, literal.Length - 1));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void AppendByte<T>(ref T handler, byte value)
        where T : struct, IInterpolatedHandler
    {
        var free = handler.Free;
        if (free.Length < 1)
        {
            goto Grow;
        }

    Append:
        free.AsRef() = value;
        handler.BytesWritten++;
        return;

    Grow:
        handler.Grow();
        free = handler.Free;
        goto Append;
    }

    // TODO: Add GetBuffer(size)
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void AppendTwoBytes<T>(ref T handler, ushort b01)
        where T : struct, IInterpolatedHandler
    {
        var free = handler.Free;
        if (free.Length < 2)
        {
            goto Grow;
        }

    Append:
        free.AsRef().Cast<byte, ushort>() = b01;
        handler.BytesWritten += 2;
        return;

    Grow:
        handler.Grow();
        free = handler.Free;
        goto Append;
    }

    internal static void AppendBytes<T>(ref T handler, ReadOnlySpan<byte> bytes)
        where T : struct, IInterpolatedHandler
    {
        var free = handler.Free;
        if (free.Length < bytes.Length)
        {
            goto Grow;
        }

    Append:
        bytes.CopyToUnsafe(ref free.AsRef());
        handler.BytesWritten += bytes.Length;
        return;

    Grow:
        handler.Grow(bytes.Length);
        free = handler.Free;
        goto Append;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AppendBytesInlined<T>(ref T handler, ReadOnlySpan<byte> bytes)
        where T : struct, IInterpolatedHandler
    {
        if (bytes.Length is 0) return;
        var free = handler.Free;
        if (free.Length < bytes.Length)
        {
            goto Grow;
        }

    Append:
        if (bytes.Length is 1)
        {
            free.AsRef() = bytes.AsRef();
        }
        else
        {
            bytes.CopyToUnsafe(ref free.AsRef());
        }

        handler.BytesWritten += bytes.Length;
        return;

    Grow:
        handler.Grow(bytes.Length);
        free = handler.Free;
        goto Append;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AppendBytesUnchecked<T>(ref T handler, ReadOnlySpan<byte> bytes)
        where T : struct, IInterpolatedHandler
    {
        Debug.Assert(handler.Free.Length >= bytes.Length);

        bytes.CopyToUnsafe(ref handler.Free.AsRef());
        handler.BytesWritten += bytes.Length;
    }

    [DoesNotReturn, StackTraceHidden]
    static void UnsupportedAppend<T>()
    {
        throw new NotSupportedException(
            $"\nCannot append a value of type '{typeof(T)}' which does not implement '{typeof(IUtf8SpanFormattable)}' or is not '{typeof(Enum)}' or '{typeof(byte[])}'.");
    }
}
