namespace ModularCommerce.Cart.Infrastructure.Persistence;
public sealed class CartRecord
{
    public Guid CustomerId { get; set; }
    public List<CartItemRecord> Items { get; set; } = [];
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class CartItemRecord
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime AddedAtUtc { get; set; }
}
