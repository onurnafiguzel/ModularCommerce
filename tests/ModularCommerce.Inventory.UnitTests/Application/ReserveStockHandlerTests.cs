using FluentAssertions;
using FluentValidation;
using ModularCommerce.Inventory.Application.Abstractions;
using ModularCommerce.Inventory.Application.Reservations.ReserveStock;
using ModularCommerce.Inventory.Domain.Stock;
using ModularCommerce.Shared.Kernel;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Inventory.UnitTests.Application;

/// <summary>Rezervasyon use case'i: biçim doğrulaması + stratejiye delegasyon, iş kuralı yok.</summary>
public class ReserveStockHandlerTests
{
    private readonly IReservationStrategy _strategy = Substitute.For<IReservationStrategy>();
    private readonly IValidator<ReserveStockCommand> _validator = new ReserveStockCommandValidator();

    private ReserveStockHandler CreateHandler() => new(_strategy, _validator);

    [Theory(DisplayName = "Geçersiz komut Validation hatası döner, strateji hiç çağrılmaz")]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task HandleAsync_WithInvalidQuantity_ReturnsValidationError(int quantity)
    {
        var result = await CreateHandler().HandleAsync(
            new ReserveStockCommand(Guid.NewGuid(), quantity), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        await _strategy.DidNotReceiveWithAnyArgs().ReserveAsync(default, default, default);
    }

    [Fact(DisplayName = "Boş ürün kimliği Validation hatası döner")]
    public async Task HandleAsync_WithEmptyProductId_ReturnsValidationError()
    {
        var result = await CreateHandler().HandleAsync(
            new ReserveStockCommand(Guid.Empty, 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact(DisplayName = "Strateji hatası (örn. ConcurrencyConflict) aynen iletilir")]
    public async Task HandleAsync_WhenStrategyFails_PropagatesError()
    {
        var command = new ReserveStockCommand(Guid.NewGuid(), 2);
        _strategy.ReserveAsync(command.ProductId, 2, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<Reservation>(InventoryErrors.ConcurrencyConflict));

        var result = await CreateHandler().HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.ConcurrencyConflict);
    }

    [Fact(DisplayName = "Başarıda rezervasyon DTO'ya doğru eşlenir")]
    public async Task HandleAsync_OnSuccess_MapsToResponse()
    {
        var stockItem = StockItem.Create(Guid.NewGuid(), 10).Value;
        var reservation = stockItem.Reserve(3).Value;
        var command = new ReserveStockCommand(stockItem.ProductId, 3);
        _strategy.ReserveAsync(command.ProductId, 3, Arg.Any<CancellationToken>())
            .Returns(Result.Success(reservation));

        var result = await CreateHandler().HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReservationId.Should().Be(reservation.Id);
        result.Value.ProductId.Should().Be(stockItem.ProductId);
        result.Value.Quantity.Should().Be(3);
        result.Value.Status.Should().Be("Active");
        result.Value.ExpiresAtUtc.Should().Be(reservation.ExpiresAtUtc);
    }
}
