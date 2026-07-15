using FluentValidation;
using ModularCommerce.Catalog.Domain.Products;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Catalog.Application.Products.UpdateProduct;

public sealed class UpdateProductHandler(
    IProductRepository repository,
    IValidator<UpdateProductCommand> validator)
{
    public async Task<Result> HandleAsync(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(Error.Validation(
                "Catalog.Products.InvalidCommand",
                string.Join(" ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var product = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (product is null)
        {
            return Result.Failure(ProductErrors.NotFound(command.Id));
        }

        var price = Money.Create(command.Price, command.Currency.ToUpperInvariant());
        if (price.IsFailure)
        {
            return Result.Failure(price.Error);
        }

        var update = product.Update(command.Name, command.Description, price.Value, command.IsActive);
        if (update.IsFailure)
        {
            return update;
        }

        // UpdateAsync ProductUpdated'ı outbox'a yazar (yeniden indeksleme) + okuma cache'ini bayatlatır.
        await repository.UpdateAsync(product, cancellationToken);

        return Result.Success();
    }
}
