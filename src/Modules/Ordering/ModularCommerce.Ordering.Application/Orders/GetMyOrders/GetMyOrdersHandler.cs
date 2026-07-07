using ModularCommerce.Ordering.Application.Abstractions;
using ModularCommerce.Ordering.Application.Orders.Common;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Ordering.Application.Orders.GetMyOrders;
public sealed class GetMyOrdersHandler(IOrderQueries queries)
{
    public async Task<Result<IReadOnlyList<OrderSummaryResponse>>> HandleAsync(
        Guid customerId,
        CancellationToken cancellationToken)
        => Result.Success(await queries.GetMyOrdersAsync(customerId, cancellationToken));
}
