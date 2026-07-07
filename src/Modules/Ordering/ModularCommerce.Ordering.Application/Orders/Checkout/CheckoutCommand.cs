namespace ModularCommerce.Ordering.Application.Orders.Checkout;
public sealed record CheckoutCommand(Guid CustomerId, string IdempotencyKey);
