namespace ModularCommerce.Catalog.Domain.Products;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void AddRange(IEnumerable<Product> products);

    /// <summary>Seed idempotansı için: hiç ürün var mı?</summary>
    Task<bool> AnyAsync(CancellationToken cancellationToken);

    /// <summary>Yeni ürünü ekler ve kaydeder (ProductCreated outbox'a yazılır).</summary>
    Task AddAsync(Product product, CancellationToken cancellationToken);

    /// <summary>Takip edilen ürünü kaydeder (ProductUpdated outbox'a yazılır) ve okuma cache'ini bayatlatır.</summary>
    Task UpdateAsync(Product product, CancellationToken cancellationToken);
}
