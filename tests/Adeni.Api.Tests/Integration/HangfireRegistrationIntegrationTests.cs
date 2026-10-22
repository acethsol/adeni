namespace Adeni.Api.Tests.Integration;

using Adeni.Api.Extensions;
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public sealed class HangfireRegistrationIntegrationTests
{
    [Fact]
    public void Testing_environment_does_not_register_hangfire()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AdeniDb"] = string.Empty,
                });
            });
        });

        Assert.False(HangfireExtensions.ShouldRegisterHangfire(
            factory.Services.GetRequiredService<IHostEnvironment>(),
            factory.Services.GetRequiredService<IConfiguration>()));

        Assert.Throws<InvalidOperationException>(() => factory.Services.GetRequiredService<JobStorage>());
    }
}
