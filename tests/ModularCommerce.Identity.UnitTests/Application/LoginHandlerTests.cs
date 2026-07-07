using FluentAssertions;
using ModularCommerce.Identity.Application.Abstractions;
using ModularCommerce.Identity.Application.Auth.Login;
using ModularCommerce.Identity.Domain.Users;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Identity.UnitTests.Application;

public class LoginHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly LoginHandler _handler;

    private static readonly Email KnownEmail = Email.Create("ali@example.com").Value;

    public LoginHandlerTests()
    {
        _hasher.DummyHash.Returns("sahte-hash");
        _tokenService.GenerateAccessToken(Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(new AccessTokenResult("access-token", DateTime.UtcNow.AddMinutes(15)));
        _tokenService.GenerateRefreshToken()
            .Returns(new RefreshTokenResult("ham-deger", "token-hash", DateTime.UtcNow.AddDays(7)));

        _handler = new LoginHandler(
            _users, _refreshTokens, _hasher, _tokenService, new LoginCommandValidator());
    }

    private User SetupKnownUser()
    {
        var user = User.Create(KnownEmail, "gercek-hash").Value;
        _users.GetByEmailAsync(KnownEmail, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    [Fact(DisplayName = "Doğru kimlik bilgisi: access + refresh token döner, refresh DB'ye hash'iyle yazılır (FR-1.2)")]
    public async Task Handle_WithValidCredentials_ReturnsTokens()
    {
        var user = SetupKnownUser();
        _hasher.Verify("gercek-hash", "dogru-sifre").Returns(true);

        var result = await _handler.HandleAsync(
            new LoginCommand("ali@example.com", "dogru-sifre"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().Be("ham-deger");
        _refreshTokens.Received(1).Add(Arg.Is<RefreshToken>(t =>
            t.UserId == user.Id && t.TokenHash == "token-hash"));
        await _refreshTokens.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Yanlış şifre → InvalidCredentials (401)")]
    public async Task Handle_WithWrongPassword_ReturnsInvalidCredentials()
    {
        SetupKnownUser();
        _hasher.Verify("gercek-hash", "yanlis-sifre").Returns(false);

        var result = await _handler.HandleAsync(
            new LoginCommand("ali@example.com", "yanlis-sifre"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.InvalidCredentials);
    }

    [Fact(DisplayName = "Olmayan e-posta AYNI hatayı döner ve sahte hash yine doğrulanır (timing-attack önlemi)")]
    public async Task Handle_WithUnknownEmail_ReturnsSameErrorAndRunsDummyVerify()
    {
        var result = await _handler.HandleAsync(
            new LoginCommand("yok@example.com", "herhangi-sifre"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.InvalidCredentials);
        _hasher.Received(1).Verify("sahte-hash", "herhangi-sifre");
        _refreshTokens.DidNotReceive().Add(Arg.Any<RefreshToken>());
    }

    [Fact(DisplayName = "Biçimsiz e-posta da InvalidCredentials döner (ayrıntı sızmaz)")]
    public async Task Handle_WithMalformedEmail_ReturnsInvalidCredentials()
    {
        var result = await _handler.HandleAsync(
            new LoginCommand("bicimsiz", "herhangi-sifre"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.InvalidCredentials);
    }
}
