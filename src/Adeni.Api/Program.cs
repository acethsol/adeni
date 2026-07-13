using Adeni.Api.Auth;
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
using Adeni.Infrastructure.Storage;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddAdeniKeyVault(builder.Environment);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Destructure.With<PiiDestructuringPolicy>()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser>(sp =>
    sp.GetRequiredService<IHttpContextAccessor>().HttpContext?.User.ToCurrentUser()
    ?? new HttpCurrentUser(null, [], null, false));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddAdeniStorage(builder.Configuration, builder.Environment);
builder.Services.AddAdeniCors(builder.Configuration, builder.Environment);
builder.Services.AddAdeniObservability(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAdeniOpenApi();
}

var app = builder.Build();
var fileStorage = app.Services.GetRequiredService<IFileStorage>();

if (app.Environment.IsDevelopment())
{
    try
    {
        await ApplyDevelopmentMigrationsAsync(app.Services);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Database migration skipped — ensure PostgreSQL is running (docker compose up -d).");
    }

    app.MapAdeniOpenApi();
}

await WarmMarketCatalogAsync(app.Services);

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

app.UseAdeniCors();
app.UseAdeniSecurityPipeline();
app.UseAuthorization();
app.MapControllers();

app.Run();

static async Task ApplyDevelopmentMigrationsAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("AdeniDb")))
    {
        return;
    }

    var dbContext = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
    var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
    tenantContext.DisableTenantFilter();
    dbContext.SyncTenantFilter();
    await dbContext.Database.MigrateAsync();
    await DevelopmentDataSeeder.SeedAsync(dbContext);
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
