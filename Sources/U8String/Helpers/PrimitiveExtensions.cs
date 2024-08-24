using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text;

namespace U8.Primitives;

static class PrimitiveExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Length(this char c) {
        return (ushort)c switch {
            < 0x80 => 1,
            < 0x800 => 2,
            _ => 3
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Length(this Rune r) {
        return (uint)r.Value switch {
            < 0x80 => 1,
            < 0x800 => 2,
            < 0x10000 => 3,
            _ => 4
        };
    }

    internal static int TotalLength(this U8Range[] ranges)
    {
        var total = 0;
        foreach (var range in ranges)
        {
            total += range.Length;
        }

        return total;
    }

    // From: https://github.com/dotnet/runtime/blob/59a38f18b777155f01c66afde35f2c0a48850cb0/src/libraries/System.Linq/src/System/Linq/Enumerable.cs#L40
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryGetSpan<T>(this IEnumerable<T> source, out ReadOnlySpan<T> span)
    {
        // Use `GetType() == typeof(...)` rather than `is` to avoid cast helpers.  This is measurably cheaper
        // but does mean we could end up missing some rare cases where we could get a span but don't (e.g. a uint[]
        // masquerading as an int[]).  That's an acceptable tradeoff.  The Unsafe usage is only after we've
        // validated the exact type; this could be changed to a cast in the future if the JIT starts to recognize it.

        var result = true;
        if (typeof(T) == typeof(char) && source.GetType() == typeof(string))
        {
            span = Unsafe.As<string>(source).AsSpan().Cast<char, T>();
        }
        else if (source.GetType() == typeof(T[]))
        {
            span = Unsafe.As<T[]>(source);
        }
        else if (source.GetType() == typeof(List<T>))
        {
            span = CollectionsMarshal.AsSpan(Unsafe.As<List<T>>(source));
        }
        // Because we don't have much of further specialization on IEnumerable<T> types, we can afford
        // to pay more upfront because span paths are just this much faster.
        else if (source.GetType() == typeof(ImmutableArray<T>))
        {
            span = Unsafe.Unbox<ImmutableArray<T>>(source).AsSpan();
        }
        else
        {
            span = default;
            result = false;
        }

        return result;
    }

    internal static U8TwoBytes AsTwoBytes(this char c) => new(c);
    internal static U8TwoBytes AsTwoBytes(this Rune r) => new(r);
    internal static U8ThreeBytes AsThreeBytes(this char c) => new(c);
    internal static U8ThreeBytes AsThreeBytes(this Rune r) => new(r);
    internal static U8FourBytes AsFourBytes(this Rune r) => new(r);
}
