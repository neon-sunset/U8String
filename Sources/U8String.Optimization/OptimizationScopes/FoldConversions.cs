using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace U8.Optimization.OptimizationScopes;

// TODO:
// U8Builder:
// - Append
// - AppendLine
// - AppendLiteral
// InterpolatedU8StringHandler: (is this even possible?)
// - AppendLiteral
// - AppendFormatted

sealed class FoldConversions : IOptimizationScope
{
    sealed record Utf8LiteralExpression(string Value)
    {
        public bool Equals(Utf8LiteralExpression? obj)
        {
            return obj != null && Value.Equals(obj.Value, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }
    }

    static readonly UTF8Encoding UTF8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    static readonly string[] ByteLookup = Enumerable
        .Range(0, 256)
        .Select(i => i.ToString(CultureInfo.InvariantCulture))
        .ToArray();

    readonly List<string> _literalValues = [];
    readonly Dictionary<object, Interceptor> _literalMap = new(new LiteralComparer());

    public string Name => "Literals";

    public IEnumerable<string> Imports =>
    [
        "System", "System.Runtime.CompilerServices",
        "U8", "U8.InteropServices"
    ];

    public IEnumerable<string> Fields => _literalValues;

    public IEnumerable<Interceptor> Interceptors => _literalMap.Values;

    static bool IsSupportedMethod(IMethodSymbol method)
    {
        var mehodName = method.Name;
        var containingType = method.ContainingType.Name;

        return (containingType, mehodName) switch
        {
            ("U8String", "Create") => true,
            ("U8String", "CreateLossy") => true,
            ("U8String", "FromAscii") => true,
            ("U8String", "FromLiteral") => true,

            ("Syntax", "u8") => true,

            _ => false
        };
    }

    public bool ProcessCallsite(
        SemanticModel model,
        IMethodSymbol method,
        InvocationExpressionSyntax invocation)
    {
        if (!IsSupportedMethod(method) ||
            invocation.ArgumentList.Arguments is not [var argument])
        {
            return false;
        }

        var expression = argument.Expression;
        var constant = model.GetConstantValue(expression);
        if (constant is not { HasValue: true, Value: object constantValue })
        {
            if (expression.IsKind(SyntaxKind.Utf8StringLiteralExpression)
                && model.GetOperation(expression) is IUtf8StringOperation operation
                && operation.Value is not null or [])
            {
                constantValue = new Utf8LiteralExpression(operation.Value);
            }
            else return false;
        }

        var callsite = new Callsite(method, invocation);
        if (_literalMap.TryGetValue(constantValue, out var interceptor))
        {
            // Already known literal - append a callsite and return
            interceptor.Callsites.Add(callsite);
            return true;
        }

        if (!TryGetString(constantValue, out var utf16) ||
            !TryGetBytes(utf16, out var utf8))
        {
            return false;
        }

        var literalName = AddByteLiteral(utf8, utf16[^1] != 0);

        _literalMap[constantValue] = new(
            Method: method,
            InstanceArg: null,
            CustomAttrs: Constants.AggressiveInlining,
            Callsites: [callsite],
            Body: $"return U8Marshal.CreateUnsafe(_{literalName}, 0, {utf8.Length});");

        return true;
    }

    static bool TryGetString(object? value, [NotNullWhen(true)] out string? result)
    {
        var invariantCulture = CultureInfo.InvariantCulture;
        var utf16 = value switch
        {
            byte u8 => u8.ToString(invariantCulture),
            sbyte i8 => i8.ToString(invariantCulture),
            ushort u16 => u16.ToString(invariantCulture),
            short i16 => i16.ToString(invariantCulture),
            uint u32 => u32.ToString(invariantCulture),
            int i32 => i32.ToString(invariantCulture),
            ulong u64 => u64.ToString(invariantCulture),
            long i64 => i64.ToString(invariantCulture),

            decimal d128 => d128.ToString(invariantCulture),

            Enum e => e.ToString(),
            char c => c.ToString(invariantCulture),
            string s => s,
            Utf8LiteralExpression u8 => u8.Value,
            _ => null
        };

        if (utf16 is not (null or []))
        {
            result = utf16;
            return true;
        }

        result = null;
        return false;
    }

    static bool TryGetBytes(string utf16, [NotNullWhen(true)] out byte[]? result)
    {
        try
        {
            result = UTF8.GetBytes(utf16);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    string AddByteLiteral(byte[] utf8, bool nullTerminate)
    {
        var literalName = Guid.NewGuid().ToString("N");
        var byteLiteral = new StringBuilder((utf8.Length * 4) + 32)
            .Append("byte[] _")
            .Append(literalName)
            .Append(" = [");

        byteLiteral.Append(ByteLookup[utf8[0]]);
        foreach (var b in utf8.AsSpan(1))
        {
            byteLiteral.Append(',');
            byteLiteral.Append(ByteLookup[b]);
        }
        if (nullTerminate) byteLiteral.Append(",0");
        byteLiteral.Append("]");

        _literalValues.Add(byteLiteral.ToString());
        return literalName;
    }

    sealed class LiteralComparer : IEqualityComparer<object>
    {
        public new bool Equals(object x, object y)
        {
            return x is string s
                ? y is string sy && s.Equals(sy, StringComparison.Ordinal)
                : x.Equals(y);
        }

        public int GetHashCode(object obj)
        {
            return obj is string s ? StringComparer.Ordinal.GetHashCode(s) : obj.GetHashCode();
        }
    }
}
