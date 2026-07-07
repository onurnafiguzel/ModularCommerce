namespace ModularCommerce.Inventory.Contracts;
public sealed record StockReservationDto(
    Guid ReservationId,
    Guid ProductId,
    int Quantity,
    DateTime ExpiresAtUtc);
