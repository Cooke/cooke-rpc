using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using CookeRpc.AspNetCore;
using CookeRpc.AspNetCore.Model;
using CookeRpc.AspNetCore.Model.TypeDefinitions;
using CookeRpc.AspNetCore.Model.Types;
using Xunit;

namespace CookeRpc.Tests
{
    public class RpcModelBuilderTests
    {
        private readonly RpcServiceModel _serviceModel;

        public RpcModelBuilderTests()
        {
            var builder = new RpcModelBuilder(new RpcModelBuilderOptions());
            builder.AddService(typeof(TestController));
            var model = builder.Build();
            _serviceModel = model.Services.First();
        }

        [Fact]
        public void Shall_Model_Nullable_Return_Value()
        {
            var proc = _serviceModel.Procedures.Single(x => x.Name == "GetStringOrNull");
            AssertNullable(proc.ReturnType);
        }

        [Fact]
        public void Shall_Model_NonNullable_Return_Value()
        {
            var proc = _serviceModel.Procedures.Single(x => x.Name == "GetString");
            Assert.Equal("string", Assert.IsType<PrimitiveRpcType>(proc.ReturnType).Name);
        }

        [Fact]
        public void Shall_Model_Nullable_Return_Value_In_Task()
        {
            var proc = _serviceModel.Procedures.Single(x => x.Name == "GetStringOrNullTask");
            AssertNullable(proc.ReturnType);
        }

        [Fact]
        public void Shall_Model_NonNullable_Return_Value_In_Task()
        {
            var proc = _serviceModel.Procedures.Single(x => x.Name == "GetStringTask");
            Assert.Equal("string", Assert.IsType<PrimitiveRpcType>(proc.ReturnType).Name);
        }

        [Fact]
        public void Shall_Model_Nullable_Parameter()
        {
            var nullableParameter = _serviceModel.Procedures.Single(x => x.Name == "DoStringOrNull").Parameters.First();
            AssertNullable(nullableParameter.Type);
        }

        [Fact]
        public void Shall_Model_Tuples()
        {
            var returnType = _serviceModel.Procedures.Single(x => x.Name == "GetPos").ReturnType;
            var genericType = Assert.IsType<GenericRpcType>(returnType);
            Assert.Equal(genericType.TypeDefinition, PrimitiveTypes.Tuple);
            Assert.IsType<PrimitiveRpcType>(genericType.TypeArguments.First());
        }

        [Fact]
        public void Class_Shall_Support_RpcTypeAttribute()
        {
            var proc = _serviceModel.Procedures.Single(x => x.Name == "SetEmailAddress");
            Assert.Equal(new PrimitiveRpcType("EmailAddress", typeof(EmailAddress)), proc.Parameters.First().Type);
        }

        private static void AssertNullable(IRpcType type)
        {
            Assert.Contains(Assert.IsType<UnionRpcType>(type).Types, t => t is PrimitiveRpcType {Name: "null"});
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

            public void SetEmailAddress(EmailAddress email)
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

        [RpcType(Kind = RpcTypeKind.Primitive)]
        public record EmailAddress(String Value);
    }
}