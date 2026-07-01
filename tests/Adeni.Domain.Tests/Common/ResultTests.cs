namespace Adeni.Domain.Tests.Common;

using Adeni.Domain.Common;

public sealed class ErrorTests
{
    [Fact]
    public void NotFound_creates_expected_error()
    {
        var error = Error.NotFound("Booking");

        Assert.Equal("not_found", error.Code);
        Assert.Contains("Booking", error.Message);
    }

    [Fact]
    public void Forbidden_creates_expected_error()
    {
        var error = Error.Forbidden("denied");

        Assert.Equal("forbidden", error.Code);
        Assert.Equal("denied", error.Message);
    }

    [Fact]
    public void Validation_creates_expected_error()
    {
        var error = Error.Validation("invalid");

        Assert.Equal("validation", error.Code);
        Assert.Equal("invalid", error.Message);
    }
}

public sealed class ResultTests
{
    [Fact]
    public void Success_result_matches_on_success()
    {
        var result = Result.Success();

        var value = result.Match(() => "ok", _ => "fail");

        Assert.Equal("ok", value);
    }

    [Fact]
    public void Failure_result_matches_on_failure()
    {
        var error = Error.Validation("bad");
        var result = Result.Failure(error);

        var value = result.Match(() => "ok", e => e.Code);

        Assert.Equal("validation", value);
    }

    [Fact]
    public void Generic_result_map_transforms_success()
    {
        var result = Result.Success(2);

        var mapped = result.Map(x => x * 3);

        Assert.True(mapped.IsSuccess);
        Assert.Equal(6, mapped.Value);
    }

    [Fact]
    public void Generic_result_map_preserves_failure()
    {
        var error = Error.NotFound("x");
        var result = Result.Failure<int>(error);

        var mapped = result.Map(x => x * 3);

        Assert.True(mapped.IsFailure);
        Assert.Equal(error, mapped.Error);
    }

    [Fact]
    public void Generic_result_bind_chains_success()
    {
        var result = Result.Success(2);

        Result<int> Divide(int x) => x == 0
            ? Result.Failure<int>(Error.Validation("zero"))
            : Result.Success(10 / x);

        var bound = result.Bind(Divide);

        Assert.True(bound.IsSuccess);
        Assert.Equal(5, bound.Value);
    }

    [Fact]
    public void Generic_result_bind_short_circuits_on_failure()
    {
        var result = Result.Failure<int>(Error.Validation("bad"));

        var bound = result.Bind(x => Result.Success(x * 2));

        Assert.True(bound.IsFailure);
    }

    [Fact]
    public void Generic_result_match_returns_expected_branch()
    {
        var success = Result.Success("value");
        var failure = Result.Failure<string>(Error.NotFound("item"));

        Assert.Equal("value", success.Match(v => v, _ => "fail"));
        Assert.Equal("fail", failure.Match(_ => "ok", _ => "fail"));
    }
}
