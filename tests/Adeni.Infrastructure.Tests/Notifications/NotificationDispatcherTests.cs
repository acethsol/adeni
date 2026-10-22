namespace Adeni.Infrastructure.Tests.Notifications;

using System.Net;
using Adeni.Application.Markets;
using Adeni.Application.Notifications;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Notifications;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Notifications;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Subscriptions;
using Adeni.Infrastructure.Tests.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class NotificationDispatcherTests
{
    [Fact]
    public async Task Preference_gating_skips_disabled_email_channel()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Prefs", CreatedAt = DateTimeOffset.UtcNow });
        db.TenantNotificationPreferences.Add(new TenantNotificationPreferences
        {
            TenantId = tenantId,
            EmailEnabled = false,
            PushEnabled = false,
            SmsRemindersEnabled = true,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var sink = scope.ServiceProvider.GetRequiredService<CapturingEmailProvider>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

        await dispatcher.SendAsync(
            new NotificationMessage("email", "a@b.com", "Hi", "Body", tenantId, "evt-1"));

        Assert.Empty(sink.Sent);
    }

    [Fact]
    public async Task Entitlement_gating_skips_sms_when_smsEnabled_false()
    {
        await using var provider = BuildProvider(subscriptionSku: SubscriptionSku.Starter);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "SMS gate",
            SubscriptionSku = SubscriptionSku.Starter,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var smsSink = scope.ServiceProvider.GetRequiredService<CapturingSmsProvider>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

        await dispatcher.SendAsync(
            new NotificationMessage("sms", "+15551234567", "Reminder", "Body", tenantId, "evt-sms-1"));

        Assert.Empty(smsSink.Sent);
    }

    [Fact]
    public async Task Duplicate_dedupe_event_sends_once()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Dedupe", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var sink = scope.ServiceProvider.GetRequiredService<CapturingEmailProvider>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
        var message = new NotificationMessage(
            "email",
            "guest@example.com",
            "Confirmed",
            "Thanks",
            tenantId,
            "booking-confirmed:abc");

        await dispatcher.SendAsync(message);
        await dispatcher.SendAsync(message);

        Assert.Single(sink.Sent);
        Assert.Equal(1, await db.NotificationDeliveries.CountAsync());
    }

    [Fact]
    public async Task Resend_provider_posts_to_mock_http_handler()
    {
        var requests = new List<HttpRequestMessage>();
        var handler = new StubHttpHandler(requests, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };

        var provider = new ResendEmailNotificationProvider(
            httpClient,
            Options.Create(new NotificationsOptions
            {
                Resend = new ResendNotificationOptions
                {
                    ApiKey = "re_test",
                    From = "noreply@adeni.io",
                },
            }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ResendEmailNotificationProvider>.Instance);

        await provider.SendAsync(
            new EmailNotificationRequest("guest@example.com", "Hello", "<p>Hi</p>", "noreply@adeni.io", Guid.NewGuid()));

        Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.Contains("/emails", requests[0].RequestUri!.AbsoluteUri);
    }

    internal static ServiceProvider BuildProvider(
        Action<IServiceCollection>? configure = null,
        SubscriptionSku subscriptionSku = SubscriptionSku.Professional)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHost { EnvironmentName = "Testing" });
        services.AddSingleton<CapturingEmailProvider>();
        services.AddSingleton<CapturingSmsProvider>();
        services.AddSingleton<IEmailNotificationProvider>(sp => sp.GetRequiredService<CapturingEmailProvider>());
        services.AddSingleton<ISmsNotificationProvider>(sp => sp.GetRequiredService<CapturingSmsProvider>());
        services.Configure<NotificationsOptions>(o =>
        {
            o.Resend.From = "noreply@adeni.io";
            o.Twilio.From = "+15551234567";
            o.Senders =
            [
                new NotificationSenderMarketOptions { CountryCode = "NG", FromEmail = "noreply@adeni.io", SmsFrom = "+15551234567" },
            ];
        });
        services.AddSingleton<IMarketCatalog, TestMarketCatalog>();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.AddEntitlementsModule();
        services.AddScoped<INotificationPreferencesService, NotificationPreferencesService>();
        services.AddScoped<INotificationRecipientResolver, NotificationRecipientResolver>();
        services.AddScoped<INotificationSenderConfigResolver, NotificationSenderConfigResolver>();
        services.AddScoped<ChannelNotificationDispatcher>();
        services.AddScoped<IdempotentNotificationDispatcher>();
        services.AddScoped<EntitlementAwareNotificationDispatcher>();
        services.AddScoped<INotificationDispatcher, PreferenceAwareNotificationDispatcher>();
        services.AddInlineNotificationJobScheduling();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private sealed class TestHost : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestMarketCatalog : IMarketCatalog
    {
        public IReadOnlyList<MarketDefinition> List() =>
            [new("lagos", "Lagos", "NG", "NGN", "Africa/Lagos", new(0, 0), ["en"], true)];

        public IReadOnlyList<MarketDefinition> ListLive() => List();

        public MarketDefinition? GetById(string marketId) =>
            List().FirstOrDefault(m => m.Id == marketId);

        public bool IsValid(string? marketId) => GetById(marketId ?? string.Empty) is not null;

        public string Normalize(string marketId) => marketId.ToLowerInvariant();
    }


    internal sealed class CapturingEmailProvider : IEmailNotificationProvider
    {
        public List<EmailNotificationRequest> Sent { get; } = [];

        public Task SendAsync(EmailNotificationRequest request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.CompletedTask;
        }
    }

    internal sealed class CapturingSmsProvider : ISmsNotificationProvider
    {
        public List<SmsNotificationRequest> Sent { get; } = [];

        public Task SendAsync(SmsNotificationRequest request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class StubHttpHandler(List<HttpRequestMessage> captured, HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            captured.Add(request);
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
