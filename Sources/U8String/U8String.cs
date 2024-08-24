using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Unicode;

using U8.InteropServices;
using U8.Primitives;
using U8.Serialization;
using U8.Shared;

namespace U8;

/// <summary>
/// UTF-8 encoded string.
/// </summary>
/// <remarks>
/// <para>U8String is an immutable value type that represents a UTF-8 encoded string.</para>
/// <para>It stores UTF-8 code units in the underlying buffer, and provides methods
/// for manipulating and accessing the string content. It can be created from or converted
/// to a string or a span of bytes, as long as the data is valid and convertible to UTF-8.</para>
/// <para>U8String slicing methods are non-copying and return a new U8String that
/// references a portion of the original data. Methods which manipulate the
/// instances of U8String ensure that the resulting U8String is well-formed and valid UTF-8,
/// unless specified otherwise. If an operation would produce invalid UTF-8, an exception is thrown.</para>
/// <para>By default, U8String is indexed by the underlying UTF-8 bytes but offers alternate Rune and Char projections.</para>
/// </remarks>
[DebuggerTypeProxy(typeof(DebugView))]
[DebuggerDisplay("{DebuggerDisplay()}")]
[JsonConverter(typeof(U8StringJsonConverter))]
[CollectionBuilder(typeof(U8String), nameof(Create))]
[NativeMarshalling(typeof(U8Marshalling))]
public readonly partial struct U8String:
    IAdditionOperators<U8String, byte, U8String>,
    IAdditionOperators<U8String, char, U8String>,
    IAdditionOperators<U8String, Rune, U8String>,
    IAdditionOperators<U8String, U8String, U8String>,
    IComparable<U8String>,
    IComparable<U8String?>,
    ICloneable,
    IEqualityOperators<U8String, byte[], bool>,
    IEqualityOperators<U8String, ImmutableArray<byte>, bool>,
    IEqualityOperators<U8String, U8String, bool>,
    IEquatable<U8String>,
    IEquatable<U8String?>,
    IEquatable<ImmutableArray<byte>>,
    IEquatable<byte[]?>,
    IList<byte>,
    IReadOnlyList<byte>,
    ISpanParsable<U8String>,
    ISpanFormattable,
    IUtf8SpanParsable<U8String>,
    IUtf8SpanFormattable {
    /// <summary>
    /// Represents an empty <see cref="U8String"/>.
    /// </summary>
    /// <remarks>
    /// Functionally equivalent to <see langword="default(U8String)"/>.
    /// </remarks>
    public static U8String Empty {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default;
    }

    /// <summary>
    /// The maximum safe length of source bytes that can be used
    /// to create a new <see cref="U8String"/>.
    /// </summary>
    /// <remarks>
    /// This value is one less than maximum possible length of a <see cref="U8String"/>.
    /// <para/>
    /// Attempting to create a <see cref="U8String"/> with a source length greater than this
    /// may result in <see cref="OutOfMemoryException"/>, <see cref="ArgumentException"/>, or other exceptions.
    /// </remarks>
    public static int MaxSafeLength {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Array.MaxLength - 1;
    }

    internal readonly byte[]? _value;
    internal readonly U8Range _inner;

    internal int Offset {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inner.Offset;
    }

    internal int End {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inner.Offset + _inner.Length;
    }

    /// <summary>
    /// The number of UTF-8 code units (bytes) in the current <see cref="U8String"/>.
    /// </summary>
    public int Length {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inner.Length;
    }

    /// <summary>
    /// Indicates whether the current <see cref="U8String"/> is empty.
    /// </summary>
    [MemberNotNullWhen(false, nameof(_value))]
    public bool IsEmpty {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            Debug.Assert(_value is null ? Length is 0 : Length > 0);
            return _value is null;
        }
    }

    /// <summary>
    /// Indicates whether the current <see cref="U8String"/> is either explicitly or implicitly null-terminated.
    /// </summary>
    /// <remarks>
    /// Implicit null-termination is when the null-terminator is stored at the next byte past the <see cref="Length"/>
    /// of the current <see cref="U8String"/>.
    /// <para/>
    /// Passing such instances to native APIs that expect C-style strings is safe as long as the
    /// <see cref="U8String"/> is pinned and not modified throughout the duration of the call.
    /// <para/>
    /// See <see cref="NullTerminate()"/> for more information.
    /// </remarks>
    public bool IsNullTerminated {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            var (value, offset, length) = this;

            if (value != null) {
                var end = offset + length;
                // Explicitly split AsRef and Add to skip Debug assert.
                // This is intended since ptr is potentially out of bounds.
                ref var ptr = ref value.AsRef().Add(end);

                return ((uint)end < (uint)value.Length && ptr is 0) || ptr.Substract(1) is 0;
            }

            return false;
        }
    }

    public U8Range Range {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inner;
    }

    /// <summary>
    /// The number of UTF-8 code points in the current <see cref="U8String"/>.
    /// </summary>
    public int RuneCount {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            var (source, offset, length) = this;
            if (source != null) {
                return (int)(uint)U8Searching.CountRunes(ref source.AsRef(offset), (uint)length);
            }

            return 0;
        }
    }

    public U8Source Source {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_value);
    }

    /// <inheritdoc/>
    int ICollection<byte>.Count => Length;

    /// <inheritdoc/>
    bool ICollection<byte>.IsReadOnly => true;

    /// <inheritdoc/>
    int IReadOnlyCollection<byte>.Count => Length;

    /// <summary>
    /// Similar to <see cref="UnsafeRef"/>, but does not throw NRE if <see cref="IsEmpty"/> is true.
    /// </summary>
    internal ref byte DangerousRef {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            Debug.Assert(_value is null || (uint)(Offset + Length) <= (uint)_value.Length);

            ref var reference = ref Unsafe.NullRef<byte>();
            var (value, offset) = (_value, (nint)(uint)Offset);
            if (value != null) {
                reference = ref Unsafe.Add(
                    ref MemoryMarshal.GetArrayDataReference(value), offset);
            }

            return ref reference;
        }
    }

    /// <summary>
    /// Will throw NRE if <see cref="IsEmpty"/> is true.
    /// </summary>
    internal ref byte UnsafeRef {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            Debug.Assert(_value is null || (uint)(Offset + Length) <= (uint)_value.Length);

            return ref Unsafe.Add(
                ref MemoryMarshal.GetArrayDataReference(_value!), (nint)(uint)Offset);
        }
    }

    /// <summary>
    /// Will throw NRE if <see cref="IsEmpty"/> is true.
    /// </summary>
    internal ReadOnlySpan<byte> UnsafeSpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            Debug.Assert(_value is null || (uint)(Offset + Length) <= (uint)_value.Length);

            return MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_value!),
                (nint)(uint)Offset), Length);
        }
    }

    /// <summary>
    /// Will throw NRE if <see cref="IsEmpty"/> is true.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref byte UnsafeRefAdd(int index) {
        Debug.Assert(_value is null || (uint)(Offset + Length) <= (uint)_value.Length);

        return ref Unsafe.Add(
            ref MemoryMarshal.GetArrayDataReference(_value!), (nint)(uint)Offset + (nint)(uint)index);
    }

    /// <summary>
    /// Evaluates if the current <see cref="U8String"/> contains only ASCII characters.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAscii() {
        var deref = this;
        return deref.IsEmpty || Ascii.IsValid(deref.UnsafeSpan);
    }

    /// <summary>
    /// Evaluates if the current <see cref="U8String"/> is normalized to the specified
    /// Unicode normalization form (default: <see cref="NormalizationForm.FormC"/>).
    /// </summary>
    internal bool IsNormalized(NormalizationForm form = NormalizationForm.FormC) {
        var source = this;
        if (!source.IsEmpty) {
            var value = source.UnsafeSpan;

            // Drain always normalized ASCII bytes
            var nonAsciiOffset = (int)Polyfills.Text.Ascii.GetIndexOfFirstNonAsciiByte(value);
            value = value.SliceUnsafe(nonAsciiOffset);

            if (value.Length > 0) {
                throw new NotImplementedException();
            }
        }

        return true;
    }

    /// <summary>
    /// Validates that the <paramref name="value"/> is a valid UTF-8 byte sequence.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> to validate.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValid(ReadOnlySpan<byte> value) {
        return value.Length <= 0 || Utf8.IsValid(value);
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Validate(ReadOnlySpan<byte> value) {
        if (!IsValid(value)) {
            ThrowHelpers.InvalidUtf8();
        }
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly byte GetPinnableReference() => ref DangerousRef;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Deconstruct(out byte[]? value, out int offset, out int length) {
        value = _value;
        offset = Offset;
        length = Length;
    }

    void IList<byte>.Insert(int index, byte item) => throw new NotSupportedException();
    void IList<byte>.RemoveAt(int index) => throw new NotSupportedException();
    void ICollection<byte>.Add(byte item) => throw new NotSupportedException();
    void ICollection<byte>.Clear() => throw new NotSupportedException();
    bool ICollection<byte>.Remove(byte item) => throw new NotSupportedException();
}
