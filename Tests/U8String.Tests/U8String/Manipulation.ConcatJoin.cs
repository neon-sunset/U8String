using System.Text;

using U8.InteropServices;

namespace U8.Tests.U8StringTests;

#pragma warning disable xUnit1042 // TheoryData has implementation issues preventing its use here
public partial class Manipulation {
    [Theory, MemberData(nameof(Strings))]
    public void ConcatByte_ProducesCorrectValue(byte[] source) {
        var u8str = U8Marshal.CreateUnsafe(source);
        var actualRight = u8str + Byte;
        var expectedRight = source.Append(Byte).ToArray();

        Assert.True(actualRight.Equals(expectedRight));
        Assert.True(actualRight.IsNullTerminated);

        var actualLeft = Byte + u8str;
        var expectedLeft = source.Prepend(Byte).ToArray();

        Assert.True(actualLeft.Equals(expectedLeft));
        Assert.True(actualLeft.IsNullTerminated);
    }

    [Fact]
    public void ConcatByte_ThrowsOnInvalidByte() {
        var u8str = (U8String)"Hello, World!"u8;

        Assert.Throws<ArgumentException>(() => u8str + 0x80);
        Assert.Throws<ArgumentException>(() => 0x80 + u8str);
    }

    public static IEnumerable<object[]> CharConcats() {
        foreach (var value in Strings
            .Select(s => (byte[])s[0])
            .Select(Encoding.UTF8.GetChars)) {
            yield return [value, OneByteChar];
            yield return [value, TwoByteChar];
            yield return [value, ThreeByteChar];
        }
    }

    [Theory, MemberData(nameof(CharConcats))]
    public void ConcatChar_ProducesCorrectValue(char[] source, char c) {
        var u8str = new U8String(source);
        var actualRight = u8str + c;
        var expectedRight = Encoding.UTF8.GetBytes([..source, c]);

        Assert.True(actualRight.Equals(expectedRight));
        Assert.True(actualRight.IsNullTerminated);

        var actualLeft = c + u8str;
        var expectedLeft = Encoding.UTF8.GetBytes(
            source.Prepend(c).ToArray());

        Assert.True(actualLeft.Equals(expectedLeft));
        Assert.True(actualLeft.IsNullTerminated);
    }

    [Fact]
    public void ConcatChar_ThrowsOnSurrogate() {
        var u8str = (U8String)"Hello, World!"u8;

        Assert.Throws<ArgumentException>(() => u8str + SurrogateChar);
        Assert.Throws<ArgumentException>(() => SurrogateChar + u8str);
    }

    public static IEnumerable<object[]> RuneConcats() {
        foreach (var value in Strings
            .Select(s => (byte[])s[0])
            .Select(Encoding.UTF8.GetString)) {
            yield return [value, OneByteRune];
            yield return [value, TwoByteRune];
            yield return [value, ThreeByteRune];
            yield return [value, FourByteRune];
        }
    }

    [Theory, MemberData(nameof(RuneConcats))]
    public void ConcatRune_ProducesCorrectValue(string source, Rune r) {
        static byte[] ToBytes(IEnumerable<Rune> runes) =>
            runes.SelectMany(Extensions.ToUtf8).ToArray();

        var u8str = new U8String(source);
        var actualRight = u8str + r;
        var expectedRight = ToBytes(source.EnumerateRunes().Append(r));

        Assert.True(actualRight.Equals(expectedRight));
        Assert.True(actualRight.IsNullTerminated);

        var actualLeft = r + u8str;
        var expectedLeft = ToBytes(source.EnumerateRunes().Prepend(r));

        Assert.True(actualLeft.Equals(expectedLeft));
        Assert.True(actualLeft.IsNullTerminated);
    }

    [Fact]
    public void ConcatString_ProducesCorrectValue() {
        var u8str = U8Marshal.CreateUnsafe(Mixed);
        var actual = u8str + u8str;
        var expected = Mixed.Concat(Mixed).ToArray();

        Assert.True(actual.Equals(expected));
        Assert.True(actual.IsNullTerminated);
    }

