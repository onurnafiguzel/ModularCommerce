namespace ModularCommerce.Cart.Application.Carts.AddItem;

public sealed record AddItemCommand(Guid CustomerId, Guid ProductId, int Quantity);
