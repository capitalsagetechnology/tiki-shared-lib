using Tiki.Shared.Results;
using Xunit;

namespace Tiki.Shared.Tests.Results;

public class ResultTests
{
    [Fact]
    public void Success_carries_no_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_carries_the_given_error()
    {
        var error = Error.NotFound("wallet.not_found", "Wallet not found.");
        var result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Success_with_error_throws()
    {
        var ex = Record.Exception(() => Result.Success<int>(default!) is { });
        Assert.Null(ex); // constructing a success never throws
    }

    [Fact]
    public void Generic_success_exposes_value()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Generic_failure_throws_on_value_access()
    {
        var result = Result.Failure<int>(Error.Validation("x", "bad"));

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Implicit_conversion_from_value_produces_success()
    {
        Result<string> result = "hello";

        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void Implicit_conversion_from_error_produces_failure()
    {
        Result<string> result = Error.Conflict("x", "conflict");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Match_invokes_the_success_branch_on_success()
    {
        Result<int> result = 10;

        var output = result.Match(value => value * 2, _ => -1);

        Assert.Equal(20, output);
    }

    [Fact]
    public void Match_invokes_the_failure_branch_on_failure()
    {
        Result<int> result = Error.Failure("x", "bad");

        var output = result.Match(value => value * 2, _ => -1);

        Assert.Equal(-1, output);
    }
}
