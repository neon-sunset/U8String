using System.Collections.Immutable;

namespace U8;

/// <summary>
/// Provides extension methods to integrate <see cref="U8String"/> with the .NET type system.
/// </summary>
public static class U8StringExtensions {
    /// <inheritdoc cref="U8String(ImmutableArray{byte})"/>
    public static U8String AsU8String(this ImmutableArray<byte> value) => new(value);

    /// <inheritdoc cref="U8String(string)"/>
    public static U8String ToU8String(this string value) => new(value);

    /// <inheritdoc cref="U8String(byte[])"/>
    public static U8String ToU8String(this byte[] value) => new(value);

    /// <inheritdoc cref="U8String(ReadOnlySpan{byte})"/>
    public static U8String ToU8String(this Span<byte> value) => new(value);

    /// <inheritdoc cref="U8String(ReadOnlySpan{char})"/>
    public static U8String ToU8String(this Span<char> value) => new(value);

    /// <inheritdoc cref="U8String(ReadOnlySpan{byte})"/>
    public static U8String ToU8String(this ReadOnlySpan<byte> value) => new(value);

    /// <inheritdoc cref="U8String(ReadOnlySpan{char})"/>
    public static U8String ToU8String(this ReadOnlySpan<char> value) => new(value);

    /// <inheritdoc cref="U8String.Create(bool)"/>
    public static U8String ToU8String(this bool value) => U8String.Create(value);

    /// <inheritdoc cref="U8String.Create(byte)"/>
    public static U8String ToU8String(this byte value) => U8String.Create(value);

    /// <summary>
    /// Converts the <see paramref="value"/> to a <see cref="U8String"/> using the default format.
    /// </summary>
    /// <typeparam name="T">The type of the value to convert.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <returns>U8String representation of the value.</returns>
    public static U8String ToU8String<T>(this T value)
    where T : IUtf8SpanFormattable {
        return U8String.Create(value);
    }

    /// <summary>
    /// Converts the <see paramref="value"/> to a <see cref="U8String"/> using the specified format.
    /// </summary>
    /// <typeparam name="T">The type of the value to convert.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="format">The format to use.</param>
    /// <returns>U8String representation of the value.</returns>
    public static U8String ToU8String<T>(this T value, ReadOnlySpan<char> format)
    where T : IUtf8SpanFormattable {
        return U8String.Create(value, format);
    }

    /// <summary>
    /// Converts the <see paramref="value"/> to a <see cref="U8String"/> using the default format and a provider, if specified.
    /// </summary>
    /// <typeparam name="T">The type of the value to convert.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="provider">The format provider to use.</param>
    /// <returns>U8String representation of the value.</returns>
    public static U8String ToU8String<T>(this T value, IFormatProvider? provider)
    where T : IUtf8SpanFormattable {
        return U8String.Create(value, provider);
    }

    /// <summary>
    /// Converts the <see paramref="value"/> to a <see cref="U8String"/> using the specified format and provider.
    /// </summary>
    /// <typeparam name="T">The type of the value to convert.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="format">The format to use.</param>
    /// <param name="provider">The format provider to use.</param>
    /// <returns>U8String representation of the value.</returns>
    public static U8String ToU8String<T>(
        this T value,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    where T : IUtf8SpanFormattable {
        return U8String.Create(value, format, provider);
    }
}
