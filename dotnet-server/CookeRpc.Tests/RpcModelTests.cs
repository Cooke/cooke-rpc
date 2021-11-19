using System.Linq;
using System.Threading.Tasks;
using CookeRpc.AspNetCore;
using CookeRpc.AspNetCore.Model;
using CookeRpc.AspNetCore.Model.Types;
using Xunit;

namespace CookeRpc.Tests
{
    public class RpcModelTests
    {
        private readonly RpcModel _model;

        public RpcModelTests()
        {
            _model = new(new RpcModelOptions());
            _model.AddService(typeof(TestController));
        }

        [Fact]
        public void Shall_Model_Nullable_Return_Value()
        {
            var serviceModel = _model.Services.First();
            var proc = serviceModel.Procedures.Single(x => x.Name == "GetStringOrNull");
            AssertNullable(proc.ReturnType);
        }

        [Fact]
        public void Shall_Model_NonNullable_Return_Value()
        {
            var serviceModel = _model.Services.First();
            var proc = serviceModel.Procedures.Single(x => x.Name == "GetString");
            Assert.Equal("string", Assert.IsType<RpcRefType>(proc.ReturnType).Name);
        }

        [Fact]
        public void Shall_Model_Nullable_Return_Value_In_Task()
        {
            var serviceModel = _model.Services.First();
            var proc = serviceModel.Procedures.Single(x => x.Name == "GetStringOrNullTask");
            AssertNullable(proc.ReturnType);
        }

        [Fact]
        public void Shall_Model_NonNullable_Return_Value_In_Task()
        {
            var serviceModel = _model.Services.First();
            var proc = serviceModel.Procedures.Single(x => x.Name == "GetStringTask");
            Assert.Equal("string", Assert.IsType<RpcRefType>(proc.ReturnType).Name);
        }

        [Fact]
        public void Shall_Model_Nullable_Parameter()
        {
            var serviceModel = _model.Services.First();
            var nullableParameter = serviceModel.Procedures.Single(x => x.Name == "DoStringOrNull").Parameters.First();
            AssertNullable(nullableParameter.Type);
        }
        
        [Fact]
        public void Shall_Model_Tuples()
        {
            var serviceModel = _model.Services.First();
            var returnType = serviceModel.Procedures.Single(x => x.Name == "GetPos").ReturnType;
            var genericType = Assert.IsType<RpcGenericType>(returnType);
            Assert.Equal(genericType.InnerType, PrimitiveTypes.Tuple);
            Assert.IsType<RpcRefType>(genericType.TypeArguments.First());
        }

        private static void AssertNullable(RpcType type)
        {
            Assert.Contains(Assert.IsType<RpcUnionType>(type).Types, t => t.Name == "null");
        }

        [RpcService]
        public class TestController
        {
            public string? GetStringOrNull() => null;

            public string GetString() => "";

            public Task<string?> GetStringOrNullTask() => Task.FromResult<string?>(null);

            public Task<string> GetStringTask() => Task.FromResult("");

            public RpcResult GetResult() => new SuccessResult();

            public (int x, string y) GetPos() => (0, "0");

            public void DoStringOrNull(string? str)
            {
            }
        }

        [RpcType(Name = "Result")]
        public abstract class RpcResult
        {
        }

        public class SuccessResult : RpcResult
        {
        }

        public class FailResult : RpcResult
        {
        }
    }
}