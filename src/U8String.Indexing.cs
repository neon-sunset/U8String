namespace U8Primitives;

public readonly partial struct U8String
{
    /// <summary>
    /// Gets a UTF-8 code unit represented as <see cref="byte"/> at the specified index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="index"/> is less than zero or greater than or equal to <see cref="Length"/>.
    /// </exception>
    /// <exception cref="NullReferenceException">Thrown when <see cref="Length"/> is zero.</exception>
    /// <returns>The <see cref="byte"/> at the specified index.</returns>
    /// <remarks>
    /// When iterating over the contents of <see cref="U8String"/>, consider using <see cref="AsSpan()"/> instead
    /// for best indexing performance.
    /// </remarks>
    public ref readonly byte this[int index]
    {
        // This will throw NRE on empty, there is nothing we can do about it
        // without sacrificing codegen quality.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref UnsafeSpan[index];
    }

    /// <inheritdoc cref="this[int]"/>
    byte IList<byte>.this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this[index];
        set => throw new NotImplementedException();
    }

    // TODO: Naming? Other options are ugly or long, or even more confusing.
    public bool IsCharBoundary(int index)
    {
        return (uint)index < (uint)Length && !U8Info.IsContinuationByte(this[index]);
    }

    public int NextCharIndex(int index)
    {
        var deref = this;
        if ((uint)index >= (uint)deref.Length)
        {
            return Length;
        }

        var span = deref.UnsafeSpan;
        while (index < span.Length && U8Info.IsContinuationByte(span[index]))
        {
            index++;
        }

        return index;
    }
}