    [Fact]
    public void ConcatString_LeftEmptyReturnsRight() {
        var left = (U8String)""u8;
        var right = (U8String)"Hello, World!"u8;

        var result = left + right;

        Assert.True(result.Equals(right));
        Assert.True(result.SourceEqual(right));
    }

    [Fact]
    public void ConcatString_RightEmptyReturnsLeft() {
        var left = (U8String)"Hello, World!"u8;
        var right = (U8String)""u8;

        var result = left + right;

        Assert.True(result.Equals(left));
        Assert.True(result.SourceEqual(left));
    }

    [Theory, MemberData(nameof(Strings))]
    public void ConcatArray_ProducesCorrectValue(byte[] source) {
        var u8str = U8Marshal.CreateUnsafe(source);
        var actualRight = u8str + Mixed;
        var expectedRight = source.Concat(Mixed).ToArray();

        Assert.True(actualRight.Equals(expectedRight));
        Assert.True(actualRight.IsNullTerminated);

        var actualLeft = Mixed + u8str;
        var expectedLeft = Mixed.Concat(source).ToArray();

        Assert.True(actualLeft.Equals(expectedLeft));
        Assert.True(actualLeft.IsNullTerminated);
    }

    [Fact]
    public void ConcatArray_LeftEmptyReturnsRight() {
        var left = Array.Empty<byte>();
        var right = (U8String)"Hello, World!"u8;

        var result = left + right;

        Assert.True(result.Equals(right));
        Assert.True(result.SourceEqual(right));
    }

    [Fact]
    public void ConcatArray_RightEmptyReturnsLeft() {
        var left = (U8String)"Hello, World!"u8;
        var right = Array.Empty<byte>();

        var result = left + right;

        Assert.True(result.Equals(left));
        Assert.True(result.SourceEqual(left));
    }

    [Fact]
    public void ConcatArray_ThrowsOnInvalid() {
        var u8str = (U8String)"Hello, World!"u8;

        Assert.Throws<FormatException>(() => u8str + Invalid);
        Assert.Throws<FormatException>(() => Invalid + u8str);
    }

    [Theory, MemberData(nameof(Strings))]
    public void ConcatSpan_ProducesCorrectValue(byte[] source) {
        var u8str = U8Marshal.CreateUnsafe(source);
        var actualRight = u8str + Mixed.AsSpan();
        var expectedRight = source.Concat(Mixed).ToArray().AsSpan();

        Assert.True(actualRight.Equals(expectedRight));
        Assert.True(actualRight.IsNullTerminated);

        var actualLeft = Mixed.AsSpan() + u8str;
        var expectedLeft = Mixed.Concat(source).ToArray().AsSpan();

        Assert.True(actualLeft.Equals(expectedLeft));
        Assert.True(actualLeft.IsNullTerminated);
    }

    [Fact]
    public void ConcatSpan_LeftEmptyReturnsRight() {
        var left = Span<byte>.Empty;
        var right = (U8String)"Hello, World!"u8;

        var result = left + right;

        Assert.True(result.Equals(right));
        Assert.True(result.SourceEqual(right));
    }

    [Fact]
    public void ConcatSpan_RightEmptyReturnsLeft() {
        var left = (U8String)"Hello, World!"u8;
        var right = Span<byte>.Empty;

        var result = left + right;

        Assert.True(result.Equals(left));
        Assert.True(result.SourceEqual(left));
    }

    [Fact]
    public void ConcatSpan_ThrowsOnInvalid() {
        var u8str = (U8String)"Hello, World!"u8;

        Assert.Throws<FormatException>(() => u8str + Invalid.AsSpan());
        Assert.Throws<FormatException>(() => Invalid.AsSpan() + u8str);
    }

    // TODO: Dedup like Join
    [Theory, MemberData(nameof(Strings))]
    public void ConcatStringArray_ProducesCorrectValue(byte[] value) {
        var strings = Enumerable.Repeat(value, 10).Select(U8String.Create).ToArray();
        var expected = Enumerable.Repeat(value, 10).Flatten().ToArray();

        var actual = U8String.Concat(strings);

        Assert.Equal(expected, actual);
        Assert.True(actual.Equals(expected));
        Assert.True(actual.IsEmpty || actual.IsNullTerminated);
    }

