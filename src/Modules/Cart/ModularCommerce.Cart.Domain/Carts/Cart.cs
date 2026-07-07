using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Cart.Domain.Carts;

public sealed class Cart
{
    public const int MaxQuantityPerLine = 10;

    public const int MaxLines = 50;

    private readonly List<CartItem> _items;

    public Guid CustomerId { get; }
    public IReadOnlyList<CartItem> Items => _items;
    public bool IsEmpty => _items.Count == 0;

    private Cart(Guid customerId, List<CartItem> items)
    {
        CustomerId = customerId;
        _items = items;
    }

    public static Result<Cart> Create(Guid customerId)
    {
        if (customerId == Guid.Empty)
        {
            return Result.Failure<Cart>(CartErrors.InvalidCustomerId);
        }

        return Result.Success(new Cart(customerId, []));
    }

    public static Cart Rehydrate(Guid customerId, IEnumerable<CartItem> items)
        => new(customerId, [.. items]);

    public Result AddItem(Guid productId, int quantity)
    {
        if (productId == Guid.Empty)
        {
            return Result.Failure(CartErrors.InvalidProductId);
        }

        if (quantity < 1)
        {
            return Result.Failure(CartErrors.InvalidQuantity);
        }

        var index = _items.FindIndex(i => i.ProductId == productId);
        if (index >= 0)
        {
            var existing = _items[index];
            var total = existing.Quantity + quantity;

            if (total > MaxQuantityPerLine)
            {
                return Result.Failure(CartErrors.QuantityLimitExceeded);
            }

            _items[index] = existing with { Quantity = total };
            return Result.Success();
        }

        if (quantity > MaxQuantityPerLine)
        {
            return Result.Failure(CartErrors.QuantityLimitExceeded);
        }

        if (_items.Count >= MaxLines)
        {
            return Result.Failure(CartErrors.LineLimitExceeded);
        }

        _items.Add(new CartItem(productId, quantity, DateTime.UtcNow));
        return Result.Success();
    }

    public Result ChangeQuantity(Guid productId, int quantity)
    {
        if (quantity < 1)
        {
            return Result.Failure(CartErrors.InvalidQuantity);
        }

        if (quantity > MaxQuantityPerLine)
        {
            return Result.Failure(CartErrors.QuantityLimitExceeded);
        }

        var index = _items.FindIndex(i => i.ProductId == productId);
        if (index < 0)
        {
            return Result.Failure(CartErrors.ItemNotFound(productId));
        }

        _items[index] = _items[index] with { Quantity = quantity };
        return Result.Success();
    }

    public Result RemoveItem(Guid productId)
    {
        var removed = _items.RemoveAll(i => i.ProductId == productId);

        return removed > 0
            ? Result.Success()
            : Result.Failure(CartErrors.ItemNotFound(productId));
    }
}
