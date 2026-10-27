namespace Adeni.Api.Tests.Extensions;

using System.Net;
using Adeni.Api.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public sealed class ForwardedHeadersExtensionsTests
{
    [Theory]
    [InlineData("ReverseProxy:ForwardLimit", "0")]
    [InlineData("ReverseProxy:KnownProxies:0", "not-an-ip")]
    [InlineData("ReverseProxy:KnownProxies:0", "0.0.0.0")]
    [InlineData("ReverseProxy:KnownNetworks:0", "0.0.0.0/0")]
    [InlineData("ReverseProxy:KnownNetworks:0", "::/0")]
    [InlineData("ReverseProxy:KnownNetworks:0", "invalid")]
    [InlineData("ReverseProxy:KnownNetworks:0", "::ffff:0:0/96")]
    [InlineData("ASPNETCORE_FORWARDEDHEADERS_ENABLED", "true")]
    public void AddAdeniForwardedHeaders_UnsafeTrustConfiguration_Rejects(string key, string value)
    {
        var configuration = Configuration(true);
        configuration[key] = value;
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddAdeniForwardedHeaders(configuration));
    }

    [Fact]
    public void AddAdeniForwardedHeaders_NoTrustedHops_Rejects()
    {
        var configuration = Configuration(true);
        configuration["ReverseProxy:KnownProxies:0"] = null;
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddAdeniForwardedHeaders(configuration));
    }

    [Fact]
    public void AddAdeniForwardedHeaders_Defaults_DisablesForwarding()
    {
        using var provider = new ServiceCollection().AddAdeniForwardedHeaders(Configuration(false)).BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        Assert.Equal(ForwardedHeaders.None, options.ForwardedHeaders);
        Assert.Empty(options.KnownProxies);
        Assert.Empty(options.KnownIPNetworks);
    }

    [Theory]
    [InlineData(true, "10.0.0.10", true)]
    [InlineData(true, "203.0.113.10", false)]
    [InlineData(false, "10.0.0.10", false)]
    public async Task ForwardedHeaders_TrustBoundary_ControlsClientIpSchemeAndRatePartition(bool enabled, string peer, bool trusted)
    {
        await using var app = await StartApp(Configuration(enabled), peer);
        using var client = app.GetTestClient();
        for (var index = 0; index < 30; index++)
        {
            using var response = await SendAsync(client, "198.51.100.1");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(trusted ? "198.51.100.1" : peer, response.Headers.GetValues("Client-IP").Single());
            Assert.Equal(trusted ? "https" : "http", response.Headers.GetValues("Client-Scheme").Single());
        }

        using var rejected = await SendAsync(client, "198.51.100.1");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        using var otherClient = await SendAsync(client, "198.51.100.2");
        Assert.Equal(trusted ? HttpStatusCode.OK : HttpStatusCode.TooManyRequests, otherClient.StatusCode);
    }

    [Theory]
    [InlineData("10.0.0.10", "198.51.100.2", "https")]
    [InlineData("10.0.1.10", "10.0.1.10", "http")]
    public async Task ForwardedHeaders_TrustedNetworkAndHopLimit_IgnoresSpoofedLeftmostHop(string peer, string expectedIp, string scheme)
    {
        var configuration = Configuration(true, false);
        configuration["ReverseProxy:KnownNetworks:0"] = "10.0.0.0/24";
        await using var app = await StartApp(configuration, peer);
        using var client = app.GetTestClient();
        using var response = await SendAsync(client, "192.0.2.99, 198.51.100.2", "http, https");
        Assert.Equal(expectedIp, response.Headers.GetValues("Client-IP").Single());
        Assert.Equal(scheme, response.Headers.GetValues("Client-Scheme").Single());
    }

    [Theory]
    [InlineData("10.0.0.11", "198.51.100.8")]
    [InlineData("203.0.113.11", "203.0.113.11")]
    public async Task ForwardedHeaders_MultipleHops_StopsAtUntrustedIntermediary(string intermediary, string expected)
    {
        var configuration = Configuration(true, false);
        configuration["ReverseProxy:KnownNetworks:0"] = "10.0.0.0/24";
        configuration["ReverseProxy:ForwardLimit"] = "2";
        await using var app = await StartApp(configuration, "10.0.0.10");
        using var client = app.GetTestClient();
        using var response = await SendAsync(client, $"198.51.100.8, {intermediary}", "https, https");
        Assert.Equal(expected, response.Headers.GetValues("Client-IP").Single());
    }

    [Theory]
    [InlineData("10.0.0.10", HttpStatusCode.OK)]
    [InlineData("203.0.113.10", HttpStatusCode.TemporaryRedirect)]
    public async Task ForwardedHeaders_BeforeHttpsRedirection_UsesOnlyTrustedScheme(string peer, HttpStatusCode status)
    {
        await using var app = await StartApp(Configuration(true), peer, redirectHttps: true);
        using var client = app.GetTestClient();
        using var response = await SendAsync(client, "198.51.100.1");
        Assert.Equal(status, response.StatusCode);
    }

    private static IConfigurationRoot Configuration(bool enabled, bool includeProxy = true) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ReverseProxy:Enabled"] = enabled.ToString(),
        ["ReverseProxy:KnownProxies:0"] = includeProxy ? "10.0.0.10" : null,
    }.Where(entry => entry.Value is not null)).Build();

    private static async Task<WebApplication> StartApp(IConfiguration configuration, string peer, bool redirectHttps = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);
        builder.Services.AddAdeniForwardedHeaders(configuration);
        builder.Services.AddAdeniRateLimiting(new ProductionEnvironment());
        var app = builder.Build();
        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
            return next(context);
        });
        app.UseForwardedHeaders();
        app.Use((context, next) =>
        {
            context.Response.Headers["Client-IP"] = context.Connection.RemoteIpAddress!.ToString();
            context.Response.Headers["Client-Scheme"] = context.Request.Scheme;
            return next(context);
        });
        if (redirectHttps)
        {
            app.UseHttpsRedirection();
        }

        app.UseRouting();
        app.UseAdeniRateLimiting();
        app.MapGet("/probe", () => "ok").RequireRateLimiting(RateLimitingExtensions.AuthSyncPolicy);
        await app.StartAsync();
        return app;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string forwardedFor, string proto = "https")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        request.Headers.Add("X-Forwarded-Proto", proto);
        return await client.SendAsync(request);
    }

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
