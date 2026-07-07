using FluentAssertions;
using ModularCommerce.Identity.Application.Abstractions;
using ModularCommerce.Identity.Application.Auth.Refresh;
using ModularCommerce.Identity.Domain.Users;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Identity.UnitTests.Application;

public class RefreshHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly RefreshHandler _handler;

    public RefreshHandlerTests()
    {
        _tokenService.HashRefreshTokenValue("ham-deger").Returns("eski-hash");
        _tokenService.GenerateAccessToken(Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(new AccessTokenResult("yeni-access", DateTime.UtcNow.AddMinutes(15)));
        _tokenService.GenerateRefreshToken()
            .Returns(new RefreshTokenResult("yeni-ham", "yeni-hash", DateTime.UtcNow.AddDays(7)));

        _handler = new RefreshHandler(
            _users, _refreshTokens, _tokenService, new RefreshCommandValidator());
    }

    private (User User, RefreshToken Token) SetupActiveToken()
    {
        var user = User.Create(Email.Create("ali@example.com").Value, "hash").Value;
        var token = RefreshToken.Create(user.Id, "eski-hash", DateTime.UtcNow.AddDays(7)).Value;

        _refreshTokens.GetByTokenHashAsync("eski-hash", Arg.Any<CancellationToken>()).Returns(token);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        return (user, token);
    }

    [Fact(DisplayName = "Rotasyon (FR-1.3): eski token iptal edilir, yeni token eklenir, yeni değerler döner")]
    public async Task Handle_WithActiveToken_RotatesToken()
    {
        var (user, oldToken) = SetupActiveToken();

        var result = await _handler.HandleAsync(new RefreshCommand("ham-deger"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("yeni-access");
        result.Value.RefreshToken.Should().Be("yeni-ham");
        oldToken.RevokedAtUtc.Should().NotBeNull("eski token rotasyonla iptal edilmeli");
        _refreshTokens.Received(1).Add(Arg.Is<RefreshToken>(t =>
            t.UserId == user.Id && t.TokenHash == "yeni-hash"));
        await _refreshTokens.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Bilinmeyen token → RefreshTokenInvalid (401)")]
    public async Task Handle_WithUnknownToken_ReturnsInvalid()
    {
        var result = await _handler.HandleAsync(new RefreshCommand("ham-deger"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.RefreshTokenInvalid);
    }

    [Fact(DisplayName = "İptal edilmiş token AYNI hatayı döner (durum ayrıntısı sızmaz)")]
    public async Task Handle_WithRevokedToken_ReturnsSameError()
    {
        var (_, token) = SetupActiveToken();
        token.Revoke(DateTime.UtcNow);

        var result = await _handler.HandleAsync(new RefreshCommand("ham-deger"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.RefreshTokenInvalid);
        _refreshTokens.DidNotReceive().Add(Arg.Any<RefreshToken>());
    }

    [Fact(DisplayName = "Kullanıcısı silinmiş token da RefreshTokenInvalid döner")]
    public async Task Handle_WhenUserMissing_ReturnsInvalid()
    {
        var (user, _) = SetupActiveToken();
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await _handler.HandleAsync(new RefreshCommand("ham-deger"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.RefreshTokenInvalid);
    }
}