    [Fact]
    public void ConcatStringArry_ReturnsSourceForSingleElement() {
        var value = (U8String)"Hello, World!"u8;
        var strings = new[] { value };

        var actual = U8String.Concat(strings);

        Assert.True(actual.Equals(value));
        Assert.True(actual.SourceEqual(value));
    }

    [Fact]
    public void ConcatStringArray_ThrowsOnNull() {
        Assert.Throws<ArgumentNullException>(() => U8String.Concat(null!));
    }

    [Theory, MemberData(nameof(Strings))]
    public void ConcatStringSpan_ProducesCorrectValue(byte[] value) {
        var strings = Enumerable.Repeat(value, 10).Select(U8String.Create).ToArray();
        var expected = Enumerable.Repeat(value, 10).Flatten().ToArray();

        var actual = U8String.Concat(strings.AsSpan());

        Assert.Equal(expected, actual);
        Assert.True(actual.Equals(expected));
        Assert.True(actual.IsEmpty || actual.IsNullTerminated);
    }

    [Fact]
    public void ConcatStringSpan_ReturnsSourceForSingleElement() {
        var value = (U8String)"Hello, World!"u8;
        var actual = U8String.Concat([value]);

        Assert.True(actual.Equals(value));
        Assert.True(actual.SourceEqual(value));
    }

    [Theory, MemberData(nameof(Strings))]
    public void ConcatStringEnumerable_ProducesCorrectValue(byte[] value) {
        var strings = Enumerable.Repeat(value, 10).Select(U8String.Create);
        var expected = Enumerable.Repeat(value, 10).Flatten().ToArray();

        var actual = U8String.Concat(strings);

        Assert.Equal(expected, actual);
        Assert.True(actual.Equals(expected));
        Assert.True(actual.IsEmpty || actual.IsNullTerminated);
    }

    [Fact]
    public void ConcatStringEnumerable_ReturnsSourceForSingleElement() {
        // Relies on .TryGetNonEnumeratedCount
        var value = (U8String)"Hello, World!"u8;
        var strings = (IEnumerable<U8String>)[value];

        var actual = U8String.Concat(strings);

        Assert.True(actual.Equals(value));
        Assert.True(actual.SourceEqual(value));
    }

    [Fact]
    public void ConcatStringEnumerable_ThrowsOnNull() {
        Assert.Throws<ArgumentNullException>(() => U8String.Concat(null!));
    }

    [Theory, MemberData(nameof(Strings))]
    public void JoinStringsOnByte_ProducesCorrectValue(byte[] value) {
        var pair = (IEnumerable<byte[]>)[[Byte], value];
        var expected = Enumerable
            .Repeat(pair, 9)
            .Flatten()
            .Prepend(value)
            .Flatten()
            .ToArray();

        IEnumerable<U8String> Overloads() {
            var strings = Enumerable.Repeat(value, 10).Select(U8String.Create);
            var array = strings.ToArray();

            yield return U8String.Join(Byte, array);
            yield return U8String.Join(Byte, array.AsSpan());
            yield return U8String.Join(Byte, strings);
            yield return U8String.Join(Byte, strings.ToList());
        }

        foreach (var actual in Overloads()) {
            Assert.Equal(expected, actual);
            Assert.True(actual.Equals(expected));
            Assert.True(actual.IsEmpty || actual.IsNullTerminated);
        }
    }

    [Fact]
    public void JoinStringsOnByte_ReturnsSourceForSingleElement() {
        var value = (U8String)"Hello, World!"u8;

        var overloads = (U8String[])
        [
            U8String.Join(Byte, [value]),
            U8String.Join(Byte, (U8String[])[value]),
            U8String.Join(Byte, (List<U8String>)[value]),
            U8String.Join(Byte, (IEnumerable<U8String>)[value])
        ];

        foreach (var actual in overloads) {
            Assert.True(actual.Equals(value));
            Assert.True(actual.SourceEqual(value));
        }
    }

