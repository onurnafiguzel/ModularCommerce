using Microsoft.EntityFrameworkCore;
using ModularCommerce.Cart.Domain.Carts;
using ModularCommerce.Shared.Kernel;
using Npgsql;

namespace ModularCommerce.Cart.Infrastructure.Persistence;

public sealed class PostgresCartRepository(CartDbContext context) : ICartRepository
{
    public async Task<Result<Domain.Carts.Cart?>> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        try
        {
            var record = await context.Carts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

            if (record is null)
            {
                return Result.Success<Domain.Carts.Cart?>(null); // sepet yok = boş sepet
            }

            // Boş sepet jsonb'de null saklanabilir → null-güvenli.
            var cart = Domain.Carts.Cart.Rehydrate(
                record.CustomerId,
                (record.Items ?? []).Select(i => new CartItem(i.ProductId, i.Quantity, i.AddedAtUtc)));

            return Result.Success<Domain.Carts.Cart?>(cart);
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            return Result.Failure<Domain.Carts.Cart?>(CartErrors.StorageUnavailable);
        }
    }

    public async Task<Result> SaveAsync(Domain.Carts.Cart cart, CancellationToken cancellationToken)
    {
        try
        {
            var items = cart.Items
                .Select(i => new CartItemRecord { ProductId = i.ProductId, Quantity = i.Quantity, AddedAtUtc = i.AddedAtUtc })
                .ToList();

            var record = await context.Carts
                .FirstOrDefaultAsync(c => c.CustomerId == cart.CustomerId, cancellationToken);

            if (record is null)
            {
                context.Carts.Add(new CartRecord
                {
                    CustomerId = cart.CustomerId,
                    Items = items,
                    UpdatedAtUtc = DateTime.UtcNow,
                });
            }
            else
            {
                record.Items = items;
                record.UpdatedAtUtc = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            return Result.Failure(CartErrors.StorageUnavailable);
        }
    }

    public async Task<Result> RemoveAsync(Guid customerId, CancellationToken cancellationToken)
    {
        try
        {
            var record = await context.Carts
                .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

            if (record is not null)
            {
                context.Carts.Remove(record);
                await context.SaveChangesAsync(cancellationToken);
            }

            return Result.Success();
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            return Result.Failure(CartErrors.StorageUnavailable);
        }
    }

    // Bağlantı hatası doğrudan NpgsqlException; SaveChanges'te DbUpdateException'a sarılabilir.
    private static bool IsDatabaseUnavailable(Exception ex)
        => ex is NpgsqlException || ex.InnerException is NpgsqlException;
}
