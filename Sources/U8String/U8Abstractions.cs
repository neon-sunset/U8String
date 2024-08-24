namespace U8.Abstractions;

public interface IU8Comparer :
    IComparer<U8String>,
    IU8EqualityComparer,
    IU8ContainsOperator,
    IU8CountOperator,
    IU8IndexOfOperator,
    IU8LastIndexOfOperator,
    IU8StartsWithOperator,
    IU8EndsWithOperator
{
    int Compare(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y);
}

public interface IU8ContainsOperator
{
    bool Contains(ReadOnlySpan<byte> source, byte value);
    bool Contains(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value);
}

public interface IU8CountOperator
{
    int Count(ReadOnlySpan<byte> source, byte value);
    int Count(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value);
}

// Note: it is expected for these to be called by U8Searching and similar, so that
// interface implementations don't have to double-check for length is 1 -> fast path.
// Flow: public method -> (enumerator ->) U8Searching/Impls -> interface implementation
public interface IU8IndexOfOperator
{
    (int Offset, int Length) IndexOf(ReadOnlySpan<byte> source, byte value);
    (int Offset, int Length) IndexOf(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value);
}

public interface IU8LastIndexOfOperator
{
    (int Offset, int Length) LastIndexOf(ReadOnlySpan<byte> source, byte value);
    (int Offset, int Length) LastIndexOf(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value);
}

public interface IU8StartsWithOperator
{
    bool StartsWith(ReadOnlySpan<byte> source, byte value);
    bool StartsWith(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value);
}

public interface IU8EndsWithOperator
{
    bool EndsWith(ReadOnlySpan<byte> source, byte value);
    bool EndsWith(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value);
}

public interface IU8EqualityComparer : IEqualityComparer<U8String>
{
    bool Equals(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y);
    int GetHashCode(ReadOnlySpan<byte> value);
}

// TODO: Member naming?
public interface IU8CaseConverter
{
    bool IsFixedLength { get; }

    int FindToLowerStart(ReadOnlySpan<byte> source);
    int FindToUpperStart(ReadOnlySpan<byte> source);

    int ToLower(ReadOnlySpan<byte> source, Span<byte> destination);
    int ToUpper(ReadOnlySpan<byte> source, Span<byte> destination);

    void ToLower(ReadOnlySpan<byte> source, ref InlineU8Builder destination);
    void ToUpper(ReadOnlySpan<byte> source, ref InlineU8Builder destination);
}

interface IEnumerable<T, TEnumerator> : IEnumerable<T>
    where TEnumerator : struct, IEnumerator<T>
{
    new TEnumerator GetEnumerator();
}

interface IU8Enumerable<TEnumerator> : IEnumerable<U8String, TEnumerator>
    where TEnumerator : struct, IU8Enumerator;

interface IU8Enumerator : IEnumerator<U8String>;

public interface IU8Formattable : IUtf8SpanFormattable
{
    U8String ToU8String(ReadOnlySpan<char> format, IFormatProvider? provider);
}

interface IU8Split<T> : IEnumerable<U8String>
    where T : struct
{
    T Separator { get; }
    U8String Value { get; }
}

interface IU8SliceCollection : ICollection<U8String>
{
    // TODO: Better naming?
    // It does make sense that the .Source of U8String is U8Source while
    // the .Source of U8Slices is U8String but I'm concerned that it might
    // be confusing.
    U8String Value { get; }
}

interface IU8Buffer
{
    ReadOnlySpan<byte> Value { get; }
}