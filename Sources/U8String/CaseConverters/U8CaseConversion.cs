using U8.Abstractions;
using U8.CaseConversion;

namespace U8;

public static class U8CaseConversion {
    public static U8AsciiCaseConverter Ascii => default;
    public static U8InvariantCaseConverter Invariant => default;
    // internal static U8FallbackInvariantCaseConverter FallbackInvariant => default;
}
