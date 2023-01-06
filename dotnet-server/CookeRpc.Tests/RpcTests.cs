using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CookeRpc.AspNetCore;
using CookeRpc.AspNetCore.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace CookeRpc.Tests
{
    public class RpcTests : IDisposable
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly IHost? _host;

        public RpcTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;

            RpcModel model = new(new RpcModelOptions());
            model.AddService(typeof(TestController));

            _host = Host.CreateDefaultBuilder().ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.ConfigureServices(services => services.AddRpc());
                webBuilder.Configure(app => { app.UseRpc(model); });
                webBuilder.UseTestServer();
            }).Start();
        }

        [Fact]
        public async Task Invoke()
        {
            var client = _host.GetTestClient();
            var response = await client.PostAsJsonAsync("/rpc",
                new object[] {new {Id = "123", Service = "TestController", Proc = "Echo"}, "Hello!"});
            response.EnsureSuccessStatusCode();

            Assert.Equal("[{\"id\":\"123\"},\"Hello!\"]", await response.Content.ReadAsStringAsync());
        }
        
        [Fact]
        public async Task SerializeEnumResult()
        {
            var client = _host.GetTestClient();
            var response = await client.PostAsJsonAsync("/rpc",
                new object[] {new {Id = "123", Service = "TestController", Proc = "Ask"}});
            response.EnsureSuccessStatusCode();

            Assert.Equal("[{\"id\":\"123\"},\"No\"]", await response.Content.ReadAsStringAsync());
        }
        
        [Fact]
        public async Task InvokeAdvanced()
        {
            var client = _host.GetTestClient();
            var response = await client.PostAsync("/rpc",
                new StringContent(
                    @"[{""id"":""123"",""service"":""TestController"",""proc"":""EchoFruit""},{""$type"":""Banana""}]"));
            
            response.EnsureSuccessStatusCode();

            Assert.Equal("[{\"id\":\"123\"},{\"$type\":\"Banana\"}]", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Inspect()
        {
            // TODO improve
            var client = _host.GetTestClient();
            var metadata = await client.GetFromJsonAsync<JsonDocument>("/rpc/introspection");
            Assert.NotNull(metadata);
        }

        [RpcService]
        public class TestController
        {
            public string Echo(string message) => message;

            public TestModel Fetch() => new();

            public Fruit EchoFruit(Fruit fruit) => fruit;
            
            public YesOrNo Ask() => YesOrNo.No;
        }

        public void Dispose()
        {
            _host?.Dispose();
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

        public enum YesOrNo
        {
            Yes,
            No
        }
    }
}