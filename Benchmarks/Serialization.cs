using System.Text.Json;
using System.Text.Json.Serialization;

using BenchmarkDotNet.Attributes;

using U8.Serialization;

namespace U8.Benchmarks;

[ShortRunJob]
[MemoryDiagnoser]
// [DisassemblyDiagnoser(maxDepth: 3)]
public class Serialization {
    public record Person {
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Title { get; init; }
        public required string ID { get; init; }
        public required Person? Another { get; init; }
    }

    public record PersonU8 {
        public required u8str FirstName { get; init; }
        public required u8str LastName { get; init; }
        public required u8str Title { get; init; }
        public required u8str ID { get; init; }
        public required PersonU8? Another { get; init; }
    }

    static readonly Person PersonValue = new() {
        FirstName = "John",
        LastName = "Doe",
        Title = "Software Engineer",
        ID = "123456789",
        Another = new() {
            FirstName = "John",
            LastName = "Doe",
            Title = "Software Engineer",
            ID = "123456789",
            Another = null
        }
    };

    static readonly PersonU8 PersonU8Value = new() {
        FirstName = u8("John"),
        LastName = u8("Doe"),
        Title = u8("Software Engineer"),
        ID = u8("123456789"),
        Another = new() {
            FirstName = u8("John"),
            LastName = u8("Doe"),
            Title = u8("Software Engineer"),
            ID = u8("123456789"),
            Another = null
        }
    };

    static readonly U8String PersonBytes = PersonValue.ToU8Json(JsonContext.Default.Person);

    [Benchmark(Baseline = true)]
    public Person? DeserializePerson() =>
        JsonSerializer.Deserialize(PersonBytes, JsonContext.Default.Person);

    [Benchmark]
    public PersonU8? DeserializePersonU8() =>
        JsonSerializer.Deserialize(PersonBytes, JsonContext.Default.PersonU8);

    [Benchmark]
    public string SerializePerson() =>
        JsonSerializer.Serialize(PersonValue, JsonContext.Default.Person);

    [Benchmark]
    public string SerializePersonU8() =>
        JsonSerializer.Serialize(PersonU8Value, JsonContext.Default.PersonU8);

    [Benchmark]
    public U8String SerializePersonToUtf8() => PersonValue.ToU8Json(JsonContext.Default.Person);

    [Benchmark]
    public U8String SerializePersonU8ToUtf8() => PersonU8Value.ToU8Json(JsonContext.Default.PersonU8);
}

[JsonSerializable(typeof(Serialization.Person))]
[JsonSerializable(typeof(Serialization.PersonU8))]
public sealed partial class JsonContext: JsonSerializerContext;