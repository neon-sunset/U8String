using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using U8.IO;

namespace U8;

public static partial class U8Console {
    readonly unsafe struct PosixStdout: U8WriteExtensions.IWriteable {
        [SupportedOSPlatformGuard("linux")]
        [SupportedOSPlatformGuard("macos")]
        [SupportedOSPlatformGuard("freebsd")]
        public static bool IsSupported {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => OperatingSystem.IsLinux()
                || OperatingSystem.IsMacOS()
                || OperatingSystem.IsFreeBSD();
        }

        [UnsupportedOSPlatform("windows")]
        public void Write(bytes value) {
            lock (Console.Out) {
                if (value is []) return;
                fixed (byte* pin = value) {
                    var ptr = pin;
                    var length = (nint)(uint)value.Length;
                    do {
                        var result = Write(1, ptr, (nuint)length);
                        if (result < 0) IOException();
                        ptr += result;
                        length -= result;
                    } while (length > 0);
                }
            }
        }

        [UnsupportedOSPlatform("windows")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDispose(ref InlineU8Builder builder) {
            var bytes = builder.Written;
            Write(bytes);
            builder.Dispose();
        }

#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute'. Why: does not require marshalling.
        [DllImport("libc", EntryPoint = "write")]
        static extern nint Write(int fd, byte* buf, nuint count);
#pragma warning restore SYSLIB1054

        ValueTask U8WriteExtensions.IWriteable.WriteAsync(
            ReadOnlyMemory<byte> value, CancellationToken ct) => throw new NotSupportedException();
        ValueTask U8WriteExtensions.IWriteable.WriteDisposeAsync(
            PooledU8Builder builder, CancellationToken ct) => throw new NotSupportedException();
    }

    [UnsupportedOSPlatform("linux")]
    [UnsupportedOSPlatform("macos")]
    [UnsupportedOSPlatform("freebsd")]
    readonly struct FallbackStdout {
        public static readonly Stream Value = Console.OpenStandardOutput();
    }

    static bytes NewLine {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => OperatingSystem.IsWindows() ? "\r\n"u8 : "\n"u8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(U8String value) {
        if (PosixStdout.IsSupported) {
            new PosixStdout().Write(value);
        }
        else FallbackStdout.Value.Write(value);
    }

    public static void Write(bytes value) {
        U8String.Validate(value);
        if (PosixStdout.IsSupported) {
            new PosixStdout().Write(value);
        }
        else FallbackStdout.Value.Write(value);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Write(ref InlineU8Builder handler) {
        if (PosixStdout.IsSupported) {
            U8WriteExtensions.WriteBuilder(new PosixStdout(), ref handler);
        }
        else FallbackStdout.Value.Write(handler.Written);
    }

    public static void Write<T>(T value)
    where T : IUtf8SpanFormattable {
        if (PosixStdout.IsSupported) {
            U8WriteExtensions.WriteUtf8Formattable(new PosixStdout(), value);
        }
        else FallbackStdout.Value.Write(value);
    }

    public static void WriteLine() {
        if (PosixStdout.IsSupported) {
            new PosixStdout().Write(NewLine);
        }
        else FallbackStdout.Value.Write(NewLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteLine(U8String value) {
        if (PosixStdout.IsSupported) {
            U8WriteExtensions.WriteLineSpan(new PosixStdout(), value);
        }
        else FallbackStdout.Value.WriteLine(value);
    }

    public static void WriteLine(bytes value) {
        if (PosixStdout.IsSupported) {
            U8String.Validate(value);
            U8WriteExtensions.WriteLineSpan(new PosixStdout(), value);
        }
        else FallbackStdout.Value.WriteLine(value);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void WriteLine(ref InlineU8Builder handler) {
        if (PosixStdout.IsSupported) {
            U8WriteExtensions.WriteLineBuilder(new PosixStdout(), ref handler);
        }
        else FallbackStdout.Value.WriteLine(handler.Written);
    }

    public static void WriteLine<T>(T value)
    where T : IUtf8SpanFormattable {
        if (PosixStdout.IsSupported) {
            U8WriteExtensions.WriteLineUtf8Formattable(new PosixStdout(), value);
        }
        else FallbackStdout.Value.WriteLine(value);
    }

    [DoesNotReturn, StackTraceHidden]
    static void IOException() {
        // TODO: EH UX
        throw new IOException();
    }
}
