using Tiki.Shared.Core.Models;
using Tiki.Shared.Results;
using Xunit;

namespace Tiki.Shared.Tests.Results;

public class ResultExtensionsTests
{
    [Fact]
    public void Generic_success_maps_to_a_successful_envelope_carrying_the_value()
    {
        Result<int> result = 42;

        var response = result.ToApiResponse("fetched");

        Assert.True(response.Success);
        Assert.Equal(42, response.Data);
        Assert.Equal("fetched", response.Message);
        Assert.Null(response.ErrorCode);
    }

    [Fact]
    public void Generic_failure_maps_to_a_failed_envelope_carrying_the_error()
    {
        Result<int> result = Error.NotFound("wallet.not_found", "Wallet not found.");

        var response = result.ToApiResponse();

        Assert.False(response.Success);
        Assert.Equal(default, response.Data);
        Assert.Equal("Wallet not found.", response.Message);
        Assert.Equal("wallet.not_found", response.ErrorCode);
    }

    [Fact]
    public void Non_generic_success_maps_to_a_successful_envelope()
    {
        var result = Result.Success();

        var response = result.ToApiResponse("done");

        Assert.True(response.Success);
        Assert.Equal("done", response.Message);
        Assert.Null(response.ErrorCode);
    }

    [Fact]
    public void Non_generic_failure_maps_to_a_failed_envelope()
    {
        var result = Result.Failure(Error.Conflict("wallet.locked", "Wallet is locked."));

        var response = result.ToApiResponse();

        Assert.False(response.Success);
        Assert.Equal("Wallet is locked.", response.Message);
        Assert.Equal("wallet.locked", response.ErrorCode);
    }
}