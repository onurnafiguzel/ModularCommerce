using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ModularCommerce.Mcp.IntegrationTests;

/// <summary>
/// Inventory'nin dev-only PUT /api/inventory/dev/stock/{id} endpoint'i yalnız Development ortamında
/// map'leniyor (InventoryModule.MapEndpoints → IHostEnvironment.IsDevelopment()). k8s'teki GERÇEK
/// deploy ASPNETCORE_ENVIRONMENT=Production ile çalışır (bkz. k8s/configmap.yaml) — bu testin amacı
/// o garantiyi koda bağlamak: Production'da bu route hiç var olmamalı (404), elle denetime güvenmek
/// yerine CI'da otomatik kırılsın.
/// </summary>
[Collection(nameof(McpHostCollection))]
public sealed class DevEndpointProductionGuardTests(McpHostFixture fixture)
{
    [Fact(DisplayName = "Production ortamında PUT /api/inventory/dev/stock/{id} 404 döner (dev-only endpoint map edilmemeli)")]
    public async Task DevStockEndpoint_InProductionEnvironment_ReturnsNotFound()
    {
        using var productionFactory = fixture.CreateProductionFactory();
        using var client = productionFactory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/inventory/dev/stock/{Guid.NewGuid()}", new { OnHand = 10 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
