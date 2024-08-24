using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace U8.InteropServices;

// TODO: Consider switching to stateful structs
// TODO: Double-check that the flow of get pinnable ref -> ConvertToUnmanaged is correct
[EditorBrowsable(EditorBrowsableState.Advanced)]
[CustomMarshaller(typeof(U8String), MarshalMode.Default, typeof(Raw))]
public static unsafe class U8Marshalling
{
    [CustomMarshaller(typeof(U8String), MarshalMode.ManagedToUnmanagedIn, typeof(LikelyNullTerminated))]
    public static class LikelyNullTerminated
    {
        // TODO: see if there's a way to create this on stack on occasion
        // the callee writes to the address, messing up the null byte
        static readonly byte Empty;

        public static byte* ConvertToUnmanaged(U8String managed)
        {
            throw new NotSupportedException($"""
                It is expected for the generated marshalling code to call {nameof(GetPinnableReference)} instead.");
                If you are seeing this exception, please file an issue at https://github.com/U8String/U8String/issues/new
                """);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly byte GetPinnableReference(U8String managed)
        {
            if (managed.IsNullTerminated)
            {
                return ref managed.UnsafeRef;
            }
            else if (managed.IsEmpty)
            {
                return ref Empty;
            }

            return ref NullTerminate(ref managed.UnsafeRef, managed.Length);

            static ref byte NullTerminate(ref byte src, int length)
            {
                ref var dst = ref new byte[(nint)(uint)(length + 1)].AsRef();

                MemoryMarshal
                    .CreateReadOnlySpan(ref src, length)
                    .CopyToUnsafe(ref dst);

                return ref dst;
            }
        }
    }

    [CustomMarshaller(typeof(U8String), MarshalMode.ManagedToUnmanagedIn, typeof(UnlikelyNullTerminated))]
    public ref struct UnlikelyNullTerminated
    {
        static readonly byte Empty;

        ref byte _ptr;
        bool _allocated;

        public static int BufferSize => 256;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromManaged(U8String managed, Span<byte> buffer)
        {
            if (buffer.Length < BufferSize)
            {
                ThrowHelpers.DestinationTooShort();
            }

            _allocated = false;

            if (managed.IsNullTerminated)
            {
                _ptr = ref managed.UnsafeRef;
            }
            else if (managed.IsEmpty)
            {
                _ptr = ref Unsafe.AsRef(in Empty);
            }
            else
            {
                NullTerminateCore(
                    ref managed.UnsafeRef,
                    ref buffer.AsRef(),
                    managed.Length);
            }
        }

        void NullTerminateCore(ref byte src, [UnscopedRef] ref byte dst, int length)
        {
            if (length > BufferSize)
            {
                dst = ref Unsafe.AsRef<byte>(NativeMemory.Alloc((uint)length + 1));
                _allocated = true;
            }

            MemoryMarshal.CreateSpan(ref src, length).CopyToUnsafe(ref dst);
            dst.Add(length) = 0;

            _ptr = ref dst;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ref readonly byte GetPinnableReference() => ref _ptr;

        // SAFETY: _ptr is always pinned and returned pointer is expected not to
        // outlive the duration of the p/invoke call it is passed to.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly byte* ToUnmanaged() => (byte*)Unsafe.AsPointer(ref _ptr);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Free()
        {
            if (_allocated)
            {
                // SAFETY: _allocated _ptr originates from NativeMemory.Alloc
                NativeMemory.Free(Unsafe.AsPointer(ref _ptr));
            }
        }
    }

    [CustomMarshaller(typeof(U8String), MarshalMode.UnmanagedToManagedIn, typeof(Validate))]
    public static class Validate
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static U8String ConvertToManaged(byte* unmanaged)
        {
            return new(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(unmanaged));
        }
    }

    [CustomMarshaller(typeof(U8String), MarshalMode.Default, typeof(Raw))]
    public static class Raw
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static U8String ConvertToManaged(byte* unmanaged)
        {
            return new(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(unmanaged), skipValidation: true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte* ConvertToUnmanaged(U8String managed)
        {
            throw new NotSupportedException($"""
                It is expected for the generated marshalling code to call {nameof(GetPinnableReference)} instead.
                If you are seeing this exception, please file an issue at https://github.com/U8String/U8String/issues/new
                """);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly byte GetPinnableReference(U8String managed)
        {
            return ref managed.DangerousRef;
        }
    }
}