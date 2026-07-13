using FluentAssertions;
using ModularCommerce.Inventory.Domain.Stock;
using Xunit;

namespace ModularCommerce.Inventory.UnitTests.Domain;

/// <summary>
/// retryable-409 istemci sözleşmesinin domain seviyesindeki kanıtı: geçici çakışmalar
/// (ConcurrencyConflict/LockTimeout) AYNI key ile tekrar denenmeli → Retryable=true; kalıcı
/// iş reddi (InsufficientStock) yeni denemede de aynı sonucu verir → Retryable=false.
/// </summary>
public sealed class InventoryErrorContractTests
{
    [Fact(DisplayName = "Geçici stok çakışmaları retryable işaretlidir (aynı key ile tekrar)")]
    public void TransientConflicts_AreRetryable()
    {
        InventoryErrors.ConcurrencyConflict.Retryable.Should().BeTrue();
        InventoryErrors.LockTimeout.Retryable.Should().BeTrue();
    }

    [Fact(DisplayName = "InsufficientStock retryable DEĞİLDİR (kalıcı iş reddi)")]
    public void InsufficientStock_IsNotRetryable()
        => InventoryErrors.InsufficientStock.Retryable.Should().BeFalse();
}
