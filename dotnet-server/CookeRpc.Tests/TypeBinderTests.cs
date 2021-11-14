using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CookeRpc.AspNetCore.Model;
using Xunit;
using Xunit.Abstractions;

namespace CookeRpc.Tests
{
    public class TypeBinderTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly RpcModel _model;

        public TypeBinderTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;

            _model = new RpcModel(new RpcModelOptions
            {
                TypeFilter = type => type.GetCustomAttribute<IgnoreAttribute>() == null
            });
            _model.MapType(typeof(Fruit));
            _model.MapType(typeof(TestModel[]));
            _model.MapType(typeof(Dictionary<string, string>));
        }

        [Fact]
        public void Resolve_Map_With_Array_To_Dictionary_With_List()
        {
            var targetType = typeof(IDictionary<string, List<double>>);
            var res = new RpcModelTypeBinder(_model).ResolveType("map<string,array<number>>", targetType);
            Assert.True(res.IsAssignableTo(targetType));

            // Must be possible to instantiate type
            Activator.CreateInstance(res);
        }

        [Fact]
        public void Resolve_Array_Of_TestModel_To_Enumerable_Of_TestModel()
        {
            var targetType = typeof(IEnumerable<TestModel>);
            var res = new RpcModelTypeBinder(_model).ResolveType("array<TestModel>", targetType);
            Assert.True(res.IsAssignableTo(targetType));

            // Must be possible to instantiate type
            Activator.CreateInstance(res);
        }

        [Fact]
        public void Resolve_Apple_To_Fruit()
        {
            var targetType = typeof(Fruit);
            var res = new RpcModelTypeBinder(_model).ResolveType("Apple", targetType);
            Assert.True(res.IsAssignableTo(targetType));

            // Must be possible to instantiate type
            Activator.CreateInstance(res);
        }

        [Fact]
        public void Do_Not_Resolve_Dangerous_To_Fruit()
        {
            var targetType = typeof(IEnumerable<object>);
            Assert.Throws<InvalidOperationException>(() =>
            {
                new RpcModelTypeBinder(_model).ResolveType("Dangerous", targetType);
            });
        }

        public class TestModel
        {
            public string Name { get; set; } = "";

            public int Integer { get; set; } = 1337;
        }

        public interface Fruit
        {
        }

        public class Banana : Fruit
        {
        }

        public class Apple : Fruit
        {
        }

        public class RedApple : Apple
        {
        }

        [Ignore]
        public class PoisonApple : Apple
        {
        }

        public class IgnoreAttribute : Attribute
        {
        }

        public class Dangerous : IEnumerable<object>
        {
            public IEnumerator<object> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}