namespace ModularCommerce.Catalog.Domain.Products;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void AddRange(IEnumerable<Product> products);

    /// <summary>Seed idempotansı için: hiç ürün var mı?</summary>
    Task<bool> AnyAsync(CancellationToken cancellationToken);
}
