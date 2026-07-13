namespace ModularCommerce.Shared.Infrastructure.RateLimiting;
public sealed record RateLimitingOptions
{
    public const string SectionName = "RateLimiting";
    public WindowLimit Global { get; init; } = new() { PermitLimit = 300, WindowSeconds = 10 };
    public WindowLimit Auth { get; init; } = new() { PermitLimit = 10, WindowSeconds = 60 };
    public CheckoutLimit Checkout { get; init; } = new() { PermitLimit = 120, QueueLimit = 120, WindowSeconds = 10 };

    public sealed record WindowLimit
    {
        public int PermitLimit { get; init; }
        public int WindowSeconds { get; init; }
    }

    public sealed record CheckoutLimit
    {
        public int PermitLimit { get; init; }
        public int QueueLimit { get; init; }
        public int WindowSeconds { get; init; }
    }
}
