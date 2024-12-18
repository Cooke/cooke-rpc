using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CookeRpc.AspNetCore;
using CookeRpc.AspNetCore.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace CookeRpc.Tests;

public class RpcTests : IDisposable
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly IHost? _host;

    public RpcTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        RpcModelBuilder modelBuilder = new(new RpcModelBuilderOptions());
        modelBuilder.AddService(typeof(TestController));

        _host = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.ConfigureServices(services => services.AddRpc());
                webBuilder.Configure(app =>
                {
                    app.UseRpc(modelBuilder.Build());
                });
                webBuilder.UseTestServer();
            })
            .Start();
    }

    [Fact]
    public async Task Invoke_Shall_Work()
    {
        var client = _host.GetTestClient();
        var response = await client.PostAsJsonAsync(
            "/rpc",
            new object[]
            {
                new
                {
                    Id = "123",
                    Service = "TestController",
                    Proc = "Echo"
                },
                "Hello!"
            }
        );
        response.EnsureSuccessStatusCode();

        Assert.Equal("[{\"id\":\"123\"},\"Hello!\"]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invoke_Task_Shall_Work()
    {
        var client = _host.GetTestClient();
        var response = await client.PostAsJsonAsync(
            "/rpc",
            new object[]
            {
                new
                {
                    Id = "123",
                    Service = "TestController",
                    Proc = "StringInTask"
                }
            }
        );
        response.EnsureSuccessStatusCode();

        Assert.Equal("[{\"id\":\"123\"},\"hello\"]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invoke_ValueTask_Shall_Work()
    {
        var client = _host.GetTestClient();
        var response = await client.PostAsJsonAsync(
            "/rpc",
            new object[]
            {
                new
                {
                    Id = "123",
                    Service = "TestController",
                    Proc = "StringInValueTask"
                }
            }
        );
        response.EnsureSuccessStatusCode();

        Assert.Equal("[{\"id\":\"123\"},\"hello\"]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SerializeEnumResult()
    {
        var client = _host.GetTestClient();
        var response = await client.PostAsJsonAsync(
            "/rpc",
            new object[]
            {
                new
                {
                    Id = "123",
                    Service = "TestController",
                    Proc = "Ask"
                }
            }
        );
        response.EnsureSuccessStatusCode();

        Assert.Equal("[{\"id\":\"123\"},\"No\"]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invoke_PolymorphicValues()
    {
        var client = _host.GetTestClient();
        var response = await client.PostAsync(
            "/rpc",
            new StringContent(
                @"[{""id"":""123"",""service"":""TestController"",""proc"":""EchoFruit""},{""$type"":""Banana""}]"
            )
        );

        response.EnsureSuccessStatusCode();

        Assert.Equal(
            "[{\"id\":\"123\"},{\"$type\":\"Banana\"}]",
            await response.Content.ReadAsStringAsync()
        );
    }

    [Fact]
    public async Task Invoke_Numbers()
    {
        var client = _host.GetTestClient();
        var response = await client.PostAsync(
            "/rpc",
            new StringContent(
                @"[{""id"":""123"",""service"":""TestController"",""proc"":""EchoNumber""},1]"
            )
        );

        response.EnsureSuccessStatusCode();

        Assert.Equal("[{\"id\":\"123\"},1]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Inspect()
    {
        // TODO improve
        var client = _host.GetTestClient();
        var metadata = await client.GetFromJsonAsync<JsonDocument>("/rpc/introspection");
        Assert.NotNull(metadata);
        _testOutputHelper.WriteLine(metadata!.RootElement.ToString());
    }

    [Fact]
    public async Task Invoke_With_Invalid_Type_Shall_Give_Bad_Request()
    {
        var client = _host.GetTestClient();
        var response = await Invoke(client, "TestController", "SetEmail", "invalid_email");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.Equal(
            "[{\"id\":\"123\",\"errorCode\":\"bad_request\",\"errorMessage\":\"Invalid value for parameter \\u0027email\\u0027: Invalid email\"}]",
            await response.Content.ReadAsStringAsync()
        );
    }

    [Fact]
    public async Task Invoke_Unknown_Service()
    {
        var client = _host.GetTestClient();
        var response = await Invoke(client, "BadController", "SetEmail", "invalid_email");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.Equal(
            "[{\"id\":\"123\",\"errorCode\":\"procedure_not_found\",\"errorMessage\":\"No service with the give name\"}]",
            await response.Content.ReadAsStringAsync()
        );
    }

    private static async Task<HttpResponseMessage> Invoke(
        HttpClient client,
        string service,
        string proc,
        params object[] args
    )
    {
        var response = await client.PostAsJsonAsync(
            "/rpc",
            new object[]
            {
                new
                {
                    Id = "123",
                    Service = service,
                    Proc = proc
                },
            }.Concat(args)
        );
        return response;
    }

    [RpcService]
    public class TestController
    {
        public string Echo(string message) => message;

        public TestModel Fetch() => new();

        public Task<Fruit?> EchoFruit(Fruit fruit) => Task.FromResult((Fruit?)fruit);

        public Task<int> EchoNumber(int num) => Task.FromResult(num);

        public YesOrNo Ask() => YesOrNo.No;

        public void SetEmail(Email email) { }

        public Task<string> StringInTask() => Task.FromResult("hello");

        public ValueTask<string> StringInValueTask() => ValueTask.FromResult("hello");
    }

    [RpcType(Kind = RpcTypeKind.Primitive)]
    [JsonConverter(typeof(EmailConverter))]
    public record Email(string Value)
    {
        public string Value { get; } =
            Value.Contains("@") ? Value : throw new ArgumentException("Invalid email");
    }

    public class EmailConverter : JsonConverter<RpcTests.Email>
    {
        public override RpcTests.Email? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            return new RpcTests.Email(
                reader.GetString() ?? throw new JsonException("Email may not be null")
            );
        }

        public override void Write(
            Utf8JsonWriter writer,
            RpcTests.Email value,
            JsonSerializerOptions options
        )
        {
            writer.WriteStringValue(value.Value);
        }
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

    public interface Fruit { }

    public class Banana : Fruit { }

    public class Apple : Fruit { }

    public class RedApple : Apple { }

    public enum YesOrNo
    {
        Yes,
        No
    }
}
