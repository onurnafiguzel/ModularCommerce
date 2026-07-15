using MassTransit;
using ModularCommerce.Catalog.Contracts.IntegrationEvents;
using ModularCommerce.Discovery.Application.Indexing;

namespace ModularCommerce.Discovery.Api.Consumers;

/// <summary>
/// Catalog'un ürün olaylarını tüketip ürünü (yeniden) indeksler. İndeksleme upsert + hash-atlama ile
/// idempotenttir → at-least-once tekrar teslimatı güvenli. Hata fırlatılır → MassTransit retry (embedding
/// geçici hatasında yeniden dener; tükenirse `_error` DLQ).
/// </summary>
public sealed class ProductChangedConsumer(IndexProductHandler handler)
    : IConsumer<ProductCreated>, IConsumer<ProductUpdated>
{
    public Task Consume(ConsumeContext<ProductCreated> context)
    {
        var m = context.Message;
        return IndexAsync(new ProductIndexRequest(m.ProductId, m.Name, m.Description, m.Sku), context.CancellationToken);
    }

    public Task Consume(ConsumeContext<ProductUpdated> context)
    {
        var m = context.Message;
        return IndexAsync(new ProductIndexRequest(m.ProductId, m.Name, m.Description, m.Sku), context.CancellationToken);
    }

    private async Task IndexAsync(ProductIndexRequest request, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Ürün indekslenemedi ({request.ProductId}): {result.Error.Code} - {result.Error.Message}");
        }
    }
}
