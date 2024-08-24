using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

namespace U8;

static class VectorExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint GetMatchCount<T>(this Vector512<T> mask)
        where T : INumberBase<T>
    {
        if (Vector512.IsHardwareAccelerated)
        {
            return (uint)BitOperations.PopCount(mask.ExtractMostSignificantBits());
        }

        if (Vector256.IsHardwareAccelerated)
        {
            var (lower, upper) = mask;
            var lowerCount = GetMatchCount(lower);
            var upperCount = GetMatchCount(upper);

            return upperCount + lowerCount;
        }

        var (vec0, vec1, vec2, vec3) = mask;

        var cnt0 = GetMatchCount(vec0);
        var cnt1 = GetMatchCount(vec1);
        var cnt2 = GetMatchCount(vec2);
        var cnt3 = GetMatchCount(vec3);

        return cnt0 + cnt1 + cnt2 + cnt3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint GetMatchCount<T>(this Vector256<T> mask)
        where T : INumberBase<T>
    {
        if (Vector256.IsHardwareAccelerated)
        {
            return (uint)BitOperations.PopCount(mask.ExtractMostSignificantBits());
        }

        var (lower, upper) = mask;
        var lowerCount = GetMatchCount(lower);
        var upperCount = GetMatchCount(upper);

        return upperCount + lowerCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint GetMatchCount<T>(this Vector128<T> mask)
        where T : INumberBase<T>
    {
        if (AdvSimd.Arm64.IsSupported)
        {
            return uint.CreateTruncating(Vector128.Sum(mask & Vector128<T>.One));
        }

        return (uint)BitOperations.PopCount(mask.ExtractMostSignificantBits());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint GetMatchCount<T>(this Vector64<T> mask)
    {
        return AdvSimd.Arm64
            .AddAcross(AdvSimd.PopCount(mask.AsByte()))
            .ToScalar() / (8 * (uint)Unsafe.SizeOf<T>());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint IndexOf<T>(this Vector512<T> value, Vector512<T> mask)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var eqmask = Vector512.Equals(value, mask);
            return (uint)BitOperations.TrailingZeroCount(eqmask.ExtractMostSignificantBits());
        }

        if (Vector256.IsHardwareAccelerated)
        {
            var comparand = mask.GetLower();
            var (lower, upper) = value;

            var eqlo = lower.Eq(comparand);
            if (eqlo == Vector256<T>.Zero)
            {
                return IndexOfMatch(upper.Eq(comparand)) + (uint)Vector256<T>.Count;
            }

            return IndexOfMatch(eqlo);
        }

        var cmp128 = mask.GetLower().GetLower();

        var (vec0, vec1, vec2, vec3) = value;

        var eq0 = vec0.Eq(cmp128);
        if (eq0 == Vector128<T>.Zero)
        {
            var eq1 = vec1.Eq(cmp128);
            if (eq1 == Vector128<T>.Zero)
            {
                var eq2 = vec2.Eq(cmp128);
                if (eq2 == Vector128<T>.Zero)
                {
                    return IndexOfMatch(vec3.Eq(cmp128)) + ((uint)Vector128<T>.Count * 3);
                }

                return IndexOfMatch(eq2) + ((uint)Vector128<T>.Count * 2);
            }

            return IndexOfMatch(eq1) + (uint)Vector128<T>.Count;
        }

        return IndexOfMatch(eq0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint IndexOf<T>(this Vector256<T> value, Vector256<T> mask)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            var eqmask = Vector256.Equals(value, mask);
            return (uint)BitOperations.TrailingZeroCount(eqmask.ExtractMostSignificantBits());
        }

        var comparand = mask.GetLower();
        var (lower, upper) = value;

        var eqlo = lower.Eq(comparand);
        if (eqlo == Vector128<T>.Zero)
        {
            return IndexOfMatch(upper.Eq(comparand)) + (uint)Vector128<T>.Count;
        }

        return IndexOfMatch(eqlo);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint IndexOf<T>(this Vector128<T> value, Vector128<T> mask)
    {
        if (AdvSimd.Arm64.IsSupported)
        {
            var res = AdvSimd
                .ShiftRightLogicalNarrowingLower(value.Eq(mask).AsUInt16(), 4)
                .AsUInt64()
                .ToScalar();

            return (uint)BitOperations.TrailingZeroCount(res) / (4 * (uint)Unsafe.SizeOf<T>());
        }

        return (uint)BitOperations.TrailingZeroCount(value.Eq(mask).ExtractMostSignificantBits());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint IndexOfMatch<T>(this Vector256<T> eqmask)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            return (uint)BitOperations.TrailingZeroCount(eqmask.ExtractMostSignificantBits());
        }

        var (lower, upper) = eqmask;
        var lowerIndex = IndexOfMatch(lower);
        if (lowerIndex < (nuint)Vector128<T>.Count)
        {
            return lowerIndex;
        }

        return IndexOfMatch(upper) + (uint)Vector128<T>.Count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint IndexOfMatch<T>(this Vector128<T> eqmask)
    {
        if (AdvSimd.Arm64.IsSupported)
        {
            var res = AdvSimd
                .ShiftRightLogicalNarrowingLower(eqmask.AsUInt16(), 4)
                .AsUInt64()
                .ToScalar();

            return (uint)BitOperations.TrailingZeroCount(res) / (4 * (uint)Unsafe.SizeOf<T>());
        }

        return (uint)BitOperations.TrailingZeroCount(eqmask.ExtractMostSignificantBits());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Deconstruct<T>(
        this Vector512<T> vector, out Vector256<T> lo, out Vector256<T> hi)
    {
        lo = vector.GetLower();
        hi = vector.GetUpper();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Deconstruct<T>(
        this Vector512<T> vector,
        out Vector128<T> vec0,
        out Vector128<T> vec1,
        out Vector128<T> vec2,
        out Vector128<T> vec3)
    {
        vec0 = vector.GetLower().GetLower();
        vec1 = vector.GetLower().GetUpper();
        vec2 = vector.GetUpper().GetLower();
        vec3 = vector.GetUpper().GetUpper();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Deconstruct<T>(
        this Vector256<T> vector, out Vector128<T> lo, out Vector128<T> hi)
    {
        lo = vector.GetLower();
        hi = vector.GetUpper();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector512<T> Eq<T>(this Vector512<T> left, Vector512<T> right)
    {
        return Vector512.Equals(left, right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector256<T> Eq<T>(this Vector256<T> left, Vector256<T> right)
    {
        return Vector256.Equals(left, right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<T> Eq<T>(this Vector128<T> left, Vector128<T> right)
    {
        return Vector128.Equals(left, right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector64<T> Eq<T>(this Vector64<T> left, Vector64<T> right)
    {
        return Vector64.Equals(left, right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector256<T> Gt<T>(this Vector256<T> left, Vector256<T> right)
    {
        return Vector256.GreaterThan(left, right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<T> Gt<T>(this Vector128<T> left, Vector128<T> right)
    {
        return Vector128.GreaterThan(left, right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector256<T> Gte<T>(this Vector256<T> left, Vector256<T> right)
    {
        return Vector256.GreaterThanOrEqual(left, right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<T> Gte<T>(this Vector128<T> left, Vector128<T> right)
    {
        return Vector128.GreaterThanOrEqual(left, right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector256<T> Lt<T>(this Vector256<T> left, Vector256<T> right)
    {
        return Vector256.LessThan(left, right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<T> Lt<T>(this Vector128<T> left, Vector128<T> right)
    {
        return Vector128.LessThan(left, right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector256<T> Lte<T>(this Vector256<T> left, Vector256<T> right)
    {
        return Vector256.LessThanOrEqual(left, right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<T> Lte<T>(this Vector128<T> left, Vector128<T> right)
    {
        return Vector128.LessThanOrEqual(left, right);
    }
}
