using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using U8.Primitives;
using U8.Shared;

namespace U8.Prototypes;

// On RegexPattern: there will be a pattern iterator abstraction.
interface Pattern {
    // https://github.com/dotnet/runtime/issues/104511
    public static ByteLookupPattern AsciiWhitespace => Holder.AsciiWhitespace;
    public static ByteLookupPattern AsciiUpper => Holder.AsciiUpper;
    public static ByteLookupPattern AsciiLower => Holder.AsciiLower;

    Match Find(bytes source);
    Match FindLast(bytes source);

    static class Holder {
        public static readonly ByteLookupPattern AsciiWhitespace = new("\t\n\v\f\r "u8);
        public static readonly ByteLookupPattern AsciiUpper = new("ABCDEFGHIJKLMNOPQRSTUVWXYZ"u8);
        public static readonly ByteLookupPattern AsciiLower = new("abcdefghijklmnopqrstuvwxyz"u8);
    }
}

interface ExtendedPattern: Pattern {
    int? Length { get; }
    bool Contains(bytes source);
    int Count(bytes source);
}

readonly struct Primitive<T>: ExtendedPattern {
    readonly T _value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Primitive(T value) {
        ThrowHelpers.CheckPattern(value);
        _value = value;
    }

    // TODO: Optimize later?
    public int? Length => _value switch {
        byte => 1,
        char c => c.Length(),
        Rune r => r.Length(),
        U8String s => s.Length,
        _ => ThrowHelpers.Unreachable<int>()
    };

    public bool Contains(bytes source) => _value switch {
        byte b => source.Contains(b),
        char c => c < 0x80
            ? source.Contains((byte)c)
            : source.IndexOf(c < 0x800
                ? c.AsTwoBytes()
                : c.AsThreeBytes()) > -1,
        Rune r => (uint)r.Value < 0x80
            ? source.Contains((byte)r.Value)
            : source.IndexOf(r.Value switch {
                < 0x800 => r.AsTwoBytes(),
                < 0x10000 => r.AsThreeBytes(),
                _ => r.AsFourBytes()
            }) > -1,
        U8String s => source.IndexOf(s) > -1,
        _ => ThrowHelpers.Unreachable<bool>()
    };

    public int Count(bytes source) => _value switch {
        byte b => source.Count(b),
        char c => c < 0x80
            ? source.Count((byte)c)
            : source.Count(c < 0x800
                ? c.AsTwoBytes()
                : c.AsThreeBytes()),
        Rune r => (uint)r.Value < 0x80
            ? source.Count((byte)r.Value)
            : source.Count(r.Value switch {
                < 0x800 => r.AsTwoBytes(),
                < 0x10000 => r.AsThreeBytes(),
                _ => r.AsFourBytes()
            }),
        U8String s => source.Count(s),
        _ => ThrowHelpers.Unreachable<int>()
    };

    public Match Find(bytes source) => _value switch {
        byte b => new(source.IndexOf(b), 1),
        char c when c < 0x80 => new(source.IndexOf((byte)c), 1),
        Rune r when (uint)r.Value < 0x80 => new(source.IndexOf((byte)r.Value), 1),
        U8String s => new(source.IndexOf(s), s.Length),
        _ => source.FindNonAscii(_value)
    };

    public Match FindLast(bytes source) => _value switch {
        byte b => new(source.LastIndexOf(b), 1),
        char c when c < 0x80 => new(source.LastIndexOf((byte)c), 1),
        Rune r when (uint)r.Value < 0x80 => new(source.LastIndexOf((byte)r.Value), 1),
        U8String s => new(source.LastIndexOf(s), s.Length),
        _ => source.FindLastNonAscii(_value)
    };
}

