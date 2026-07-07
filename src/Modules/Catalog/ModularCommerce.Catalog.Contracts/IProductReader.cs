namespace ModularCommerce.Catalog.Contracts;
public interface IProductReader
{
    Task<IReadOnlyList<ProductSnapshotDto>> GetByIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken);
}
