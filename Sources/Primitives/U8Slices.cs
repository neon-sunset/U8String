using System.Collections;

using U8.Abstractions;

namespace U8.Primitives;

public readonly struct U8Slices : IList<U8String>
{
    readonly byte[]? _source;
    readonly U8Range[]? _ranges;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal U8Slices(byte[]? source, U8Range[] slices)
    {
        _source = source;
        _ranges = slices;
    }

    public U8String this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_source, _ranges![index]);
        set => throw new NotSupportedException();
    }

    public bool IsEmpty => _source is null;

    public int Count => _ranges?.Length ?? 0;

    bool ICollection<U8String>.IsReadOnly => true;

    // TODO: Optimize this
    public bool Contains(U8String item)
    {
        var source = _source;
        var ranges = _ranges;
        if (ranges != null)
        {
            for (var i = 0; i < ranges.Length; i++)
            {
                if (item.Equals(new U8String(source, ranges[i])))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // TODO: Optimize this
    public void CopyTo(U8String[] array, int arrayIndex)
    {
        var span = array.AsSpan()[arrayIndex..][..Count];
        var source = _source;
        var ranges = _ranges;
        ref var dst = ref span.AsRef();

        if (ranges != null)
        {
            for (var i = 0; i < ranges.Length; i++)
            {
                dst.Add(i) = new U8String(source, ranges[i]);
            }
        }
    }

    // TODO: Optimize this
    public int IndexOf(U8String item)
    {
        var source = _source;
        var ranges = _ranges;
        if (ranges != null)
        {
            for (var i = 0; i < ranges.Length; i++)
            {
                if (item.Equals(new U8String(source, ranges[i])))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    public Enumerator GetEnumerator() => new(_source, _ranges ?? []);

    IEnumerator<U8String> IEnumerable<U8String>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IU8Enumerator
    {
        readonly byte[]? _source;
        readonly U8Range[] _ranges;
        int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(byte[]? source, U8Range[] ranges)
        {
            _source = source;
            _ranges = ranges;
            _index = -1;
        }

        public readonly U8String Current => new(_source, _ranges.AsRef(_index));

        readonly object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            var index = _index + 1;
            if ((uint)index < (uint)_ranges.Length)
            {
                _index = index;
                return true;
            }

            return false;
        }

        public void Reset() => _index = -1;

        public void Dispose() { }
    }

    void IList<U8String>.Insert(int index, U8String item) => throw new NotSupportedException();
    void IList<U8String>.RemoveAt(int index) => throw new NotSupportedException();
    void ICollection<U8String>.Add(U8String item) => throw new NotSupportedException();
    void ICollection<U8String>.Clear() => throw new NotSupportedException();
    bool ICollection<U8String>.Remove(U8String item) => throw new NotSupportedException();
}
