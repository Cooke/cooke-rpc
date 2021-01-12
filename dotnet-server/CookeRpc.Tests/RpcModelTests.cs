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
            Assert.Equal("string", Assert.IsType<NativeType>(proc.ReturnType).Name);
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
            Assert.Equal("string", Assert.IsType<NativeType>(proc.ReturnType).Name);
        }

        private static void AssertNullable(RpcType type)
        {
            Assert.Contains(Assert.IsType<UnionType>(type).Types, t => t.Name == "null");
        }

        [RpcService]
        public class TestController
        {
            public string? GetStringOrNull() => null;

            public string GetString() => "";

            public Task<string?> GetStringOrNullTask() => Task.FromResult<string?>(null);

            public Task<string> GetStringTask() => Task.FromResult("");
        }
    }
}