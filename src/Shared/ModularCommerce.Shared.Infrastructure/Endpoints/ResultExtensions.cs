using Microsoft.AspNetCore.Http;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Shared.Infrastructure.Endpoints;

public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result)
        => result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Error);

    public static IResult ToHttpResult(this Result result)
        => result.IsSuccess ? Results.NoContent() : ToProblem(result.Error);

    private static IResult ToProblem(Error error)
        => Results.Problem(
            statusCode: error.Type switch
            {
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError,
            },
            title: error.Code,
            detail: error.Message);
}