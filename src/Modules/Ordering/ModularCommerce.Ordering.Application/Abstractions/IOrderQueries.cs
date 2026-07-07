using ModularCommerce.Ordering.Application.Orders.Common;

namespace ModularCommerce.Ordering.Application.Abstractions;
public interface IOrderQueries
{
    Task<IReadOnlyList<OrderSummaryResponse>> GetMyOrdersAsync(
        Guid customerId,
        CancellationToken cancellationToken);
}
