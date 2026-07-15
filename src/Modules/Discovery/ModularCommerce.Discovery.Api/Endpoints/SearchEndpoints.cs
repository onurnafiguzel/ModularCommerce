using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ModularCommerce.Discovery.Application.Search;
using ModularCommerce.Shared.Infrastructure.Endpoints;

namespace ModularCommerce.Discovery.Api.Endpoints;

/// <summary>Arama isteği gövdesi. TopN verilmezse 10.</summary>
public sealed record SearchRequest(string Query, int TopN = 10);

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this IEndpointRouteBuilder group)
    {
        // Anonim (Catalog GET'leri gibi). Sorguyu embed eder, kosinüs benzerliğiyle en yakın N ürünü döner.
        group.MapPost("/search", async (
            SearchRequest request,
            SearchProductsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new SearchQuery(request.Query, request.TopN), cancellationToken);
            return result.ToHttpResult();
        });
    }
}
