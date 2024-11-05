using System;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using CookeRpc.AspNetCore;
using CookeRpc.AspNetCore.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace CookeRpc.Tests
{
    public class RpcAuthorizationTests : IDisposable
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly IHost? _host;

        public RpcAuthorizationTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;

            RpcModelBuilder model = new(new RpcModelBuilderOptions());
            model.AddService(typeof(TestController));

            _host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddAuthorization(o =>
                            o.AddPolicy("Fail", p => p.AddRequirements(new FailRequirement()))
                        );
                        services.AddRpc();
                    });
                    webBuilder.Configure(app =>
                    {
                        app.Use(
                            (context, next) =>
                            {
                                if (context.Request.Headers.ContainsKey("authorization"))
                                {
                                    context.User = new ClaimsPrincipal(
                                        new ClaimsIdentity(
                                            new[] { new Claim(ClaimTypes.NameIdentifier, "123") },
                                            "auth-header"
                                        )
                                    );
                                }

                                return next();
                            }
                        );
                        app.UseRpc(model.Build());
                    });
                    webBuilder.UseTestServer();
                })
                .Start();
        }

        [Fact]
        public async Task Invoke_Secure_Without_Identity_Shall_Fail()
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
                        Proc = "Secure"
                    }
                }
            );
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            Assert.Equal(
                "[{\"id\":\"123\",\"errorCode\":\"authentication_required\",\"errorMessage\":\"Authentication required\"}]",
                await response.Content.ReadAsStringAsync()
            );
        }

        [Fact]
        public async Task Invoke_Authorize_Fail_Shall_Fail_With_Custom_Message()
        {
            var client = _host.GetTestClient();
            client.DefaultRequestHeaders.Add("authorization", "true");
            var response = await client.PostAsJsonAsync(
                "/rpc",
                new object[]
                {
                    new
                    {
                        Id = "123",
                        Service = "TestController",
                        Proc = "AuthorizeFail"
                    }
                }
            );
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            Assert.Equal(
                "[{\"id\":\"123\",\"errorCode\":\"authorization_error\",\"errorMessage\":\"Failed Message\"}]",
                await response.Content.ReadAsStringAsync()
            );
        }

        [Fact]
        public async Task Invoke_Secure_With_Identity_Shall_Pass()
        {
            var client = _host.GetTestClient();
            client.DefaultRequestHeaders.Add("authorization", "true");
            var response = await client.PostAsJsonAsync(
                "/rpc",
                new object[]
                {
                    new
                    {
                        Id = "123",
                        Service = "TestController",
                        Proc = "Secure"
                    }
                }
            );
            response.EnsureSuccessStatusCode();

            Assert.Equal("[{\"id\":\"123\"}]", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Invoke_Anonymous_Without_Identity_Shall_Pass()
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
                        Proc = "Anonymous"
                    }
                }
            );
            response.EnsureSuccessStatusCode();

            Assert.Equal("[{\"id\":\"123\"}]", await response.Content.ReadAsStringAsync());
        }

        [RpcService]
        [Authorize]
        public class TestController
        {
            public void Secure() { }

            [Authorize("Fail")]
            public void AuthorizeFail() { }

            [AllowAnonymous]
            public void Anonymous() { }
        }

        public void Dispose()
        {
            _host?.Dispose();
        }
    }

    public class FailRequirement : AuthorizationHandler<FailRequirement>, IAuthorizationRequirement
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            FailRequirement requirement
        )
        {
            context.Fail(new AuthorizationFailureReason(this, "Failed Message"));
            return Task.CompletedTask;
        }
    }
}
