using ModularCommerce.Cart.Domain.Carts;

namespace ModularCommerce.Cart.Infrastructure.Persistence;

public sealed record CartDocument(Guid CustomerId, List<CartItemDocument> Items)
{
    public static CartDocument FromCart(Domain.Carts.Cart cart)
        => new(
            cart.CustomerId,
            [.. cart.Items.Select(i => new CartItemDocument(i.ProductId, i.Quantity, i.AddedAtUtc))]);

    public Domain.Carts.Cart ToCart()
        => Domain.Carts.Cart.Rehydrate(
            CustomerId,
            Items.Select(i => new CartItem(i.ProductId, i.Quantity, i.AddedAtUtc)));
}

public sealed record CartItemDocument(Guid ProductId, int Quantity, DateTime AddedAtUtc);
