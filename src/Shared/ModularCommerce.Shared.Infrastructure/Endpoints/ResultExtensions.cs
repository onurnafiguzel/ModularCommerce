using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Shared.Infrastructure.Endpoints;

public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result)
        => result.IsSuccess ? Results.Ok(result.Value) : new ProblemResult(result.Error);

    public static IResult ToHttpResult(this Result result)
        => result.IsSuccess ? Results.NoContent() : new ProblemResult(result.Error);

    /// <summary>
    /// Hata → ProblemDetails eşlemesi. Doğrudan Results.Problem yerine bir IResult:
    /// böylece ExecuteAsync HttpContext'i görür ve gövdeyi IProblemDetailsService üzerinden
    /// yazar — 4xx yanıtları da 500'lerle AYNI kabuğu (traceId + correlationId, CustomizeProblemDetails)
    /// alır. Ek olarak `code` (makine-okunur) ve retryable-409'larda `retryable: true` yazılır.
    /// </summary>
    private sealed class ProblemResult(Error error) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var statusCode = ProblemMapping.StatusCodeFor(error.Type);
            httpContext.Response.StatusCode = statusCode;

            var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = ProblemMapping.ForError(error, statusCode),
            });
        }
    }
}
