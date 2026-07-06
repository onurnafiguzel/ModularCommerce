namespace ModularCommerce.Inventory.Application.Stock.GetStock;

/// <summary>Ürün stok durumu yanıtı.</summary>
public sealed record StockResponse(
    Guid ProductId,
    int OnHand,
    int Reserved,
    int Available);
