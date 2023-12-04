using System.Collections.Immutable;
using System.IO.Hashing;

using U8Primitives.Abstractions;

namespace U8Primitives;

#pragma warning disable RCS1003 // Add braces. Why: manual codegen tuning.
public readonly partial struct U8String
{
    public static int Compare(U8String x, U8String y)
    {
        int result;
        if (!x.IsEmpty)
        {
            if (!y.IsEmpty)
            {
                var left = x.UnsafeSpan;
                var right = y.UnsafeSpan;

                result = Compare(left, right);
            }
            else result = 1;
        }
        else result = y.IsEmpty ? 0 : -1;

        return result;
    }

    public static int Compare(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
    {
        return x.SequenceCompareTo(y);
    }

    public static int Compare<T>(U8String x, U8String y, T comparer)
        where T : IComparer<U8String>
    {
        return comparer.Compare(x, y);
    }

    public static int Compare<T>(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y, T comparer)
        where T : IU8Comparer
    {
        return comparer.Compare(x, y);
    }

    /// <summary>
    /// Compares two <see cref="U8String"/> instances using lexicographical semantics and returns
    /// an integer that indicates whether the first instance precedes, follows, or occurs in the same
    /// position in the sort order as the second instance.
    /// </summary>
    public int CompareTo(U8String other)
    {
        int result;
        if (!other.IsEmpty)
        {
            var deref = this;
            if (!deref.IsEmpty)
            {
                var left = deref.UnsafeSpan;
                var right = other.UnsafeSpan;

                result = Compare(left, right);
            }
            else result = -1;
        }
        else result = IsEmpty ? 0 : 1;

        return result;
    }

    public int CompareTo(U8String? other)
    {
        // Supposedly, this is for collections which opt to store 'U8String?'
        if (other.HasValue)
        {
            return CompareTo(other.Value);
        }

        return 1;
    }

    public int CompareTo(ReadOnlySpan<byte> other)
    {
        int result;
        var deref = this;
        if (!deref.IsEmpty)
        {
            if (!other.IsEmpty)
            {
                var left = deref.UnsafeSpan;
                result = Compare(left, other);
            }
            else result = 1;
        }
        else result = other.IsEmpty ? 0 : -1;

        return result;
    }

    public int CompareTo<T>(U8String other, T comparer)
        where T : IComparer<U8String>
    {
        return comparer.Compare(this, other);
    }

    public int CompareTo<T>(ReadOnlySpan<byte> other, T comparer)
        where T : IU8Comparer
    {
        return comparer.Compare(this, other);
    }

    /// <summary>
    /// Indicates whether the current <see cref="U8String"/> instance is equal to another
    /// object of <see cref="U8String"/> or <see cref="byte"/> array.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj switch
        {
            U8String other => Equals(other),
            ImmutableArray<byte> other => Equals(other),
            byte[] other => Equals(other),
            _ => false,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(U8String? other)
    {
        if (other.HasValue)
        {
            return Equals(other.Value);
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(U8String other)
    {
        var deref = this;
        if (deref.Length == other.Length)
        {
            if (deref.Length > 0 && (
                deref.Offset != other.Offset || !deref.SourceEquals(other)))
            {
                return deref.UnsafeSpan.SequenceEqual(
                    other.UnsafeSpan.SliceUnsafe(0, deref.Length));
            }

            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(byte[]? other)
    {
        if (other != null)
        {
            return Equals(other.AsSpan());
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ImmutableArray<byte> other)
    {
        return Equals(other.AsSpan());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ReadOnlySpan<byte> other)
    {
        var deref = this;
        if (deref.Length == other.Length)
        {
            if (deref.Length > 0)
            {
                return deref.UnsafeSpan.SequenceEqual(other);
            }

            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals<T>(U8String other, T comparer)
        where T : IEqualityComparer<U8String>
    {
        return comparer.Equals(this, other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals<T>(ReadOnlySpan<byte> other, T comparer)
        where T : IU8EqualityComparer
    {
        return comparer.Equals(this, other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SourceEquals(U8String other)
    {
        return ReferenceEquals(_value, other._value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SourceEquals(U8Source value)
    {
        return ReferenceEquals(_value, value.Value);
    }

    /// <inheritdoc cref="GetHashCode(ReadOnlySpan{byte})"/>
    public override int GetHashCode()
    {
        var hash = XxHash3.HashToUInt64(this, U8HashSeed.Value);

        return ((int)hash) ^ (int)(hash >> 32);
    }

    public int GetHashCode<T>(T comparer) where T : IEqualityComparer<U8String>
    {
        return comparer.GetHashCode(this);
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <param name="value">UTF-8 bytes to calculate the hash code for.</param>
    /// <remarks>
    /// The hash code is calculated using the xxHash3 algorithm.
    /// </remarks>
    public static int GetHashCode(ReadOnlySpan<byte> value)
    {
        var hash = XxHash3.HashToUInt64(value, U8HashSeed.Value);

        return ((int)hash) ^ (int)(hash >> 32);
    }

    public static int GetHashCode<T>(ReadOnlySpan<byte> value, T comparer)
        where T : IU8EqualityComparer
    {
        return comparer.GetHashCode(value);
    }
}
