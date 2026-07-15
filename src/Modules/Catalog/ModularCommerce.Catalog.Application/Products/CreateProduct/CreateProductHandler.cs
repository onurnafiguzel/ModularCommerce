using FluentValidation;
using ModularCommerce.Catalog.Domain.Products;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Catalog.Application.Products.CreateProduct;

public sealed class CreateProductHandler(
    IProductRepository repository,
    IValidator<CreateProductCommand> validator)
{
    public async Task<Result<Guid>> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<Guid>(Error.Validation(
                "Catalog.Products.InvalidCommand",
                string.Join(" ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var price = Money.Create(command.Price, command.Currency.ToUpperInvariant());
        if (price.IsFailure)
        {
            return Result.Failure<Guid>(price.Error);
        }

        var product = Product.Create(command.Name, command.Description, command.Sku, price.Value, command.StockQuantity);
        if (product.IsFailure)
        {
            return Result.Failure<Guid>(product.Error);
        }

        // AddAsync ProductCreated'ı (interceptor ile) outbox'a atomik yazar → Discovery indeksler.
        await repository.AddAsync(product.Value, cancellationToken);

        return Result.Success(product.Value.Id);
    }
}
