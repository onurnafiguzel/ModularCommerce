using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModularCommerce.Mcp.Clients;
using ModularCommerce.Mcp.Tools;
using Xunit;

namespace ModularCommerce.Mcp.Tests;

/// <summary>
/// set_cart_item'in "miktarı X yap" semantiği ve idempotentliği: PUT /items/{id} API'si zaten
/// SET davranışında (artırmıyor), ama sadece ürün sepette VARKEN kullanılabilir — bu yüzden tool
/// önce GetCart ile mevcut durumu okuyup PUT (var)/POST (yok, sıfırdan set)/DELETE (quantity=0)
/// arasında doğru fiili seçmeli, aynı çağrı tekrarlansa bile sepeti bozmamalı.
/// </summary>
public sealed class CartToolsTests
{
    private static readonly Guid ProductId = Guid.Parse("9de537a5-e680-4d99-81fd-ff657f1a1dc9");

    private static string CartJson(params (Guid ProductId, int Quantity)[] items)
    {
        var itemsJson = string.Join(',', items.Select(i =>
            $$"""{"productId":"{{i.ProductId}}","quantity":{{i.Quantity}},"addedAtUtc":"2026-01-01T00:00:00Z"}"""));
        return $$"""{"customerId":"00000000-0000-0000-0000-000000000001","items":[{{itemsJson}}],"warning":""}""";
    }

    private static StoreTools BuildTools(FakeHttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services
            .AddModularCommerceApiClient("http://localhost")
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        var apiClient = services.BuildServiceProvider().GetRequiredService<ModularCommerceApiClient>();
        return new StoreTools(apiClient, TestSupport.CreateExecutionWrapper());
    }

    [Fact(DisplayName = "get_cart sepet gövdesini olduğu gibi döner")]
    public async Task GetCart_ReturnsCartBody()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(CartJson((ProductId, 3))),
        });
        var tools = BuildTools(handler);

        var result = await tools.GetCart(CancellationToken.None);

        result.Should().Contain(ProductId.ToString());
        result.Should().Contain("\"quantity\":3");
    }

    [Fact(DisplayName = "set_cart_item: ürün sepette YOKKEN quantity>0 → POST /items çağrılır (PUT değil)")]
    public async Task SetCartItem_ProductNotInCart_PositiveQuantity_CallsPost()
    {
        var requests = new List<(HttpMethod Method, string Path)>();
        var handler = new FakeHttpMessageHandler(request =>
        {
            requests.Add((request.Method, request.RequestUri!.AbsolutePath));

            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(CartJson()) };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(CartJson((ProductId, 5))) };
        });
        var tools = BuildTools(handler);

        await tools.SetCartItem(ProductId.ToString(), 5, CancellationToken.None);

        requests.Should().ContainSingle(r => r.Method == HttpMethod.Get && r.Path == "/api/cart");
        requests.Should().ContainSingle(r => r.Method == HttpMethod.Post && r.Path == "/api/cart/items");
        requests.Should().NotContain(r => r.Method == HttpMethod.Put);
    }

    [Fact(DisplayName = "set_cart_item: ürün sepette ZATEN VARKEN quantity>0 → PUT /items/{id} çağrılır (POST değil, artırmaz)")]
    public async Task SetCartItem_ProductAlreadyInCart_PositiveQuantity_CallsPut()
    {
        var requests = new List<(HttpMethod Method, string Path)>();
        var handler = new FakeHttpMessageHandler(request =>
        {
            requests.Add((request.Method, request.RequestUri!.AbsolutePath));

            return request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(CartJson((ProductId, 2))) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(CartJson((ProductId, 7))) };
        });
        var tools = BuildTools(handler);

        await tools.SetCartItem(ProductId.ToString(), 7, CancellationToken.None);

        requests.Should().ContainSingle(r => r.Method == HttpMethod.Put && r.Path == $"/api/cart/items/{ProductId}");
        requests.Should().NotContain(r => r.Method == HttpMethod.Post);
    }

    [Fact(DisplayName = "set_cart_item: AYNI çağrı iki kez tekrarlanırsa her ikisinde de PUT gider, miktar katlanmaz (idempotent)")]
    public async Task SetCartItem_CalledTwiceWithSameArgs_IsIdempotent()
    {
        var putBodies = new List<string>();
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Put)
            {
                putBodies.Add(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(CartJson((ProductId, 4))) };
            }

            // Her GET, ürünün ZATEN sepette (quantity=4) olduğunu gösteriyor — ikinci çağrı da
            // PUT'a gitmeli, POST'a değil.
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(CartJson((ProductId, 4))) };
        });
        var tools = BuildTools(handler);

        await tools.SetCartItem(ProductId.ToString(), 4, CancellationToken.None);
        await tools.SetCartItem(ProductId.ToString(), 4, CancellationToken.None);

        putBodies.Should().HaveCount(2);
        putBodies.Should().AllSatisfy(body => body.Should().Contain("\"quantity\":4"));
    }

    [Fact(DisplayName = "set_cart_item: quantity=0 ve ürün sepette VARKEN → DELETE çağrılır")]
    public async Task SetCartItem_ZeroQuantity_ProductInCart_CallsDelete()
    {
        var requests = new List<(HttpMethod Method, string Path)>();
        var handler = new FakeHttpMessageHandler(request =>
        {
            requests.Add((request.Method, request.RequestUri!.AbsolutePath));

            return request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(CartJson((ProductId, 2))) }
                : new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var tools = BuildTools(handler);

        await tools.SetCartItem(ProductId.ToString(), 0, CancellationToken.None);

        requests.Should().ContainSingle(r => r.Method == HttpMethod.Delete && r.Path == $"/api/cart/items/{ProductId}");
    }

    [Fact(DisplayName = "set_cart_item: quantity=0 ve ürün ZATEN sepette YOKKEN → DELETE HİÇ çağrılmaz (no-op, idempotent — ItemNotFound hatası asla görülmez)")]
    public async Task SetCartItem_ZeroQuantity_ProductNotInCart_IsNoOpAndNeverCallsDelete()
    {
        var requests = new List<HttpMethod>();
        var handler = new FakeHttpMessageHandler(request =>
        {
            requests.Add(request.Method);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(CartJson()) };
        });
        var tools = BuildTools(handler);

        var result = await tools.SetCartItem(ProductId.ToString(), 0, CancellationToken.None);

        requests.Should().ContainSingle().Which.Should().Be(HttpMethod.Get);
        result.Should().NotBeNull();
    }

    [Fact(DisplayName = "set_cart_item: negatif quantity McpException ile reddedilir, hiç HTTP çağrısı yapılmaz")]
    public async Task SetCartItem_NegativeQuantity_ThrowsWithoutCallingApi()
    {
        var tools = BuildTools(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("HTTP çağrısı hiç yapılmamalıydı")));

        var act = async () => await tools.SetCartItem(ProductId.ToString(), -1, CancellationToken.None);

        await act.Should().ThrowAsync<McpException>();
    }
}
