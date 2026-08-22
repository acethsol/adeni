namespace Adeni.Infrastructure.Tests.Catalog;

using Adeni.Infrastructure.Catalog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

internal static class CatalogTestSupport
{
    internal static void AddCategoryWorkflowCatalog(this IServiceCollection services)
    {
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddCatalogModule();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Adeni.Infrastructure.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
