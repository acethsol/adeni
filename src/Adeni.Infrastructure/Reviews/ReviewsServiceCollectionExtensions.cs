namespace Adeni.Infrastructure.Reviews;

using Adeni.Application.Reviews;
using Microsoft.Extensions.DependencyInjection;

public static class ReviewsServiceCollectionExtensions
{
    public static IServiceCollection AddReviewsModule(this IServiceCollection services)
    {
        services.AddScoped<IReviewService, ReviewService>();
        return services;
    }
}
