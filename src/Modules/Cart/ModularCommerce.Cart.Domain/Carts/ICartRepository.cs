using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Cart.Domain.Carts;

public interface ICartRepository
{
    /// <summary>Başarılı ve null: sepet hiç yok (boş sepet demektir).</summary>
    Task<Result<Cart?>> GetAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>Yazma TTL'i kaydırır (yazma-kaydırmalı 7 gün).</summary>
    Task<Result> SaveAsync(Cart cart, CancellationToken cancellationToken);

    Task<Result> RemoveAsync(Guid customerId, CancellationToken cancellationToken);
}
