using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Shared.Infrastructure.Endpoints;

/// <summary>
/// Hata → HTTP eşlemesinin TEK doğruluk kaynağı. Hem endpoint hata yolu (ToHttpResult) hem
/// rate-limiter (429) buradan geçer ki tüm hata gövdeleri aynı kabuğu paylaşsın:
/// { title, detail, status, code, retryable? } + (IProblemDetailsService ile) traceId/correlationId.
/// </summary>
public static class ProblemMapping
{
    /// <summary>Yanıt gövdesinde makine-okunur hata kodu alanının adı.</summary>
    public const string CodeExtension = "code";

    /// <summary>retryable-409 sözleşmesinin makine-okunur bayrağı (aynı key ile tekrar dene).</summary>
    public const string RetryableExtension = "retryable";

    public static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>Domain hatasından ProblemDetails üretir (code + retryable dahil).</summary>
    public static ProblemDetails ForError(Error error, int statusCode)
    {
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = error.Code,
            Detail = error.Message,
        };

        problemDetails.Extensions[CodeExtension] = error.Code;
        if (error.Retryable)
        {
            problemDetails.Extensions[RetryableExtension] = true;
        }

        return problemDetails;
    }
}
