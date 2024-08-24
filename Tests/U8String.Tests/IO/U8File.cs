using System.Collections.Immutable;

using U8.IO;

namespace U8.Tests.IO;

public class U8FileTests
{
    public static readonly IEnumerable<object[]> ValidStrings = Constants.ValidStrings.Select(s => new[] { s });

    [Theory, MemberData(nameof(ValidStrings))]
    public async Task ReadFromFileHandle_ProducesCorrectResult(ReferenceText text)
    {
        var expected = text.Utf8;
        var path = await CreateTempFile(expected);

        try
        {

            using var handle = File.OpenHandle(path);

            var actual = (U8String[])
            [
                U8File.Read(handle),
                U8File.Read(handle, offset: 0, length: expected.Length),
                U8File.Read(handle, offset: 0, stripBOM: true),
                U8File.Read(handle, offset: 0, roundTrailingRune: true),
                U8File.Read(handle, offset: 0, stripBOM: true, roundTrailingRune: true),

                ..await Task.WhenAll(
                    U8File.ReadAsync(handle),
                    U8File.ReadAsync(handle, offset: 0, length: expected.Length),
                    U8File.ReadAsync(handle, offset: 0, stripBOM: true),
                    U8File.ReadAsync(handle, offset: 0, roundTrailingRune: true),
                    U8File.ReadAsync(handle, offset: 0, stripBOM: true, roundTrailingRune: true))
            ];

            foreach (var str in actual)
            {
                Assert.Equal(expected, str);
                Assert.Equal(0, str.Offset);
                Assert.Equal(expected.Length, str.Length);
                Assert.Equal(!str.IsEmpty, str.IsNullTerminated);
                Assert.True(str.Equals(expected));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadFromFilePath_ThrowsOnNullPath()
    {
        Assert.Throws<ArgumentNullException>(() => U8File.Read((string)null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => U8File.ReadAsync((string)null!));
    }

    [Fact]
    public async Task ReadFromFileHandle_ThrowsOnNegativeOffset()
    {
        var path = await CreateTempFile([42, 42, 42, 42]);
        try
        {
            using var handle = File.OpenHandle(path);

            Assert.Throws<ArgumentException>(() => U8File.Read(handle, offset: -1));
            await Assert.ThrowsAsync<ArgumentException>(() => U8File.ReadAsync(handle, offset: -1));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadFromHandle_ThrowsOnOffsetExceedingLength()
    {
        var path = await CreateTempFile([42, 42, 42, 42]);
        try
        {
            using var handle = File.OpenHandle(path);

            Assert.Throws<ArgumentException>(() => U8File.Read(handle, offset: 5));
            await Assert.ThrowsAsync<ArgumentException>(() => U8File.ReadAsync(handle, offset: 5));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadFromHandle_ThrowsOnInvalidUtf8()
    {
        var path = await CreateTempFile([0xFF, 0xFF, 0xFF, 0xFF]);
        try
        {
            using var handle = File.OpenHandle(path);

            Assert.Throws<FormatException>(() => U8File.Read(handle));
            await Assert.ThrowsAsync<FormatException>(() => U8File.ReadAsync(handle));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadFromHandleAsync_ThrowsOnCancelledToken()
    {
        var path = await CreateTempFile([42, 42, 42, 42]);
        try
        {
            using var handle = File.OpenHandle(path);

            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => U8File.ReadAsync(handle, ct: cts.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => U8File.ReadAsync(handle, offset: 0, length: 4, ct: cts.Token));
        }
        finally
        {
            File.Delete(path);
        }
    }

    static async Task<string> CreateTempFile(ImmutableArray<byte> content)
    {
        var path = Path.GetTempFileName();
        using var writeHandle = File.OpenHandle(path, access: FileAccess.Write);

        await RandomAccess.WriteAsync(writeHandle, content.AsMemory(), 0);

        return path;
    }
}
