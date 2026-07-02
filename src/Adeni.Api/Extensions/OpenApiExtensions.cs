namespace Adeni.Api.Extensions;

using Microsoft.AspNetCore.OpenApi;
using Scalar.AspNetCore;

public static class OpenApiExtensions
{
    public static IServiceCollection AddAdeniOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "Adeni API";
                document.Info.Version = "v1";
                document.Info.Description = "Trusted local services marketplace.";
                return Task.CompletedTask;
            });
        });

    public static WebApplication MapAdeniOpenApi(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.WithTitle("Adeni API");
            options.WithOpenApiRoutePattern("/openapi/{documentName}.json");
        });

        app.MapGet("/swagger", () => Results.Redirect("/scalar/v1"))
            .ExcludeFromDescription();

        return app;
    }
}
