namespace ModularCommerce.Inventory.Application.Reservations.Common;

public sealed record ReservationResponse(
    Guid ReservationId,
    Guid ProductId,
    int Quantity,
    string Status,
    DateTime ExpiresAtUtc);
