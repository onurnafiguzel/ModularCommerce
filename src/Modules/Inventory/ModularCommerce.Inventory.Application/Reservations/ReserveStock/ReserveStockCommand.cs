namespace ModularCommerce.Inventory.Application.Reservations.ReserveStock;

/// <summary>Stok rezervasyon komutu (FR-3.2).</summary>
public sealed record ReserveStockCommand(Guid ProductId, int Quantity);
