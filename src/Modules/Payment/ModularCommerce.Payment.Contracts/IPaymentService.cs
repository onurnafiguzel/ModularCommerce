using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Payment.Contracts;
public interface IPaymentService
{
    Task<Result<PaymentResultDto>> ChargeAsync(ChargeRequest request, CancellationToken cancellationToken);
    Task<Result<RefundResultDto>> RefundAsync(RefundRequest request, CancellationToken cancellationToken);
}
