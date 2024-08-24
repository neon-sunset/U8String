using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using U8.InteropServices;

var text = u8("Hello from C#!");
Interop.Print(text, text.Length);
Interop.PrintNullTerminated(text);

if (Interop.CountRunes(text, text.Length) != text.RuneCount)
    U8Console.WriteLine(u8("Rune count mismatch!"));

var fromRust = Interop.GetString();
U8Console.WriteLine(fromRust);

static unsafe partial class Interop {
    [SuppressGCTransition]
    [LibraryImport("rust", EntryPoint = "print")]
    public static partial void Print(U8String text, nint length);

    [SuppressGCTransition]
    [LibraryImport("rust", EntryPoint = "print_null_terminated")]
    public static partial void PrintNullTerminated(
        [MarshalUsing(typeof(U8Marshalling.LikelyNullTerminated))] U8String text);

    [SuppressGCTransition]
    [LibraryImport("rust", EntryPoint = "count_runes")]
    public static partial nint CountRunes(U8String text, nint length);

    [SuppressGCTransition]
    [LibraryImport("rust", EntryPoint = "get_str")]
    public static partial U8String GetString();
}