[SkipLocalsInit]
static class PatternExtensions {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CountSegments<T>(this T pattern, bytes source) {
        return pattern switch {
            byte b => source.Count(b) + 1,
            char c => CountCharSplit(source, c) + 1,
            Rune r => CountRuneSplit(source, r) + 1,
            ExtendedPattern => ((ExtendedPattern)pattern).Count(source) + 1,
            Pattern => CountPatternSplit(source, pattern) + 1,
            Splitter => ((Splitter)pattern).CountSegments(source),
            _ => Unsupported<T, int>(),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int CountCharSplit(bytes source, char c) {
        return c < 0x80
            ? source.Count((byte)c) + 1
            : source.Count(c < 0x800 ? c.AsTwoBytes() : c.AsThreeBytes()) + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int CountRuneSplit(bytes source, Rune r) {
        return (uint)r.Value < 0x80
            ? source.Count((byte)r.Value) + 1
            : source.Count(r.Value switch {
                < 0x800 => r.AsTwoBytes(),
                < 0x10000 => r.AsThreeBytes(),
                _ => r.AsFourBytes()
            }) + 1;
    }

    static int CountPatternSplit<T>(bytes source, T pattern) {
        Debug.Assert(pattern is Pattern);
        throw new NotImplementedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Match FindNonAscii<T>(this bytes source, T pattern) {
        Debug.Assert(pattern is not (byte or U8String or Pattern or Splitter));
        return pattern switch {
            char c => FindNonAsciiChar(source, c),
            Rune r => FindNonAsciiRune(source, r),
            _ => Unsupported<T, Match>(),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static Match FindNonAsciiChar(bytes source, char c) {
        Debug.Assert(c > 0x7F);
        int length;
        bytes needle;
        if (c < 0x800) {
            needle = c.AsTwoBytes().AsSpan();
            length = 2;
        }
        else {
            needle = c.AsThreeBytes().AsSpan();
            length = 3;
        }
        return new(source.IndexOf(needle), length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static Match FindNonAsciiRune(bytes source, Rune r) {
        Debug.Assert(r.Value > 0x7F);
        int length;
        bytes needle;
        if (r.Value < 0x800) {
            needle = r.AsTwoBytes().AsSpan();
            length = 2;
        }
        else if (r.Value < 0x10000) {
            needle = r.AsThreeBytes().AsSpan();
            length = 3;
        }
        else {
            needle = r.AsFourBytes().AsSpan();
            length = 4;
        }
        return new(source.IndexOf(needle), length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Match FindLastNonAscii<T>(this bytes source, T pattern) {
        Debug.Assert(pattern is not (byte or U8String or Pattern or Splitter));
        throw new NotImplementedException();
        // return pattern switch {
        //     char c => FindLastNonAsciiChar(source, c),
        //     Rune r => FindLastNonAsciiRune(source, r),
        //     _ => Unsupported<T, Match>(),
        // };
    }

    [DoesNotReturn, StackTraceHidden]
    static U Unsupported<T, U>() {
        throw new NotSupportedException($"{typeof(T)} is not a supported pattern type.");
    }
}

interface Splitter {
    SegmentMatch FindSegment(bytes source);
    SegmentMatch FindLastSegment(bytes source);
}

interface ExtendedSplitter: Splitter {
    bool ContainsSegment(bytes source);
    int CountSegments(bytes source);
    int FillSegments(Span<U8Range> segments, bytes source);
    int FillSegments(Span<U8String> segments, bytes source);
}

readonly struct TrimWhitespace<T>: Splitter {
    readonly T _pattern;

    public TrimWhitespace(T pattern) {
        ThrowHelpers.CheckPattern(pattern);
        _pattern = pattern;
    }

    public SegmentMatch FindSegment(bytes source) {
        throw new NotImplementedException();
    }

    public SegmentMatch FindLastSegment(bytes source) {
        throw new NotImplementedException();
    }
}

readonly struct SkipEmpty<T>: Splitter {
    readonly T _pattern;

    public SkipEmpty(T pattern) {
        ThrowHelpers.CheckPattern(pattern);
        _pattern = pattern;
    }

    // We have to specialize this just like Split<T> itself
    // to ensure optimal codegen that prevents inlining artifacts.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SegmentMatch FindSegment(bytes source) => _pattern switch {
        Splitter => FindSplitter(source, _pattern),
        Pattern => FindPattern(source, _pattern),
        _ => FindPattern(source, new Primitive<T>(_pattern)),
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static SegmentMatch FindPattern<U>(bytes source, U pattern) {
        Debug.Assert(pattern is Pattern);

        var offset = 0;
        while (offset < source.Length) {
            var match = ((Pattern)pattern)
                .Find(source.SliceUnsafe(offset));
            var remainder = match.Offset + match.Length;

            var segment = match.IsFound
                ? new(offset, match.Offset, offset + remainder)
                : SegmentMatch.Last(offset, source.Length - offset);

            if (segment.SegmentLength > 0) {
                return segment;
            }

            offset += remainder;
        }

        return SegmentMatch.NotFound;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static SegmentMatch FindSplitter(bytes source, T splitter) {
        Debug.Assert(splitter is Splitter);

        var offset = 0;
        while (offset < source.Length) {
            var segment = ((Splitter)splitter)
                .FindSegment(source.SliceUnsafe(offset));

            if (segment.SegmentLength > 0) {
                return segment.ShiftBy(offset);
            }
            if (segment.IsLast) {
                break;
            }

            offset += segment.RemainderOffset;
        }

        return SegmentMatch.NotFound;
    }

    public SegmentMatch FindLastSegment(bytes source) {
        throw new NotImplementedException();
    }
}

// readonly struct SkipEmptySplitter<T>: Splitter
// where T: struct {
//     readonly T _pattern;

//     public SkipEmptySplitter(T pattern) {
//         ThrowHelpers.CheckPattern(pattern);
//         _pattern = pattern;
//     }

//     public SegmentMatch FindSegment(bytes source) {
//         var offset = 0;
//         do {
//             var haystack = source.SliceUnsafe(offset);
//             int segmentOffset, segmentLength, remainderOffset;
//             if (_pattern is not Splitter) {
//                 var matchOffset = _pattern.Find(haystack, out var matchLength);
//                 segmentOffset = 0;
//                 segmentLength = matchOffset;
//                 remainderOffset = matchOffset + matchLength;
//             }
//             else {
//                 (segmentOffset, segmentLength, remainderOffset) =
//                     ((Splitter)_pattern).FindSegment(haystack);
//             }

//             if (segmentLength > 0) {
//                 return new(
//                     segmentOffset + offset,
//                     segmentLength,
//                     remainderOffset + offset);
//             }

//             if (segmentLength < 0) break;
//             offset += remainderOffset;
//         } while (offset < source.Length);
//         return SegmentMatch.NotFound;
//     }

//     public SegmentMatch FindLastSegment(bytes source) {
//         throw new NotImplementedException();
//     }
// }

readonly struct Match(int offset, int length) {
    public bool IsFound => Offset > -1;
    public readonly int Offset = offset;
    public readonly int Length = length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator SegmentMatch(Match match) =>
        new(0, match.Offset, match.Offset + match.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator (int Offset, int Length)(Match match) =>
        Unsafe.BitCast<Match, (int, int)>(match);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out int offset, out int length) {
        offset = Offset;
        length = Length;
    }
}

// SplitMatch?
readonly struct SegmentMatch(
    int segmentOffset,
    int segmentLength,
    int remainderOffset) {

    public bool IsFound => SegmentLength > -1;
    public bool IsLast => RemainderOffset < 0;
    public readonly int SegmentOffset = segmentOffset;
    public readonly int SegmentLength = segmentLength;
    public readonly int RemainderOffset = remainderOffset;

    public static SegmentMatch NotFound => new(0, -1, 0);
    public static SegmentMatch Last(int offset, int length) => new(offset, length, -1);

    internal SegmentMatch ShiftBy(int offset) {
        return new(SegmentOffset + offset, SegmentLength, RemainderOffset + offset);
    }

    public void Deconstruct(out int segmentOffset, out int segmentLength, out int remainderOffset) {
        segmentOffset = SegmentOffset;
        segmentLength = SegmentLength;
        remainderOffset = RemainderOffset;
    }
}

readonly struct EitherBytePattern: ExtendedPattern {
    readonly byte _a, _b;

    public EitherBytePattern(byte a, byte b) {
        ThrowHelpers.CheckAscii((byte)(a | b));
        (_a, _b) = (a, b);
    }

    public int? Length => 1;

    public bool Contains(bytes source) {
        throw new NotImplementedException();
    }

    public int Count(bytes source) {
        return (int)(uint)U8Searching.CountEitherByte(
            _a, _b, ref source.AsRef(), (uint)source.Length);
    }

    public Match Find(bytes source) => new(source.IndexOfAny(_a, _b), 1);
    public Match FindLast(bytes source) => new(source.LastIndexOfAny(_a, _b), 1);
}

readonly struct ByteLookupPattern: Pattern {
    readonly SearchValues<byte> _values;

    public ByteLookupPattern(bytes values) {
        if (!Ascii.IsValid(values)) {
            ThrowHelpers.InvalidAscii();
        }
        _values = SearchValues.Create(values);
    }

    public Match Find(bytes source) => new(source.IndexOfAny(_values), 1);
    public Match FindLast(bytes source) => new(source.LastIndexOfAny(_values), 1);
}

// readonly struct PatternSplitter<T> where T : Pattern...
// readonly struct SplitEnumerator<TSplitter> where TSplitter : Splitter...
// Split<TPattern> -> SplitEnumerator<PatternSplitter<TPattern>>
