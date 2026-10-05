using Xunit;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Tiki.Shared.Core.Exceptions;
using Tiki.Shared.Core.Middleware;

namespace Tiki.Shared.Tests.Middleware;

/// <summary>
/// A validation failure must reach the caller with the field it is about. The middleware once
/// wrote the problem by its declared base type, which silently dropped the <c>errors</c> map.
/// </summary>
public sealed class ErrorHandlingMiddlewareTests
{
    [Fact]
    public async Task A_validation_failure_keeps_its_field_errors()
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var middleware = new ErrorHandlingMiddleware(
            _ => throw new ValidationException(new Dictionary<string, string[]>
            {
                ["documentType"] = ["Choose one of the accepted document types."],
            }),
            NullLogger<ErrorHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        var errors = body.RootElement.GetProperty("errors");
        Assert.Equal("Choose one of the accepted document types.", errors.GetProperty("documentType")[0].GetString());
    }
}
