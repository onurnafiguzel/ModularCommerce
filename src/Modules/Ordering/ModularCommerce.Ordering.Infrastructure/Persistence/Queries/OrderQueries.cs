using Microsoft.EntityFrameworkCore;
using ModularCommerce.Ordering.Application.Abstractions;
using ModularCommerce.Ordering.Application.Orders.Common;

namespace ModularCommerce.Ordering.Infrastructure.Persistence.Queries;

public sealed class OrderQueries(OrderingDbContext context) : IOrderQueries
{
    private const int MaxResults = 20;

    public async Task<IReadOnlyList<OrderSummaryResponse>> GetMyOrdersAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        // Owned koleksiyonlar sahiple birlikte yüklenir; 20 sipariş için bellek
        // içinde özetlemek tek karmaşık SQL projeksiyonundan daha okunur ve yeterli.
        var orders = await context.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Take(MaxResults)
            .ToListAsync(cancellationToken);

        return [.. orders.Select(o => new OrderSummaryResponse(
            o.Id,
            o.Status.ToString(),
            o.TotalAmount,
            o.Currency,
            o.Lines.Count,
            o.CreatedAtUtc))];
    }
}
