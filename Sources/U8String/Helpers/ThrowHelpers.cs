using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using U8.Prototypes;

namespace U8;

// TODO: Author exception messages
static class ThrowHelpers {
    [DoesNotReturn, StackTraceHidden]
    internal static void InvalidUtf8() {
        // TODO: Better exception message?
        throw new FormatException("The value is not a valid UTF-8 sequence.");
    }

    [DoesNotReturn, StackTraceHidden]
    internal static void InvalidAscii() {
        throw new FormatException("The value is not a valid ASCII sequence.");
    }

    [DoesNotReturn, StackTraceHidden]
    internal static void InvalidSplit() {
        throw new FormatException("Invalid UTF-8: found a continuation byte or invalid UTF-8 sequence.");
    }

    [DoesNotReturn, StackTraceHidden]
    internal static void IndexOutOfRange() {
        throw new IndexOutOfRangeException();
    }

    [DoesNotReturn, StackTraceHidden]
    internal static void ArgumentException() {
        throw new ArgumentException();
    }

    [DoesNotReturn, StackTraceHidden]
    internal static void ArgumentException(string message) {
        throw new ArgumentException(message);
    }

    [DoesNotReturn, StackTraceHidden]
    internal static void ArgumentOutOfRange() {
        throw new ArgumentOutOfRangeException();
    }

    [DoesNotReturn, StackTraceHidden]
    internal static void ArgumentOutOfRange(string? paramName) {
        throw new ArgumentOutOfRangeException(paramName);
    }

    [DoesNotReturn, StackTraceHidden]
    internal static T ArgumentOutOfRange<T>(string? paramName = null) {
        throw new ArgumentOutOfRangeException(paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CheckNull([NotNull] object? value) {
        // TODO: There is argument name mismatch - it's always "value".
        // Examine the codegen impact of using built-in ArgumentNullException.ThrowIfNull
        // or, alternatively, see if minimal/no impact can be achieved here manually.
        if (value is null) {
            NullValue();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CheckAscii(byte value) {
        if (value > 0x7F) {
            NonAsciiByte();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CheckSurrogate(char value) {
        if (char.IsSurrogate(value)) {
            SurrogateChar();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CheckPattern<T>(T pattern) {
        switch (pattern) {
            case byte b: CheckAscii(b); break;
            case char c: CheckSurrogate(c); break;
            case not (Rune or U8String or Pattern or Splitter):
                Unsupported(); break;
        }

        [DoesNotReturn, StackTraceHidden]
        static void Unsupported() {
            throw new NotSupportedException($"{typeof(T)} is not a supported pattern type.");
        }
    }

    [DoesNotReturn, StackTraceHidden]
    internal static void DestinationTooShort() {
        throw new ArgumentException("The destination buffer is too short.");
    }

    [DoesNotReturn, StackTraceHidden]
    internal static void NullValue() {
        throw new ArgumentNullException("value");
    }

    [DoesNotReturn, StackTraceHidden]
    internal static void NonAsciiByte() {
        throw new ArgumentException("The argument is not a valid ASCII byte.");
    }

    [DoesNotReturn, StackTraceHidden]
    internal static void SurrogateChar() {
        throw new ArgumentException(
            "The argument is a surrogate character. Separate surrogate UTF-16 code units are not representable in UTF-8.");
    }

    [DoesNotReturn, StackTraceHidden]
    internal static T SequenceIsEmpty<T>() {
        throw new InvalidOperationException("The sequence is empty.");
    }

    [DoesNotReturn, StackTraceHidden]
    internal static void NonConstantString() {
        throw new ArgumentException("The argument is not a constant string.", "value");
    }

    [DoesNotReturn, StackTraceHidden]
    internal static void Unreachable(
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0) {
        throw new InvalidOperationException($"Unreachable code reached at {path}:{line}.");
    }

    [DoesNotReturn, StackTraceHidden]
    internal static T Unreachable<T>(
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0) {
        throw new InvalidOperationException($"Unreachable code reached at {path}:{line}.");
    }

    [DoesNotReturn, StackTraceHidden]
    internal static void NotSupportedBigEndian() {
        throw new NotSupportedException(
            "This operation is currently not supported on big-endian systems. " +
            "Please file an issue at https://github.com/U8String/U8String/issues/new");
    }
}
