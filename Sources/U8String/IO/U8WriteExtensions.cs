using U8.Serialization;

namespace U8.IO;

// Overloads order:
// Write(interpolation)
// Write<T>(formattable)
// WriteAsync(interpolation)
// WriteAsync<T>(formattable)
// WriteLine(u8string)
// WriteLine(u8bytes)
// WriteLine(interpolation)
// WriteLine<T>(formattable)
// TODO: culture-sensitive and format-specific overloads
public static partial class U8WriteExtensions
{
    internal interface IWriteable
    {
        void Write(ReadOnlySpan<byte> value);
        void WriteDispose(ref InlineU8Builder builder);

        ValueTask WriteAsync(ReadOnlyMemory<byte> value, CancellationToken ct);
        ValueTask WriteDisposeAsync(PooledU8Builder builder, CancellationToken ct);
    }

    internal static ReadOnlySpan<byte> NewLine
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => OperatingSystem.IsWindows() ? "\r\n"u8 : "\n"u8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteBuilder<T>(T destination, ref InlineU8Builder builder)
        where T : IWriteable
    {
        destination.WriteDispose(ref builder);
    }

    internal static void WriteUtf8Formattable<T, U>(T destination, U value)
        where T : IWriteable
        where U : IUtf8SpanFormattable
    {
        var builder = new InlineU8Builder();
        builder.AppendFormatted(value);
        destination.WriteDispose(ref builder);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ValueTask WriteBuilderAsync<T>(T destination, PooledU8Builder builder, CancellationToken ct)
        where T : IWriteable
    {
        return destination.WriteDisposeAsync(builder, ct);
    }

    static ValueTask WriteUtf8FormattableAsync<T, U>(T destination, U value, CancellationToken ct)
        where T : IWriteable
        where U : IUtf8SpanFormattable
    {
        var builder = new PooledU8Builder();
        builder.AppendFormatted(value);
        return destination.WriteDisposeAsync(builder, ct);
    }

    internal static void WriteLineSpan<T>(T destination, ReadOnlySpan<byte> value)
        where T : IWriteable
    {
        var length = value.Length + NewLine.Length;
        var builder = new InlineU8Builder(length, bytesOnly: true);
        var buffer = builder.Free;
        buffer.WriteUnchecked(value);
        buffer.WriteUnchecked(NewLine, value.Length);
        builder.BytesWritten = length;
        destination.WriteDispose(ref builder);
    }

    internal static void WriteLineBuilder<T>(T destination, ref InlineU8Builder builder)
        where T : IWriteable
    {
        builder.AppendBytesInlined(NewLine);
        destination.WriteDispose(ref builder);
    }

    internal static void WriteLineUtf8Formattable<T, U>(T destination, U value)
        where T : IWriteable
        where U : IUtf8SpanFormattable
    {
        var builder = new InlineU8Builder();
        builder.AppendFormatted(value);
        builder.AppendBytesInlined(NewLine);
        destination.WriteDispose(ref builder);
    }

    static ValueTask WriteLineMemoryAsync<T>(T destination, ReadOnlyMemory<byte> value, CancellationToken ct)
        where T : IWriteable
    {
        var length = value.Length + NewLine.Length;
        var builder = new PooledU8Builder(length, bytesOnly: true);
        var buffer = builder.Free;
        buffer.WriteUnchecked(value.Span);
        buffer.WriteUnchecked(NewLine, value.Length);
        builder.BytesWritten = length;
        return destination.WriteDisposeAsync(builder, ct);
    }

    static ValueTask WriteLineBuilderAsync<T>(T destination, PooledU8Builder builder, CancellationToken ct)
        where T : IWriteable
    {
        builder.AppendBytesInlined(NewLine);
        return destination.WriteDisposeAsync(builder, ct);
    }

    static ValueTask WriteLineUtf8FormattableAsync<T, U>(T destination, U value, CancellationToken ct)
        where T : IWriteable
        where U : IUtf8SpanFormattable
    {
        var builder = new PooledU8Builder();
        builder.AppendFormatted(value);
        builder.AppendBytesInlined(NewLine);
        return destination.WriteDisposeAsync(builder, ct);
    }
}

public static partial class U8WriteEnumExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void WriteEnum<T, U>(T destination, U value)
        where T : U8WriteExtensions.IWriteable
        where U : struct, Enum
    {
        destination.Write(value.ToU8String());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ValueTask WriteEnumAsync<T, U>(T destination, U value, CancellationToken ct)
        where T : U8WriteExtensions.IWriteable
        where U : struct, Enum
    {
        return destination.WriteAsync(value.ToU8String(), ct);
    }

    static void WriteLineEnum<T, U>(T destination, U value)
        where T : U8WriteExtensions.IWriteable
        where U : struct, Enum
    {
        var formatted = value.ToU8String();
        var newline = U8WriteExtensions.NewLine;
        var length = formatted.Length + newline.Length;

        var builder = new InlineU8Builder(length, bytesOnly: true);
        var buffer = builder.Free;
        buffer.WriteUnchecked(formatted);
        buffer.WriteUnchecked(newline, formatted.Length);
        builder.BytesWritten = length;
        destination.WriteDispose(ref builder);
    }

    static ValueTask WriteLineEnumAsync<T, U>(T destination, U value, CancellationToken ct)
        where T : U8WriteExtensions.IWriteable
        where U : struct, Enum
    {
        var formatted = value.ToU8String();
        var newline = U8WriteExtensions.NewLine;
        var length = formatted.Length + newline.Length;

        var builder = new PooledU8Builder(length, bytesOnly: true);
        var buffer = builder.Free;
        buffer.WriteUnchecked(formatted);
        buffer.WriteUnchecked(newline, formatted.Length);
        builder.BytesWritten = length;
        return destination.WriteDisposeAsync(builder, ct);
    }
}
