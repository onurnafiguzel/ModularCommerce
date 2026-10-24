using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModularCommerce.Mcp.Clients;
using Xunit;

namespace ModularCommerce.Mcp.Tests;

/// <summary>
/// place_order'ın retry güvenliği: resilience pipeline bir denemeyi tekrarladığında, Idempotency-Key
/// header'ı HER zaman AYNI değeri taşımalı (idempotencyKey parametresi retry döngüsünün dışında,
/// bir kez sabitlenir — ComputePayloadHash gibi her çağrıda yeniden üretilmez). Ayrıca 4xx (422 dahil)
/// ASLA retry edilmemeli — yalnız 5xx/408/429 ve transport hataları.
/// </summary>
public sealed class PlaceOrderRetryTests
{
    private static ModularCommerceApiClient BuildClient(FakeHttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services
            .AddModularCommerceApiClient("http://localhost")
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider().GetRequiredService<ModularCommerceApiClient>();
    }

    [Fact(DisplayName = "İlk deneme 503 (geçici) → retry edilir, İKİNCİ denemede AYNI Idempotency-Key header'ı gider")]
    public async Task FirstAttemptTransientFailure_RetriesWithIdenticalIdempotencyKey()
    {
        const string fixedKey = "sabit-niyet-anahtari-001";
        var capturedKeys = new List<string?>();

        var handler = new FakeHttpMessageHandler(request =>
        {
            capturedKeys.Add(request.Headers.TryGetValues("Idempotency-Key", out var values)
                ? values.First()
                : null);

            return capturedKeys.Count == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) // geçici — retry tetikler
                : new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("""{"id":"11111111-1111-1111-1111-111111111111"}"""),
                };
        });

        var client = BuildClient(handler);

        var result = await client.PlaceOrderAsync(fixedKey, CancellationToken.None);

        result.Should().Contain("11111111-1111-1111-1111-111111111111");
        capturedKeys.Should().HaveCount(2, "ilk deneme başarısız oldu, bir kez daha denenmeli");
        capturedKeys.Should().AllSatisfy(key => key.Should().Be(fixedKey),
            "idempotencyKey retry döngüsünün İÇİNDE asla yeniden üretilmemeli");
    }

    [Theory(DisplayName = "4xx yanıtları (422 dahil) RETRY EDİLMEZ — tek deneme, McpException fırlatılır")]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task FourXxResponse_IsNeverRetried(HttpStatusCode statusCode)
    {
        var callCount = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(
                    """{"code":"Ordering.Checkout.IdempotencyKeyReused","detail":"farklı niyet","retryable":false}"""),
            };
        });

        var client = BuildClient(handler);

        var act = async () => await client.PlaceOrderAsync("herhangi-bir-key", CancellationToken.None);

        await act.Should().ThrowAsync<McpException>();
        callCount.Should().Be(1, "4xx terminal bir client hatasıdır, retry sonucu değiştirmez");
    }

    [Fact(DisplayName = "429 (rate limit) GEÇİCİ sayılır ve retry edilir")]
    public async Task TooManyRequests_IsRetried()
    {
        var callCount = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            callCount++;
            return callCount == 1
                ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                : new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("""{"id":"22222222-2222-2222-2222-222222222222"}"""),
                };
        });

        var client = BuildClient(handler);

        var result = await client.PlaceOrderAsync("baska-bir-key", CancellationToken.None);

        result.Should().Contain("22222222-2222-2222-2222-222222222222");
        callCount.Should().Be(2);
    }

    [Fact(DisplayName = "McpException mesajı retryable=false için 'kalıcı hata', retryable=true için 'geçici hata' der")]
    public async Task McpExceptionMessage_ReflectsRetryability()
    {
        var terminalHandler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent("""{"code":"Ordering.Checkout.IdempotencyKeyReused","detail":"farklı sepet","retryable":false}"""),
        });
        var terminalClient = BuildClient(terminalHandler);
        var terminalAct = async () => await terminalClient.PlaceOrderAsync("key-terminal", CancellationToken.None);
        var terminalException = await terminalAct.Should().ThrowAsync<McpException>();
        terminalException.Which.Message.Should().Contain("kalıcı bir hata");

        var retryableHandler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent("""{"code":"Payment.InFlight","detail":"işleniyor","retryable":true}"""),
        });
        var retryableClient = BuildClient(retryableHandler);
        var retryableAct = async () => await retryableClient.PlaceOrderAsync("key-retryable", CancellationToken.None);
        var retryableException = await retryableAct.Should().ThrowAsync<McpException>();
        retryableException.Which.Message.Should().Contain("geçici bir hata");
    }

    [Fact(DisplayName = "Domain-seviyeli retryable=true (409 Payment.InFlight) Polly tarafından OTOMATİK retry edilmez — çağıran ajan karar vermeli")]
    public async Task DomainRetryable409_IsNotAutomaticallyRetriedByPolly()
    {
        // 409, transient-hata predicate'inde YOK (yalnız 5xx/408/429) — bilinçli bir ayrım:
        // "retryable=true" çağıranın (LLM ajanının) AYNI key ile YENİ bir tool çağrısı yapması
        // gerektiği anlamına gelir, Polly'nin TEK çağrı içinde sessizce tekrar denemesi değil.
        var callCount = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent("""{"code":"Payment.InFlight","detail":"işleniyor","retryable":true}"""),
            };
        });

        var client = BuildClient(handler);
        var act = async () => await client.PlaceOrderAsync("tek-cagri-key", CancellationToken.None);

        await act.Should().ThrowAsync<McpException>();
        callCount.Should().Be(1, "409 Polly'nin transient kümesinde değil — otomatik retry edilmemeli");
    }
}
