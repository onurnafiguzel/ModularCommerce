namespace ModularCommerce.Cart.Domain.Carts;
public sealed record CartItem(Guid ProductId, int Quantity, DateTime AddedAtUtc);
