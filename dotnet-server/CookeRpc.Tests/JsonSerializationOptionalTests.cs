using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CookeRpc.AspNetCore.Core;
using CookeRpc.AspNetCore.JsonSerialization;
using CookeRpc.AspNetCore.Utils;
using Xunit;
using Xunit.Abstractions;

namespace CookeRpc.Tests;

public class JsonSerializationOptionalTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly JsonSerializerOptions? _options;

    public JsonSerializationOptionalTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        _options = new JsonSerializerOptions
        {
            Converters = { new OptionalRpcJsonConverterFactory(), },
            PropertyNameCaseInsensitive = true
        };
    }

    [Fact]
    public void DeserializeEmptyOptional()
    {
        const string json = "{ }";
        var obj = JsonSerializer.Deserialize<ModelWithOptional>(json, _options)!;
        Assert.False(obj.OptionalString.HasValue);
        Assert.False(obj.OptionalNullString.HasValue);
    }

    [Fact]
    public void DeserializeWithSetOptional()
    {
        const string json = "{ \"optionalNullString\": null, \"optionalString\": \"Hello\" }";
        var obj = JsonSerializer.Deserialize<ModelWithOptional>(json, _options)!;
        Assert.True(obj.OptionalString.HasValue);
        Assert.True(obj.OptionalNullString.HasValue);
        Assert.Null(obj.OptionalNullString.Value);
        Assert.Equal("Hello", obj.OptionalString.Value);
    }

    private class ModelWithOptional
    {
        public Optional<string?> OptionalNullString { get; set; }

        public Optional<string> OptionalString { get; set; }
    }

    [Fact]
    public void DeserializeWithoutTypeInfo()
    {
        const string json = "{\"Radius\":3}";
        var obj = JsonSerializer.Deserialize<Apple>(json, _options);

        var apple = Assert.IsType<Apple>(obj);
        Assert.Equal(3, Assert.IsType<Apple>(apple).Radius);
    }

    public interface IFruit { }

    public class Apple : IFruit
    {
        public int Radius { get; set; } = 3;
    }

    public record ModelWithConstructor(string Value);
}
