namespace ModularCommerce.Inventory.Domain.Stock;

public enum ReservationStatus
{
    Active = 0,
    Committed = 1,
    Released = 2,
    Expired = 3,
}