    [Fact]
    public void JoinStringArrayOnByte_ThrowsOnNull() {
        Assert.Throws<ArgumentNullException>(() => U8String.Join(Byte, null!));
    }

    [Theory, MemberData(nameof(Strings))]
    public void JoinStringsOnChar_ProducesCorrectValue(byte[] value) {
        var utf16 = Encoding.UTF8.GetString(value);

        foreach (var c in new[] { OneByteChar, TwoByteChar, ThreeByteChar }) {
            var expected = Encoding.UTF8.GetBytes(
                string.Join(c, Enumerable.Repeat(utf16, 10)));

            IEnumerable<U8String> Overloads() {
                var strings = Enumerable.Repeat(utf16, 10).Select(U8String.Create);
                var array = strings.ToArray();

                yield return U8String.Join(c, array);
                yield return U8String.Join(c, array.AsSpan());
                yield return U8String.Join(c, strings);
                yield return U8String.Join(c, strings.ToList());
            }

            foreach (var actual in Overloads()) {
                Assert.Equal(expected, actual);
                Assert.True(actual.Equals(expected));
                Assert.True(actual.IsEmpty || actual.IsNullTerminated);
            }
        }
    }

    [Fact]
    public void JoinStringsOnChar_ReturnsSourceForSingleElement() {
        var value = (U8String)"Hello, World!"u8;

        var overloads = (U8String[])
        [
            U8String.Join(TwoByteChar, [value]),
            U8String.Join(TwoByteChar, (U8String[])[value]),
            U8String.Join(TwoByteChar, (List<U8String>)[value]),
            U8String.Join(TwoByteChar, (IEnumerable<U8String>)[value])
        ];

        foreach (var actual in overloads) {
            Assert.True(actual.Equals(value));
            Assert.True(actual.SourceEqual(value));
        }
    }

    [Fact]
    public void JoinStringArrayOnChar_ThrowsOnNull() {
        Assert.Throws<ArgumentNullException>(() => U8String.Join(TwoByteChar, null!));
    }

    [Theory, MemberData(nameof(Strings))]
    public void JoinStringsOnRune_ProducesCorrectValue(byte[] value) {
        var utf16 = Encoding.UTF8.GetString(value);

        foreach (var r in new[] { OneByteRune, TwoByteRune, ThreeByteRune, FourByteRune }) {
            var expected = Encoding.UTF8.GetBytes(
                string.Join(r.ToString(), Enumerable.Repeat(utf16, 10)));

            IEnumerable<U8String> Overloads() {
                var strings = Enumerable.Repeat(utf16, 10).Select(U8String.Create);
                var array = strings.ToArray();

                yield return U8String.Join(r, array);
                yield return U8String.Join(r, array.AsSpan());
                yield return U8String.Join(r, strings);
                yield return U8String.Join(r, strings.ToList());
            }

            foreach (var actual in Overloads()) {
                Assert.Equal(expected, actual);
                Assert.True(actual.Equals(expected));
                Assert.True(actual.IsEmpty || actual.IsNullTerminated);
            }
        }
    }

    [Fact]
    public void JoinStringsOnRune_ReturnsSourceForSingleElement() {
        var value = (U8String)"Hello, World!"u8;

        var overloads = (U8String[])
        [
            U8String.Join(ThreeByteRune, [value]),
            U8String.Join(ThreeByteRune, (U8String[])[value]),
            U8String.Join(ThreeByteRune, (List<U8String>)[value]),
            U8String.Join(ThreeByteRune, (IEnumerable<U8String>)[value])
        ];

        foreach (var actual in overloads) {
            Assert.True(actual.Equals(value));
            Assert.True(actual.SourceEqual(value));
        }
    }

    [Fact]
    public void JoinStringArrayOnRune_ThrowsOnNull() {
        Assert.Throws<ArgumentNullException>(() => U8String.Join(ThreeByteRune, null!));
    }
}
