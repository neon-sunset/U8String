using BenchmarkDotNet.Attributes;

using U8.Primitives;
using U8.Prototypes;

namespace U8.Benchmarks;

[ShortRunJob]
[MemoryDiagnoser]
public class BoxedDispatch
{
    [Params(100, 1000, int.MaxValue)]
    public int Length;

    U8Split<byte> OldSplit = [];
    Split<byte> NewSplit = [];

    IEnumerable<U8String> OldSplitBoxed = [];
    IEnumerable<U8String> NewSplitBoxed = [];

    [GlobalSetup]
    public async Task Setup()
    {
        var notices = (await new HttpClient()
            .GetU8StringAsync("https://raw.githubusercontent.com/dotnet/runtime/main/THIRD-PARTY-NOTICES.TXT"))
            .SliceRounding(0, Length);

        var oldSplit = new U8Split<byte>(notices, (byte)'\n');
        OldSplit = oldSplit;
        OldSplitBoxed = oldSplit;

        var newSplit = new Split<byte>(notices, (byte)'\n');
        NewSplit = newSplit;
        NewSplitBoxed = newSplit;
    }

    [Benchmark]
    public int RegularOld()
    {
        var length = 0;
        foreach (var segment in OldSplit)
        {
            length += segment.Length;
        }
        return length;
    }

    [Benchmark]
    public int RegularNew()
    {
        var length = 0;
        foreach (var segment in NewSplit)
        {
            length += segment.Length;
        }
        return length;
    }

    [Benchmark]
    public int BoxedOld()
    {
        var length = 0;
        foreach (var segment in OldSplitBoxed)
        {
            length += segment.Length;
        }
        return length;
    }

    [Benchmark]
    public int BoxedNew()
    {
        var length = 0;
        foreach (var segment in NewSplitBoxed)
        {
            length += segment.Length;
        }
        return length;
    }
}