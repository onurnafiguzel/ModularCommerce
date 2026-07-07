namespace ModularCommerce.Cart.Application.Carts.UpdateItemQuantity;
public sealed record UpdateItemQuantityCommand(Guid CustomerId, Guid ProductId, int Quantity);
