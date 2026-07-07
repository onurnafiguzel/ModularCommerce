namespace ModularCommerce.Cart.Application.Carts.Common;

public sealed record CartResponse(
    Guid CustomerId,
    IReadOnlyList<CartItemResponse> Items,
    string Warning)
{
    public const string ReservationWarning =
        "Sepete eklemek rezervasyon değildir; stok ve fiyat checkout'ta yeniden doğrulanır.";

    public static CartResponse FromCart(Domain.Carts.Cart cart)
        => new(
            cart.CustomerId,
            [.. cart.Items.Select(i => new CartItemResponse(i.ProductId, i.Quantity, i.AddedAtUtc))],
            ReservationWarning);

    public static CartResponse Empty(Guid customerId)
        => new(customerId, [], ReservationWarning);
}

public sealed record CartItemResponse(Guid ProductId, int Quantity, DateTime AddedAtUtc);
