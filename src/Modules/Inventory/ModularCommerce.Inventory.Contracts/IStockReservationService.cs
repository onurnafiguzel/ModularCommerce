using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Inventory.Contracts;
public interface IStockReservationService
{
    Task<Result<StockReservationDto>> ReserveAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken);
    Task<Result> ReleaseAsync(
        Guid reservationId, 
        CancellationToken cancellationToken);
    Task<Result> CommitAsync(
        Guid reservationId, 
        CancellationToken cancellationToken);
    Task<Result> ExpireAsync(
        Guid reservationId, 
        CancellationToken cancellationToken);
    Task<Result> ReturnAsync(
        Guid reservationId, 
        CancellationToken cancellationToken);
}
