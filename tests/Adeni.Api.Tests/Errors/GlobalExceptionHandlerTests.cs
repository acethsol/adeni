namespace Adeni.Api.Tests.Errors;

using Adeni.Api.Errors;
using Adeni.Api.Middleware;
using Adeni.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_returns_500_problem_details_with_correlation_id()
    {
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var handler = new GlobalExceptionHandler(environment, NullLogger<GlobalExceptionHandler>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.Items[CorrelationIdMiddleware.ItemKey] = "ex-corr-99";

        var handled = await handler.TryHandleAsync(
            httpContext,
            new InvalidOperationException("database exploded"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Contains(ErrorCodes.InternalServerError, body);
        Assert.Contains("ex-corr-99", body);
        Assert.DoesNotContain("database exploded", body);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Adeni.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
