using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModularCommerce.Mcp.Clients;
using ModularCommerce.Mcp.Tools;
using Xunit;

namespace ModularCommerce.Mcp.Tests;

public sealed class StoreToolsTests
{
    private static StoreTools BuildTools(FakeHttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services
            .AddModularCommerceApiClient("http://localhost")
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        var apiClient = services.BuildServiceProvider().GetRequiredService<ModularCommerceApiClient>();
        return new StoreTools(apiClient, TestSupport.CreateExecutionWrapper());
    }

    [Fact(DisplayName = "place_order tool'u hata mesajına idempotency_key'e özgü rehberlik ekler (AYNI/YENİ key)")]
    public async Task PlaceOrder_WrapsErrorWithIdempotencyKeyGuidance()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent(
                """{"code":"Ordering.Checkout.IdempotencyKeyReused","detail":"farklı sepet","retryable":false}"""),
        });
        var tools = BuildTools(handler);

        var act = async () => await tools.PlaceOrder("gozlemlenen-key-42", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<McpException>();
        exception.Which.Message.Should().Contain("gozlemlenen-key-42");
        exception.Which.Message.Should().Contain("YENİ bir idempotency_key");
    }

    [Fact(DisplayName = "check_stock tool'u geçersiz GUID için açıklayıcı McpException fırlatır")]
    public async Task CheckStock_InvalidGuid_ThrowsClearError()
    {
        var tools = BuildTools(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("HTTP çağrısı hiç yapılmamalıydı")));

        var act = async () => await tools.CheckStock("gecerli-olmayan-guid", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<McpException>();
        exception.Which.Message.Should().Contain("gecerli-olmayan-guid");
    }
}
