using Adeni.Api.Auth;
using Adeni.Api.Extensions;
using Adeni.Application.Abstractions;
using Adeni.Application.DependencyInjection;
using Adeni.Infrastructure.Configuration;
using Adeni.Infrastructure.DependencyInjection;
using Adeni.Infrastructure.Logging;
using Adeni.Infrastructure.Persistence;
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
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

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
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
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
    await dbContext.Database.MigrateAsync();
}

public partial class Program;
