using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ModularCommerce.Payment.Infrastructure.Persistence;

namespace ModularCommerce.Payment.Api.Endpoints;

public static class PaymentDevEndpoints
{
    /// <summary>
    /// Yalnız Development: K6 "tek charge" kanıtı bu endpoint'ten okunur (Inventory'nin
    /// dev-stok deseni). Production'da hiç map edilmez.
    /// </summary>
    public static void MapPaymentDevEndpoints(this IEndpointRouteBuilder group, bool isDevelopment)
    {
        if (!isDevelopment)
        {
            return;
        }

        group.MapGet("/dev/payments", async (
            Guid customerId,
            string key,
            PaymentDbContext context,
            CancellationToken cancellationToken) =>
        {
            var payments = await context.Payments
                .AsNoTracking()
                .Include(p => p.Attempts)
                .Where(p => p.CustomerId == customerId && p.IdempotencyKey == key)
                .Select(p => new
                {
                    p.Id,
                    Status = p.Status.ToString(),
                    p.Amount,
                    p.Currency,
                    p.PspTransactionId,
                    p.FailureCode,
                    AttemptCount = p.Attempts.Count,
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(new { count = payments.Count, payments });
        });
    }
}
