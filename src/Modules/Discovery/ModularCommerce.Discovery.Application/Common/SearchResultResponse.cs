namespace ModularCommerce.Discovery.Application.Common;

/// <summary>Arama sonucu satırı: ürün kimliği + benzerlik skoru (1 = birebir, 0 = ilgisiz).</summary>
public sealed record SearchResultResponse(Guid ProductId, double Score);
