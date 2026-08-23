namespace Adeni.Api.Tests.Errors;

using Adeni.Api.Errors;
using Adeni.Api.Middleware;
using Adeni.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

public sealed class ApiErrorResponseMapperTests
{
    [Fact]
    public void Forbidden_entitlement_error_includes_code_and_params()
    {
        var error = ErrorCodes.BookingLimitReachedError(10);
        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdMiddleware.ItemKey] = "corr-123";

        var result = ApiErrorResponseMapper.ToActionResult(error, httpContext);
        var problem = Assert.IsType<ObjectResult>(result).Value as ProblemDetails;

        Assert.NotNull(problem);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(ErrorCodes.BookingLimitReached, problem.Extensions["code"]);
        Assert.Equal("corr-123", problem.Extensions["correlationId"]);

        var parameters = Assert.IsType<Dictionary<string, object?>>(problem.Extensions["params"]);
        Assert.Equal(10, parameters["limit"]);
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
    }

    [Theory]
    [InlineData("validation", 400)]
    [InlineData("not_found", 404)]
    [InlineData("forbidden", 403)]
    [InlineData("conflict", 409)]
    [InlineData(ErrorCodes.SlotExpired, 400)]
    [InlineData(ErrorCodes.SlotUnavailable, 409)]
    [InlineData(ErrorCodes.MultiLocationRequired, 403)]
    public void ResolveStatusCode_maps_legacy_and_dotted_codes(string code, int expectedStatus) =>
        Assert.Equal(expectedStatus, ApiErrorResponseMapper.ResolveStatusCode(code));

    [Fact]
    public void Legacy_validation_error_still_returns_problem_details_with_title()
    {
        var error = Error.Validation("Phone is required.");
        var result = ApiErrorResponseMapper.ToActionResult(error, new DefaultHttpContext());
        var problem = Assert.IsType<ObjectResult>(result).Value as ProblemDetails;

        Assert.NotNull(problem);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("validation", problem.Extensions["code"]);
        Assert.Equal("Phone is required.", problem.Title);
    }
}
