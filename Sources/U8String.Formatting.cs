using System.Buffers;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using U8.Primitives;
using U8.Shared;

namespace U8;

[InterpolatedStringHandler]
[EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable RCS1003 // Add braces to multi-line expression. Why: more compact and readable here.
public struct InterpolatedU8StringHandler
{
    static readonly ConditionalWeakTable<string, byte[]> LiteralPool = [];

    readonly IFormatProvider? _provider;
    InlineBuffer128 _inline;
    byte[]? _rented;

    public int BytesWritten { get; private set; }

    public ReadOnlySpan<byte> Written
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_rented ?? _inline.AsSpan()).SliceUnsafe(0, BytesWritten);
    }

    Span<byte> Free
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_rented ?? _inline.AsSpan()).SliceUnsafe(BytesWritten);
    }

    public InterpolatedU8StringHandler(
        int literalLength,
        int formattedCount,
        IFormatProvider? formatProvider = null)
    {
        Unsafe.SkipInit(out _inline);

        var initialLength = literalLength + (formattedCount * 12);
        if (initialLength > InlineBuffer128.Length)
        {
            _rented = ArrayPool<byte>.Shared.Rent(initialLength);
        }

        _provider = formatProvider;
    }

    // Reference: https://github.com/dotnet/runtime/issues/93501
    // Refactor once inlined TryGetBytes gains UTF8EncodingSealed.ReadUtf8 call
    // which JIT/AOT can optimize away for string literals, eliding the transcoding.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLiteral([ConstantExpected] string s)
    {
        if (s.Length > 0)
        {
            if (s.Length is 1 && char.IsAscii(s[0]))
            {
                AppendByte((byte)s[0]);
                return;
            }

            AppendLiteralString(s);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void AppendLiteral(ReadOnlySpan<char> s)
    {
    Retry:
        if (Encoding.UTF8.TryGetBytes(s, Free, out var written))
        {
            BytesWritten += written;
            return;
        }

        Grow();
        goto Retry;
    }

    public void AppendFormatted(bool value)
    {
        AppendBytes(value ? "True"u8 : "False"u8);
    }

    public void AppendFormatted(char value)
    {
        ThrowHelpers.CheckSurrogate(value);

        if (char.IsAscii(value))
        {
            AppendByte((byte)value);
            return;
        }

        AppendBytes(new U8Scalar(value, checkAscii: false).AsSpan());
    }

    public void AppendFormatted(Rune value)
    {
        if (value.IsAscii)
        {
            AppendByte((byte)value.Value);
            return;
        }

        AppendBytes(new U8Scalar(value, checkAscii: false).AsSpan());
    }

    public void AppendFormatted(U8String value)
    {
        if (!value.IsEmpty)
        {
            AppendBytes(value.UnsafeSpan);
        }
    }

    public void AppendFormatted(ReadOnlySpan<byte> value)
    {
        U8String.Validate(value);
        AppendBytes(value);
    }

    public void AppendFormatted(ReadOnlySpan<char> value)
    {
        AppendLiteral(value);
    }

    // Explicit no-format overload for more compact codegen
    // and specialization so that *if* TryFormat is inlined into
    // the body, the format-specific branches are optimized away.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void AppendFormatted<T>(T value)
        where T : IUtf8SpanFormattable
    {
    Retry:
        if (value.TryFormat(Free, out var written, default, _provider))
        {
            BytesWritten += written;
            return;
        }

        Grow();
        goto Retry;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void AppendFormatted<T>(T value, ReadOnlySpan<char> format)
        where T : IUtf8SpanFormattable
    {
    Retry:
        if (value.TryFormat(Free, out var written, format, _provider))
        {
            BytesWritten += written;
            return;
        }

        Grow();
        goto Retry;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void AppendLiteralString(string s)
    {
        if (!LiteralPool.TryGetValue(s, out var literal))
        {
            literal = Encoding.UTF8.GetBytes(s);
            LiteralPool.AddOrUpdate(s, literal);
        }

        AppendBytes(literal);
    }

    void AppendByte(byte value)
    {
    Retry:
        var free = Free;
        if (free.Length > 0)
        {
            free[0] = value;
            BytesWritten++;
            return;
        }

        Grow();
        goto Retry;
    }

    void AppendBytes(ReadOnlySpan<byte> bytes)
    {
    Retry:
        var free = Free;
        if (free.Length >= bytes.Length)
        {
            bytes.CopyToUnsafe(ref free.AsRef());
            BytesWritten += bytes.Length;
            return;
        }

        Grow();
        goto Retry;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void Grow()
    {
        const int initialRentLength = 1024;

        var arrayPool = ArrayPool<byte>.Shared;
        var rented = _rented;
        var written = (rented ?? _inline.AsSpan())
            .SliceUnsafe(0, BytesWritten);

        var newLength = rented is null
            ? initialRentLength
            : rented.Length * 2;

        var newArr = arrayPool.Rent(newLength);

        written.CopyToUnsafe(ref newArr.AsRef());
        _rented = newArr;

        if (rented != null)
        {
            arrayPool.Return(rented);
        }
    }

    internal readonly void Dispose()
    {
        var rented = _rented;
        if (rented != null)
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}

public readonly partial struct U8String
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static U8String Format(ref InterpolatedU8StringHandler handler)
    {
        var result = new U8String(handler.Written, skipValidation: true);
        handler.Dispose();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool TryFormatLiteral<T>(T value, out U8String literal)
    {
        if (value is byte u8)
        {
            literal = U8Literals.GetByte(u8);
            return true;
        }
        else if (value is sbyte i8 && U8Literals.TryGetInt8(i8, out literal)) return true;
        else if (value is short i16 && U8Literals.TryGetInt16(i16, out literal)) return true;
        else if (value is ushort u16 && U8Literals.TryGetUInt16(u16, out literal)) return true;
        else if (value is int i32 && U8Literals.TryGetInt32(i32, out literal)) return true;
        else if (value is uint u32 && U8Literals.TryGetUInt32(u32, out literal)) return true;
        else if (value is long i64 && U8Literals.TryGetInt64(i64, out literal)) return true;
        else if (value is ulong u64 && U8Literals.TryGetUInt64(u64, out literal)) return true;

        Unsafe.SkipInit(out literal);
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool TryFormatPresized<T>(T value, out U8String result)
        where T : IUtf8SpanFormattable
    {
        var length = GetFormattedLength<T>();
        var buffer = new byte[length];
        var success = value.TryFormat(buffer, out length, default, null);

        result = new(buffer, 0, length);
        return success;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool TryFormatPresized<T>(
        T value, ReadOnlySpan<char> format, IFormatProvider? provider, out U8String result)
            where T : IUtf8SpanFormattable
    {
        var length = GetFormattedLength<T>();
        var buffer = new byte[length]; // Most cases will be shorter than this, no need to add extra null terminator
        var success = value.TryFormat(buffer, out length, format, provider);

        result = new(buffer, 0, length);
        return success;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int GetFormattedLength<T>() where T : IUtf8SpanFormattable
    {
        if (typeof(T) == typeof(sbyte)) return 8;
        if (typeof(T) == typeof(char)) return 4;
        if (typeof(T) == typeof(Rune)) return 8;
        if (typeof(T) == typeof(short)) return 8;
        if (typeof(T) == typeof(ushort)) return 8;
        if (typeof(T) == typeof(int)) return 12;
        if (typeof(T) == typeof(uint)) return 12;
        if (typeof(T) == typeof(long)) return 24;
        if (typeof(T) == typeof(ulong)) return 20;
        if (typeof(T) == typeof(nint)) return 24;
        if (typeof(T) == typeof(nuint)) return 20;
        if (typeof(T) == typeof(float)) return 16;
        if (typeof(T) == typeof(double)) return 24;
        if (typeof(T) == typeof(decimal)) return 32;
        if (typeof(T) == typeof(DateTime)) return 32;
        if (typeof(T) == typeof(DateTimeOffset)) return 40;
        if (typeof(T) == typeof(TimeSpan)) return 24;
        if (typeof(T) == typeof(Guid)) return 40;

        return 32;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static U8String FormatUnsized<T>(T value)
        where T : IUtf8SpanFormattable
    {
        int length;
        var buffer = new byte[64];
        while (!value.TryFormat(buffer, out length, default, null))
        {
            buffer = new byte[buffer.Length * 2];
        }

        return new(buffer, 0, length);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static U8String FormatUnsized<T>(
        T value, ReadOnlySpan<char> format, IFormatProvider? provider)
            where T : IUtf8SpanFormattable
    {
        // TODO: Maybe it's okay to steal from array pool?
        int length;
        var buffer = new byte[64];
        while (!value.TryFormat(buffer, out length, format, provider))
        {
            buffer = new byte[buffer.Length * 2];
        }

        return new(buffer, 0, length);
    }
}
