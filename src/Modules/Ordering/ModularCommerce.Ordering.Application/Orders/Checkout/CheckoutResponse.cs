using ModularCommerce.Ordering.Application.Orders.Common;

namespace ModularCommerce.Ordering.Application.Orders.Checkout;
public sealed record CheckoutResponse(OrderResponse Order, bool IsExisting);