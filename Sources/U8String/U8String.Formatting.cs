using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using U8.Shared;

namespace U8;

[InterpolatedStringHandler]
[EditorBrowsable(EditorBrowsableState.Advanced)]
// TODO: Add padding support
public /* ref */ struct InlineU8Builder : IInterpolatedHandlerImplementation
{
    const int InitialRentLength = 512;

    InlineBuffer64 _inline;
    byte[]? _rented;

    public IFormatProvider Provider { get; private set; }

    public int BytesWritten { get; internal set; }

    public ReadOnlySpan<byte> Written
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_rented ?? _inline.AsSpan()).SliceUnsafe(0, BytesWritten);
    }

    internal Span<byte> Free
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_rented ?? _inline.AsSpan()).SliceUnsafe(BytesWritten);
    }

    int IInterpolatedHandler.BytesWritten
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BytesWritten;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => BytesWritten = value;
    }

    Span<byte> IInterpolatedHandler.Free => Free;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InlineU8Builder()
    {
        Unsafe.SkipInit(out _inline);
        Provider = CultureInfo.InvariantCulture;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InlineU8Builder(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        Unsafe.SkipInit(out _inline);

        if (length > InlineBuffer64.Length)
        {
            _rented = ArrayPool<byte>.Shared.Rent(length);
        }

        Provider = CultureInfo.InvariantCulture;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InlineU8Builder(
        int literalLength,
        int formattedCount,
        IFormatProvider? formatProvider = null)
    {
        Unsafe.SkipInit(out _inline);

        var initialLength = literalLength + (formattedCount * 12);
        if (initialLength > InlineBuffer64.Length)
        {
            _rented = ArrayPool<byte>.Shared.Rent(initialLength);
        }

        Provider = formatProvider ?? CultureInfo.InvariantCulture;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal InlineU8Builder(int length, bool bytesOnly)
    {
        Debug.Assert(bytesOnly);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        Unsafe.SkipInit(out _inline);

        if (length > InlineBuffer64.Length)
        {
            _rented = ArrayPool<byte>.Shared.Rent(length);
        }

        Provider = null!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLiteral([ConstantExpected] string s) => U8Interpolation.AppendLiteral(ref this, s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(bool value) => U8Interpolation.AppendFormatted(ref this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(char value) => U8Interpolation.AppendFormatted(ref this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(Rune value) => U8Interpolation.AppendFormatted(ref this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(U8String value) => U8Interpolation.AppendFormatted(ref this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(U8String? value) => U8Interpolation.AppendFormatted(ref this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(ReadOnlySpan<byte> value) => U8Interpolation.AppendFormatted(ref this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(string? value) => U8Interpolation.AppendFormatted(ref this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(ReadOnlySpan<char> value) => U8Interpolation.AppendFormatted(ref this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted<T>(T value) => U8Interpolation.AppendFormatted(ref this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted<T>(T value, ReadOnlySpan<char> format)
        where T : IUtf8SpanFormattable => U8Interpolation.AppendFormatted(ref this, value, format);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted<T>(T value, ReadOnlySpan<char> format, IFormatProvider? provider)
        where T : IUtf8SpanFormattable => U8Interpolation.AppendFormatted(ref this, value, format, provider);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AppendBytes(ReadOnlySpan<byte> bytes) => U8Interpolation.AppendBytes(ref this, bytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AppendBytesInlined(ReadOnlySpan<byte> bytes) => U8Interpolation.AppendBytesInlined(ref this, bytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AppendBytesUnchecked(ReadOnlySpan<byte> bytes) => U8Interpolation.AppendBytesUnchecked(ref this, bytes);

    internal void EnsureInitialized()
    {
        Provider ??= CultureInfo.InvariantCulture;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void EnsureCapacity(int length)
    {
        if (Free.Length < length)
        {
            Grow(length);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void IInterpolatedHandler.Grow()
    {
        var arrayPool = ArrayPool<byte>.Shared;
        var rented = _rented;
        var written = (rented ?? _inline.AsSpan())
            .SliceUnsafe(0, BytesWritten);

        var newLength = rented is null
            ? InitialRentLength
            : rented.Length * 2;

        var newArr = arrayPool.Rent(newLength);

        written.CopyToUnsafe(ref newArr.AsRef());
        _rented = newArr;

        if (rented != null)
        {
            arrayPool.Return(rented);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IInterpolatedHandler.Grow(int hint) => Grow(hint);

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void Grow(int hint)
    {
        var arrayPool = ArrayPool<byte>.Shared;
        var rented = _rented;
        var written = (rented ?? _inline.AsSpan())
            .SliceUnsafe(0, BytesWritten);

        var newLength = rented is null
            ? InitialRentLength
            : rented.Length * 2;

        newLength = Math.Max(newLength, written.Length + hint);

        var newArr = arrayPool.Rent(newLength);

        written.CopyToUnsafe(ref newArr.AsRef());
        _rented = newArr;

        if (rented != null)
        {
            arrayPool.Return(rented);
        }
    }

    /// <summary>
    /// Resets the builder.
    /// </summary>
    /// <remarks>
    /// This method does not return the pooled buffer.
    /// It is necessary to call <see cref="Dispose"/> after the builder is no longer needed.
    /// </remarks> 
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => BytesWritten = 0;

    /// <summary>
    /// Resets the builder and returns the pooled buffer if applicable.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        BytesWritten = 0;
        (var rented, _rented) = (_rented, null);

        if (rented != null)
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void ArrayPoolSafeDispose()
    {
        BytesWritten = 0;
        var rented = Interlocked.Exchange(ref _rented, null);
        if (rented != null)
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}

[InterpolatedStringHandler]
[EditorBrowsable(EditorBrowsableState.Never)]
public struct PooledU8Builder : IInterpolatedHandlerImplementation
{
    const int InitialRentLength = 512;

    byte[] _array;

    public IFormatProvider Provider { get; private set; }

    public int BytesWritten { get; internal set; }

    public readonly ReadOnlySpan<byte> Written
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array.SliceUnsafe(0, BytesWritten);
    }

    internal readonly ReadOnlyMemory<byte> WrittenMemory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array.AsMemory(0, BytesWritten);
    }

    internal readonly Span<byte> Free
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array.SliceUnsafe(BytesWritten);
    }

    internal readonly Memory<byte> FreeMemory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array.AsMemory(BytesWritten);
    }

    int IInterpolatedHandler.BytesWritten
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BytesWritten;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => BytesWritten = value;
    }

    readonly Span<byte> IInterpolatedHandler.Free => Free;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PooledU8Builder()
    {
        _array = ArrayPool<byte>.Shared.Rent(InitialRentLength);
        Provider = CultureInfo.InvariantCulture;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PooledU8Builder(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        _array = ArrayPool<byte>.Shared.Rent(length);
        Provider = CultureInfo.InvariantCulture;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PooledU8Builder(
        int literalLength,
        int formattedCount,
        IFormatProvider? formatProvider = null)
    {
        _array = ArrayPool<byte>.Shared.Rent(literalLength + (formattedCount * 96));
        Provider = formatProvider ?? CultureInfo.InvariantCulture;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PooledU8Builder(int length, bool bytesOnly)
    {
        Debug.Assert(bytesOnly);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        _array = ArrayPool<byte>.Shared.Rent(length);
        Provider = null!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLiteral([ConstantExpected] string s) => U8Interpolation.AppendLiteral(ref this, s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(bool value) => U8Interpolation.AppendFormatted(ref this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(char value) => U8Interpolation.AppendFormatted(ref this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(Rune value) => U8Interpolation.AppendFormatted(ref this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(U8String value) => U8Interpolation.AppendFormatted(ref this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(U8String? value) => U8Interpolation.AppendFormatted(ref this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(ReadOnlySpan<byte> value) => U8Interpolation.AppendFormatted(ref this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(string? value) => U8Interpolation.AppendFormatted(ref this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(ReadOnlySpan<char> value) => U8Interpolation.AppendFormatted(ref this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted<T>(T value) => U8Interpolation.AppendFormatted(ref this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted<T>(T value, ReadOnlySpan<char> format)
        where T : IUtf8SpanFormattable => U8Interpolation.AppendFormatted(ref this, value, format);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted<T>(T value, ReadOnlySpan<char> format, IFormatProvider? provider)
        where T : IUtf8SpanFormattable => U8Interpolation.AppendFormatted(ref this, value, format, provider);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AppendBytes(ReadOnlySpan<byte> bytes) => U8Interpolation.AppendBytes(ref this, bytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AppendBytesInlined(ReadOnlySpan<byte> bytes) => U8Interpolation.AppendBytesInlined(ref this, bytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AppendBytesUnchecked(ReadOnlySpan<byte> bytes) => U8Interpolation.AppendBytesUnchecked(ref this, bytes);

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void Grow()
    {
        var arrayPool = ArrayPool<byte>.Shared;
        var rented = _array;
        var newArr = arrayPool.Rent(_array.Length * 2);

        rented
            .SliceUnsafe(0, BytesWritten)
            .CopyToUnsafe(ref newArr.AsRef());

        _array = newArr;
        arrayPool.Return(rented);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IInterpolatedHandler.Grow() => Grow();

    [MethodImpl(MethodImplOptions.NoInlining)]
    void IInterpolatedHandler.Grow(int hint)
    {
        var arrayPool = ArrayPool<byte>.Shared;
        var rented = _array;
        var bytesWritten = BytesWritten;
        var length = Math.Max(rented.Length * 2, bytesWritten + hint);
        var newArr = arrayPool.Rent(length);

        rented
            .SliceUnsafe(0, bytesWritten)
            .CopyToUnsafe(ref newArr.AsRef());

        _array = newArr;
        arrayPool.Return(rented);
    }

    /// <summary>
    /// Resets the builder.
    /// </summary>
    /// <remarks>
    /// This method does not return the pooled buffer.
    /// It is necessary to call <see cref="Dispose"/> after the builder is no longer needed.
    /// </remarks> 
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => BytesWritten = 0;

    /// <summary>
    /// Returns the pooled buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        BytesWritten = 0;
        (var rented, _array) = (_array, null!);

        if (rented != null)
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}

public readonly partial struct U8String
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool TryFormatPresized<T>(T value, out U8String result)
        where T : IUtf8SpanFormattable
    {
        var length = GetFormattedLength<T>();
        var buffer = new byte[length];
        var success = value.TryFormat(buffer, out length, default, CultureInfo.InvariantCulture);

        result = new(buffer, 0, length);
        return success;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool TryFormatPresized<T>(
        T value, ReadOnlySpan<char> format, IFormatProvider provider, out U8String result)
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
        // This table priotizes the common case formatted length
        // given invariant culture and no format string. We pay extra
        // cost for e.g. long dates to ensure minimal heap footprint for
        // numerous u8(DateTime.UtcNow) and similar.
        if (typeof(T) == typeof(sbyte)) return 8;
        if (typeof(T) == typeof(char)) return 4;
        if (typeof(T) == typeof(Rune)) return 8;
        if (typeof(T) == typeof(short)) return 8;
        if (typeof(T) == typeof(ushort)) return 8;
        if (typeof(T) == typeof(int)) return 16;
        if (typeof(T) == typeof(uint)) return 16;
        if (typeof(T) == typeof(long)) return 24;
        if (typeof(T) == typeof(ulong)) return 24;
        if (typeof(T) == typeof(nint)) return 24;
        if (typeof(T) == typeof(nuint)) return 24;
        if (typeof(T) == typeof(float)) return 16;
        if (typeof(T) == typeof(double)) return 24;
        if (typeof(T) == typeof(decimal)) return 32;
        if (typeof(T) == typeof(DateTime)) return 24;
        if (typeof(T) == typeof(DateTimeOffset)) return 32;
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
        while (!value.TryFormat(buffer, out length, default, CultureInfo.InvariantCulture))
        {
            buffer = new byte[buffer.Length * 2];
        }

        return new(buffer, 0, length);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static U8String FormatUnsized<T>(
        T value, ReadOnlySpan<char> format, IFormatProvider provider)
            where T : IUtf8SpanFormattable
    {
        int length;
        var buffer = new byte[64];
        while (!value.TryFormat(buffer, out length, format, provider))
        {
            buffer = new byte[buffer.Length * 2];
        }

        return new(buffer, 0, length);
    }
}
