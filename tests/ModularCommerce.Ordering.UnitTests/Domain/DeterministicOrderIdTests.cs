using FluentAssertions;
using ModularCommerce.Ordering.Domain.Orders;
using Xunit;

namespace ModularCommerce.Ordering.UnitTests.Domain;

/// <summary>
/// RFC 9562 §5.5 (UUIDv5) uygulamasının byte-sırası/hash adımlarının doğruluğunu, RFC'nin
/// KENDİ yayımladığı bilinen test vektörüyle kanıtlar — byte-swap hatası (en sık yapılan hata)
/// bu test geçmeden fark edilmeden kalamaz.
/// </summary>
public sealed class DeterministicOrderIdTests
{
    [Fact(DisplayName = "RFC 9562 bilinen test vektörü: DNS namespace + \"www.example.com\" → sabit UUIDv5")]
    public void CreateVersion5_MatchesRfc9562KnownTestVector()
    {
        var dnsNamespace = Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

        var result = DeterministicOrderId.CreateVersion5(dnsNamespace, "www.example.com");

        result.Should().Be(Guid.Parse("2ed6657d-e927-568b-95e1-2665a8aea6a2"));
    }

    [Fact(DisplayName = "Aynı (CustomerId, IdempotencyKey) → her zaman AYNI Order.Id (determinizm)")]
    public void Create_WithSameInputs_IsDeterministic()
    {
        var customerId = Guid.NewGuid();
        const string key = "checkout-abc-123";

        var first = DeterministicOrderId.Create(customerId, key);
        var second = DeterministicOrderId.Create(customerId, key);

        second.Should().Be(first);
    }

    [Fact(DisplayName = "FARKLI IdempotencyKey (büyük/küçük harf dahil) → FARKLI Order.Id")]
    public void Create_WithDifferentKeyCasing_ProducesDifferentId()
    {
        var customerId = Guid.NewGuid();

        var lower = DeterministicOrderId.Create(customerId, "abc");
        var upper = DeterministicOrderId.Create(customerId, "ABC");

        lower.Should().NotBe(upper, "key karşılaştırması büyük/küçük harfe duyarlı olmalı, normalize edilmemeli");
    }

    [Fact(DisplayName = "FARKLI CustomerId, AYNI IdempotencyKey → FARKLI Order.Id")]
    public void Create_WithDifferentCustomerId_ProducesDifferentId()
    {
        const string key = "same-key";

        var first = DeterministicOrderId.Create(Guid.NewGuid(), key);
        var second = DeterministicOrderId.Create(Guid.NewGuid(), key);

        second.Should().NotBe(first);
    }
}
