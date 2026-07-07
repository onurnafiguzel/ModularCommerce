using FluentAssertions;
using ModularCommerce.Identity.Application.Abstractions;
using ModularCommerce.Identity.Application.Auth.Logout;
using ModularCommerce.Identity.Domain.Users;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Identity.UnitTests.Application;

public class LogoutHandlerTests
{
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly LogoutHandler _handler;

    public LogoutHandlerTests()
    {
        _tokenService.HashRefreshTokenValue("ham-deger").Returns("token-hash");
        _handler = new LogoutHandler(_refreshTokens, _tokenService, new LogoutCommandValidator());
    }

    [Fact(DisplayName = "Kendi token'ı iptal edilir ve kaydedilir (FR-1.3)")]
    public async Task Handle_WithOwnToken_RevokesIt()
    {
        var userId = Guid.NewGuid();
        var token = RefreshToken.Create(userId, "token-hash", DateTime.UtcNow.AddDays(7)).Value;
        _refreshTokens.GetByTokenHashAsync("token-hash", Arg.Any<CancellationToken>()).Returns(token);

        var result = await _handler.HandleAsync(
            new LogoutCommand("ham-deger", userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        token.RevokedAtUtc.Should().NotBeNull();
        await _refreshTokens.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Bulunmayan token'da da 204: logout idempotenttir, bilgi sızdırmaz")]
    public async Task Handle_WithUnknownToken_StillSucceeds()
    {
        var result = await _handler.HandleAsync(
            new LogoutCommand("ham-deger", Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _refreshTokens.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Başkasının token'ı İPTAL EDİLMEZ ama yanıt yine başarılıdır (sahiplik kontrolü)")]
    public async Task Handle_WithSomeoneElsesToken_DoesNotRevoke()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "token-hash", DateTime.UtcNow.AddDays(7)).Value;
        _refreshTokens.GetByTokenHashAsync("token-hash", Arg.Any<CancellationToken>()).Returns(token);

        var result = await _handler.HandleAsync(
            new LogoutCommand("ham-deger", Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        token.RevokedAtUtc.Should().BeNull("başkasının token'ına dokunulmaz");
        await _refreshTokens.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
