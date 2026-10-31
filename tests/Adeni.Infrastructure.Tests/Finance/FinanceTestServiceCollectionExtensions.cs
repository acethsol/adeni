namespace Adeni.Infrastructure.Tests.Finance;

using Adeni.Application.Finance;
using Adeni.Application.Storage;
using Adeni.Infrastructure.Booking;
using Adeni.Infrastructure.Finance;
using Adeni.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

internal static class FinanceTestServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentOrchestratorDependencies(this IServiceCollection services)
    {
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddScoped<ICustomerPaymentAccessQuery, CustomerPaymentAccessQuery>();
        services.AddScoped<IBookingDepositCalculator, BookingDepositCalculator>();
        services.AddScoped<IPaymentCallbackUrlValidator, PaymentCallbackUrlValidator>();
        return services;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Adeni.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    public static IServiceCollection AddMediaUploadBlobValidation(this IServiceCollection services)
    {
        services.AddScoped<IMediaUploadBlobValidator, MediaUploadBlobValidator>();
        return services;
    }
}
