using FluentValidation;
using ModularCommerce.Ordering.Domain.Orders;

namespace ModularCommerce.Ordering.Application.Orders.Checkout;
public sealed class CheckoutCommandValidator : AbstractValidator<CheckoutCommand>
{
    public CheckoutCommandValidator()
    {
        RuleFor(c => c.CustomerId)
            .NotEmpty()
            .WithMessage("Müşteri kimliği boş olamaz.");

        RuleFor(c => c.IdempotencyKey)
            .NotEmpty()
            .WithMessage("Idempotency-Key header'ı zorunludur.")
            .MaximumLength(Order.IdempotencyKeyMaxLength)
            .WithMessage($"Idempotency-Key en fazla {Order.IdempotencyKeyMaxLength} karakter olabilir.");
    }
}
