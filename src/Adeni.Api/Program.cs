using Adeni.Api.Auth;
using Adeni.Api.Errors;
using Adeni.Api.Extensions;
using Adeni.Api.Middleware;
using Adeni.Application.Abstractions;
using Adeni.Application.DependencyInjection;
using Adeni.Application.Markets;
using Adeni.Infrastructure.Configuration;
using Adeni.Infrastructure.DependencyInjection;
using Adeni.Infrastructure.Logging;
using Adeni.Infrastructure.Persistence;
using Adeni.Application.Storage;
using Adeni.Application.Reporting;
using Adeni.Infrastructure.Storage;
using Adeni.Infrastructure.Tenancy;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddAdeniKeyVault(builder.Environment);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Destructure.With<PiiDestructuringPolicy>()
    .Filter.ByExcluding(CalendarOAuthLogFilter.ShouldExclude)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.ValidateProductionConfiguration();
builder.Services.AddAdeniForwardedHeaders(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser>(sp =>
    sp.GetRequiredService<IHttpContextAccessor>().HttpContext?.User.ToCurrentUser()
    ?? new HttpCurrentUser(null, [], null, false));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddAdeniHangfire(builder.Configuration, builder.Environment);
builder.Services.AddAdeniStorage(builder.Configuration, builder.Environment);
builder.Services.Replace(ServiceDescriptor.Scoped<IReportPdfBrandingLoader, ReportPdfBrandingLoader>());
builder.Services.AddAdeniCors(builder.Configuration, builder.Environment);
builder.Services.AddAdeniObservability(builder.Configuration);
builder.Services.AddAdeniRateLimiting(builder.Environment);
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSingleton<Adeni.Application.Messaging.IMessagingRealtimePublisher, Adeni.Api.Messaging.SignalRMessagingRealtimePublisher>();
builder.Services.AddSingleton<Adeni.Application.Realtime.ITenantRealtimePublisher, Adeni.Api.Realtime.SignalRTenantRealtimePublisher>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAdeniOpenApi();
}

var app = builder.Build();
var fileStorage = app.Services.GetRequiredService<IFileStorage>();

if (ShouldSeedSampleData(app.Environment, app.Configuration))
{
    try
    {
        await SeedSampleDataAsync(app.Services);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Sample data seed skipped — publish the database first (./scripts/publish-db.ps1) and ensure SQL Server is running.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapAdeniOpenApi();
}

await WarmMarketCatalogAsync(app.Services);

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationId)
            && correlationId is not null)
        {
            diagnosticContext.Set(CorrelationIdMiddleware.LogContextPropertyName, correlationId);
        }
    };
});
app.UseHttpsRedirection();

var storageProvider = app.Configuration.GetSection(StorageOptions.SectionName)["Provider"] ?? "Local";
if (string.Equals(storageProvider, "Local", StringComparison.OrdinalIgnoreCase)
    && fileStorage is LocalFileStorage localFileStorage)
{
    var localRoot = localFileStorage.GetRootPath();
    Directory.CreateDirectory(localRoot);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(localRoot),
        RequestPath = "/media"
    });
}

app.UseAdeniHangfire();

app.UseAdeniCors();
app.UseAdeniRateLimiting();
app.UseAdeniSecurityPipeline();
app.UseAuthorization();
app.MapControllers();
app.MapHub<Adeni.Api.Messaging.MessagingHub>("/hubs/messaging");
app.MapHub<Adeni.Api.Realtime.OperationsHub>("/hubs/operations");

app.Run();

static bool ShouldSeedSampleData(IHostEnvironment environment, IConfiguration configuration)
{
    var enabled = configuration.GetSection("SampleData").GetValue<bool?>("Enabled");
    if (enabled.HasValue)
    {
        return enabled.Value;
    }

    return environment.IsDevelopment() || environment.IsStaging();
}

static async Task SeedSampleDataAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
    if (!ShouldSeedSampleData(environment, configuration))
    {
        return;
    }

    var hasSql = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("AdeniDb"));
    // Empty AdeniDb in Development uses the in-memory provider — still seed sample tenants.
    if (!hasSql && !environment.IsDevelopment())
    {
        return;
    }

    var dbContext = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
    var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
    tenantContext.DisableTenantFilter();
    dbContext.SyncTenantFilter();
    var sampleSqlDir = SampleDataSqlSeedRunner.ResolveScriptsDirectory(environment, configuration);
    if (configuration.GetValue<bool>("SampleData:CleanupVolumeDemo"))
    {
        await DevelopmentDataSeeder.CleanupVolumeDemoAsync(dbContext, sampleSqlDir);
    }

    await DevelopmentDataSeeder.SeedAsync(dbContext, sampleSqlDir);
    var loader = scope.ServiceProvider.GetRequiredService<IMarketCatalogLoader>();
    await loader.EnsureLoadedAsync();
}

static async Task WarmMarketCatalogAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("AdeniDb")))
    {
        var inMemoryLoader = scope.ServiceProvider.GetService<IMarketCatalogLoader>();
        if (inMemoryLoader is not null)
        {
            try
            {
                await inMemoryLoader.EnsureLoadedAsync();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Market catalog warm-up skipped.");
            }
        }

        return;
    }

    try
    {
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.DisableTenantFilter();
        var dbContext = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        dbContext.SyncTenantFilter();
        var loader = scope.ServiceProvider.GetRequiredService<IMarketCatalogLoader>();
        await loader.EnsureLoadedAsync();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Market catalog warm-up skipped.");
    }
}

public partial class Program;
