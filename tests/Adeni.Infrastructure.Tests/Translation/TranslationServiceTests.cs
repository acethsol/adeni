namespace Adeni.Infrastructure.Tests.Translation;

using System.Net;
using System.Text;
using Adeni.Application.Caching;
using Adeni.Application.Translation;
using Adeni.Infrastructure.Caching;
using Adeni.Infrastructure.Translation;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

public sealed class TranslationServiceTests
{
    [Fact]
    public async Task Translate_uses_redis_cache_for_repeated_text()
    {
        var handler = new StubHandler(_ => AzureResponse("Coupe classique"));
        await using var provider = BuildProvider(handler);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationService>();

        var first = await service.TranslateAsync(["Classic haircut"], "en", "fr");
        var second = await service.TranslateAsync(["Classic haircut"], "en", "fr");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("Coupe classique", first.Value!.Translations["Classic haircut"]);
        Assert.Equal("Coupe classique", second.Value!.Translations["Classic haircut"]);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Translate_returns_identity_when_source_matches_target()
    {
        await using var provider = BuildProvider(new StubHandler(_ => throw new InvalidOperationException()));
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationService>();

        var result = await service.TranslateAsync(["Hello"], "en", "en");

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello", result.Value!.Translations["Hello"]);
    }

    private static ServiceProvider BuildProvider(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddSingleton<ICacheService, DistributedCacheService>();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddOptions<TranslationOptions>().Configure(options =>
        {
            options.Key = "test-key";
            options.Region = "canadacentral";
        });
        services.AddHttpClient("azure-translator")
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddHttpClient("dev-translator")
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddSingleton<ITranslationService, TranslationService>();
        return services.BuildServiceProvider();
    }

    private static string AzureResponse(string translated) =>
        $$"""[{"translations":[{"text":"{{translated}}","to":"fr"}]}]""";

    private sealed class StubHandler(Func<HttpRequestMessage, string> responder) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            var body = responder(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Adeni.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
