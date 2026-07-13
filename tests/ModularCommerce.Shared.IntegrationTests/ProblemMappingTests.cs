using FluentAssertions;
using Microsoft.AspNetCore.Http;
using ModularCommerce.Shared.Infrastructure.Endpoints;
using ModularCommerce.Shared.Kernel;
using Xunit;

namespace ModularCommerce.Shared.IntegrationTests;

/// <summary>
/// Hata → ProblemDetails eşlemesinin TEK doğruluk kaynağı (web host'suz, saf mantık):
/// ErrorType → HTTP status, `code` (makine-okunur) ve retryable-409'da `retryable: true`.
/// Bu, retryable/terminal istemci sözleşmesinin yapısal (string eşleştirmesiz) kanıtıdır.
/// </summary>
public sealed class ProblemMappingTests
{
    [Theory(DisplayName = "ErrorType doğru HTTP status'a eşlenir")]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorType.Failure, StatusCodes.Status500InternalServerError)]
    public void StatusCodeFor_MapsEachType(ErrorType type, int expected)
        => ProblemMapping.StatusCodeFor(type).Should().Be(expected);

    [Fact(DisplayName = "ForError: title=code, detail=message ve makine-okunur code alanı yazılır")]
    public void ForError_WritesCodeAndDetail()
    {
        var error = Error.NotFound("Sample.NotFound", "Kayıt yok.");

        var problem = ProblemMapping.ForError(error, StatusCodes.Status404NotFound);

        problem.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Title.Should().Be("Sample.NotFound");
        problem.Detail.Should().Be("Kayıt yok.");
        problem.Extensions[ProblemMapping.CodeExtension].Should().Be("Sample.NotFound");
    }

    [Fact(DisplayName = "Retryable-409: gövde 'retryable: true' taşır (aynı key ile tekrar dene)")]
    public void ForError_RetryableConflict_SetsRetryableTrue()
    {
        var error = Error.Conflict("Sample.Transient", "Geçici çakışma.", retryable: true);

        var problem = ProblemMapping.ForError(error, StatusCodes.Status409Conflict);

        problem.Extensions[ProblemMapping.RetryableExtension].Should().Be(true);
    }

    [Fact(DisplayName = "Terminal hata: 'retryable' alanı HİÇ yazılmaz (varlığı = retryable sinyali)")]
    public void ForError_TerminalConflict_OmitsRetryable()
    {
        var error = Error.Conflict("Sample.Terminal", "Kalıcı ret.");

        var problem = ProblemMapping.ForError(error, StatusCodes.Status409Conflict);

        problem.Extensions.Should().NotContainKey(ProblemMapping.RetryableExtension);
    }
}
