using System.Diagnostics.CodeAnalysis;
using System.Collections;

using U8.Primitives;
using System.Text;

namespace U8.Prototypes;

// TODO: Flatten certain impl. bits to reduce inlining and locals pressure
[SkipLocalsInit]
readonly struct Split<T>: ICollection<U8String>
    where T : struct {
    readonly U8String _source;
    readonly T _pattern;

    internal Split(U8String source, T pattern) {
        _source = source;
        _pattern = pattern;
    }

    public int Count {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // get => _pattern.CountSegments(_source);
        get => throw new NotImplementedException();
    }

    public bool Contains(U8String item) {
        // TODO: ContainsSegment
        throw new NotImplementedException();
    }

    // TODO: Optimize calling convention by moving to a static helper
    public int CopyTo(Span<U8String> destination) {
        var index = 0;
        foreach (var item in this) {
            destination[index++] = item;
        }
        return index + 1;
    }

    public int CopyTo(Span<U8Range> destination) {
        var index = 0;
        foreach (var item in this) {
            destination[index++] = item.Range;
        }
        return index + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_source, _pattern);

    IEnumerator<U8String> IEnumerable<U8String>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    bool ICollection<U8String>.IsReadOnly => true;
    void ICollection<U8String>.Add(U8String item) => throw new NotSupportedException();
    void ICollection<U8String>.CopyTo(U8String[] array, int arrayIndex) => CopyTo(array.AsSpan(arrayIndex));
    void ICollection<U8String>.Clear() => throw new NotSupportedException();
    bool ICollection<U8String>.Remove(U8String item) => throw new NotSupportedException();

    public struct Enumerator: IEnumerator<U8String> {
        readonly byte[] _bytes;
        readonly T _pattern;
        bool _done;

        (int Offset, int Length) _current;
        (int Offset, int Length) _remainder;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(U8String source, T pattern) {
            _bytes = source._value!;
            _pattern = pattern;
            _current = default;
            _remainder = (source._inner.Offset, source._inner.Length);
        }

        public readonly U8String Current {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(_bytes, new(_current.Offset, _current.Length));
        }

        // FIXME: This currently does not return true once for
        // empty sources, which is inconsistent with other logic.
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() {
            if (_done) {
                return false;
            }

            var (offset, length) = _remainder;
            var bytes = _bytes.SliceUnsafe(offset, length);
            var match = _pattern.NextSegment(bytes);

            var current = (
                offset + match.SegmentOffset,
                match.SegmentLength);

            var remainder = (
                offset + match.RemainderOffset,
                length - match.RemainderOffset);

            if (!match.IsFound) {
                _done = true;
            }

            (_current, _remainder) = (current, remainder);

            return true;
        }

        [SuppressMessage(
            "Style",
            "IDE0251:Make member 'readonly'",
            Justification = "No. This *cannot* be made readonly." +
            "_pattern is likely to be a mutable struct if it's disposable!")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() {
            //if (_pattern is IDisposable)
            //{
            //    ((IDisposable)_pattern).Dispose();
            //}
        }

        readonly object IEnumerator.Current => Current;
        readonly void IEnumerator.Reset() => throw new NotSupportedException();
    }
}

static class SplitExtensions {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Split<BytePattern> NewSplit(this U8String source, byte pattern) {
        ThrowHelpers.CheckAscii(pattern);
        return new(source, new(pattern));
    }

    [SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Split<CharPattern> NewSplit(this U8String source, char pattern) {
        ThrowHelpers.CheckSurrogate(pattern);
        return new(source, new(pattern));
    }

    public static Split<Rune> NewSplit(this U8String source, Rune pattern) {
        return new(source, pattern);
    }

    public static Split<U8String> NewSplit(this U8String source, U8String pattern) {
        return new(source, pattern);
    }

    // public static Split<EitherBytePattern> NewSplitAny(this U8String source, byte a, byte b) {
    //     ThrowHelpers.CheckAscii(a);
    //     ThrowHelpers.CheckAscii(b);
    //     return new(source, new(a, b));
    // }
}
