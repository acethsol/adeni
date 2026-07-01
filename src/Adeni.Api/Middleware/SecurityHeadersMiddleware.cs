namespace Adeni.Api.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string StrictApiCsp =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";

    private const string ApiDocumentationCsp =
        "default-src 'none'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'";

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Content-Security-Policy"] = IsApiDocumentationPath(context.Request.Path)
            ? ApiDocumentationCsp
            : StrictApiCsp;
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

        if (context.Request.IsHttps)
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        await next(context);
    }

    internal static bool IsApiDocumentationPath(PathString path) =>
        path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/swagger", StringComparison.OrdinalIgnoreCase);
}
