namespace ModularCommerce.Discovery.Application.Indexing;

/// <summary>
/// Bir ürünü indeksleme isteği — Catalog integration event'inin taşıdığı aranabilir alanlar.
/// (Consumer bu isteği event'ten kurar; handler embedding üretir.)
/// </summary>
public sealed record ProductIndexRequest(
    Guid ProductId,
    string Name,
    string? Description,
    string Sku);
