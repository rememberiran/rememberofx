using Application;
using Microsoft.AspNetCore.Mvc;

namespace Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return new OkResult();
        }

        return ToErrorResult(result.Error!);
    }

    public static IActionResult ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(result.Value);
        }

        return ToErrorResult(result.Error!);
    }

    private static ObjectResult ToErrorResult(DomainError error)
    {
        var body = new { error = error.Code, message = error.Message };

        return error.StatusCode switch
        {
            400 => new BadRequestObjectResult(body),
            401 => new UnauthorizedObjectResult(body),
            403 => new ObjectResult(body) { StatusCode = 403 },
            404 => new NotFoundObjectResult(body),
            409 => new ConflictObjectResult(body),
            _ => new ObjectResult(body) { StatusCode = error.StatusCode },
        };
    }
}
