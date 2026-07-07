using ModularCommerce.Ordering.Application.Orders.Common;
using ModularCommerce.Ordering.Domain.Orders;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Ordering.Application.Orders.GetOrder;

public sealed class GetOrderHandler(IOrderRepository orders)
{
    public async Task<Result<OrderResponse>> HandleAsync(
        Guid orderId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(orderId, cancellationToken);

        if (order is null || order.CustomerId != customerId)
        {
            return Result.Failure<OrderResponse>(OrderErrors.NotFound(orderId));
        }

        return Result.Success(OrderResponse.FromOrder(order));
    }
}
