using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tiki.Shared.Results;

namespace Tiki.Shared.Core.Middleware;

[ApiController]
public class BaseApiController : ControllerBase
{
    [ApiExplorerSettings(IgnoreApi = true)]
    protected ActionResult ApiResponse(Result result) =>
        StatusCode(ToStatusCode(result), result.ToApiResponse());

    [ApiExplorerSettings(IgnoreApi = true)]
    protected ActionResult ApiResponse<T>(Result<T> result) =>
        StatusCode(ToStatusCode(result), result.ToApiResponse());

    private static int ToStatusCode(Result result)
    {
        if (result.IsSuccess)
            return StatusCodes.Status200OK;

        return result.Error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };
    }
}
