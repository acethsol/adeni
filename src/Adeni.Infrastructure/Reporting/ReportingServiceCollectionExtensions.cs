namespace Adeni.Infrastructure.Reporting;

using Adeni.Application.Reporting;
using Microsoft.Extensions.DependencyInjection;

public static class ReportingServiceCollectionExtensions
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services)
    {
        services.AddScoped<IReportRenderer, QuestPdfReportRenderer>();
        services.AddScoped<IReportExportService, ReportExportService>();
        return services;
    }
}
