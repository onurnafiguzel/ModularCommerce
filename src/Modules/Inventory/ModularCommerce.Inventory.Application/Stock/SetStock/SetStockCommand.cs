namespace ModularCommerce.Inventory.Application.Stock.SetStock;

/// <summary>
/// Dev-only stok reset komutu: ürünün stok kaydını ve rezervasyonlarını silip
/// </summary>
public sealed record SetStockCommand(Guid ProductId, int OnHand);
