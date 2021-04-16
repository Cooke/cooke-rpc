using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CookeRpc.AspNetCore.Core;
using CookeRpc.AspNetCore.JsonSerialization;
using CookeRpc.AspNetCore.Utils;
using Xunit;
using Xunit.Abstractions;

namespace CookeRpc.Tests
{
    public class NestedTypeJsonSerializationTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly JsonSerializerOptions? _options;

        public NestedTypeJsonSerializationTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;

            _options = new JsonSerializerOptions
            {
                Converters =
                {
                    new OptionalRpcJsonConverterFactory(),
                    new NestedTypedObjectConverterFactory(new TestTypeBinder()),
                }
            };
        }

        [Fact]
        public void SerializeWithNestedTypeInfoTest()
        {
            var json = JsonSerializer.Serialize<object>(
                new FruitBasket10Size
                {
                    Fruits = new IFruit[] {new Apple(), new Banana()},
                    Decoration = new RosetteDecoration
                    {
                        Length = 15, Color = "Pink", DecorationFruit = new Apple {Radius = 33}
                    },
                }, _options);

            _testOutputHelper.WriteLine(json);
            Assert.Equal(
                "{\"Fruits\":[{\"$Apple\":{\"Radius\":3}},{\"$Banana\":{\"Angle\":30}}],\"Size\":10,\"Decoration\":{\"$RosetteDecoration\":{\"Length\":15,\"Color\":\"Pink\",\"DecorationFruit\":{\"$Apple\":{\"Radius\":33}}}}}",
                json);
        }

        [Fact]
        public void DeserializeWithNestedTypeInfo()
        {
            const string json =
                "{\"$FruitBasket10Size\":{\"Fruits\":[{\"$Apple\":{\"Radius\":3}},{\"$Banana\":{\"Angle\":30}}],\"Size\":10,\"Decoration\":{\"$RosetteDecoration\":{\"Length\":15,\"Color\":\"Pink\",\"DecorationFruit\":{\"$Apple\":{\"Radius\":33}}}}}}";
            var obj = JsonSerializer.Deserialize<object>(json, _options);

            var basket = Assert.IsType<FruitBasket10Size>(obj);
            Assert.Collection(basket.Fruits, x => Assert.Equal(3, Assert.IsType<Apple>(x).Radius),
                x => Assert.Equal(30, Assert.IsType<Banana>(x).Angle));

            var decoration = Assert.IsType<RosetteDecoration>(basket.Decoration);
            Assert.Equal(15, decoration.Length);
            Assert.Equal("Pink", decoration.Color);
            var decorationFruit = Assert.IsType<Apple>(decoration.DecorationFruit);
            Assert.Equal(33, decorationFruit.Radius);
        }

        [Fact]
        public void DeserializeWithConstructor()
        {
            const string json = "{ \"$ModelWithConstructor\": { \"value\": \"Hello\", \"extra\": 123 } }";
            var obj = JsonSerializer.Deserialize<object>(json, _options);
            var model = Assert.IsType<ModelWithConstructor>(obj);
            Assert.Equal("Hello", model.Value);
        }

        [Fact]
        public void DeserializeWithoutTypeInfo()
        {
            const string json = "{\"Radius\":3}";
            var obj = JsonSerializer.Deserialize<Apple>(json, _options);

            var apple = Assert.IsType<Apple>(obj);
            Assert.Equal(3, Assert.IsType<Apple>(apple).Radius);
        }

        public interface IFruit
        {
        }

        public class Banana : IFruit
        {
            public int Angle { get; set; } = 30;
        }

        public class Apple : IFruit
        {
            public int Radius { get; set; } = 3;
        }

        public interface IFruitBasket
        {
            IEnumerable<IFruit> Fruits { get; }

            public int Size { get; }

            public Decoration Decoration { get; set; }
        }

        public class FruitBasket10Size : IFruitBasket
        {
            public IEnumerable<IFruit> Fruits { get; init; } = Array.Empty<IFruit>();

            public int Size => 10;

            public Decoration Decoration { get; set; } = new RosetteDecoration();
        }

        public abstract class Decoration
        {
            public string Color { get; set; } = "Gray";

            public IFruit DecorationFruit { get; set; } = new Banana {Angle = 333};
        }

        public class RosetteDecoration : Decoration
        {
            public int Length { get; set; }
        }

        public record ModelWithConstructor(string Value);

        public class TestTypeBinder : ITypeBinder
        {
            public string GetName(Type type) => type.Name;

            public Type ResolveType(string typeName, Type targetType) =>
                typeof(NestedTypeJsonSerializationTests).GetNestedType(typeName) ??
                throw new Exception($"Cannot resolve type with name '{typeName}'");
            
            public bool ShouldResolveType(Type targetType)
            {
                return targetType == typeof(object) || targetType.IsInterface || targetType.IsAbstract;
            }
        }
    }
}