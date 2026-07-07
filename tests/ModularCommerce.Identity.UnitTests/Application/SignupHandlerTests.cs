using FluentAssertions;
using ModularCommerce.Identity.Application.Abstractions;
using ModularCommerce.Identity.Application.Auth.Signup;
using ModularCommerce.Identity.Domain.Users;
using ModularCommerce.Shared.Kernel;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Identity.UnitTests.Application;

public class SignupHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly SignupHandler _handler;

    public SignupHandlerTests()
    {
        _users.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
        _hasher.Hash(Arg.Any<string>()).Returns("hash-degeri");
        _handler = new SignupHandler(_users, _hasher, new SignupCommandValidator());
    }

    [Fact(DisplayName = "Geçerli kayıt: kullanıcı eklenir, e-posta normalize döner (FR-1.1)")]
    public async Task Handle_WithValidCommand_CreatesUser()
    {
        var result = await _handler.HandleAsync(
            new SignupCommand("  Ali@Example.COM ", "gizli-sifre-123"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("ali@example.com");
        _users.Received(1).Add(Arg.Is<User>(u => u.PasswordHash == "hash-degeri"));
        await _users.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Aynı e-posta ikinci kez kayıt olamaz (FR-1.5)")]
    public async Task Handle_WithExistingEmail_ReturnsConflict()
    {
        var email = Email.Create("ali@example.com").Value;
        _users.GetByEmailAsync(email, Arg.Any<CancellationToken>())
            .Returns(User.Create(email, "mevcut-hash").Value);

        var result = await _handler.HandleAsync(
            new SignupCommand("ali@example.com", "gizli-sifre-123"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.EmailAlreadyExists);
        _users.DidNotReceive().Add(Arg.Any<User>());
    }

    [Fact(DisplayName = "Check-then-insert yarışı: SaveChanges'in 23505 çevirimi aynen iletilir")]
    public async Task Handle_WhenSaveDetectsRace_PropagatesConflict()
    {
        _users.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure(IdentityErrors.EmailAlreadyExists));

        var result = await _handler.HandleAsync(
            new SignupCommand("ali@example.com", "gizli-sifre-123"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.EmailAlreadyExists);
    }

    [Theory(DisplayName = "Kısa/boş şifre istek-şekli doğrulamasına takılır")]
    [InlineData("")]
    [InlineData("kisa")]
    [InlineData("1234567")]
    public async Task Handle_WithWeakPassword_ReturnsValidationError(string password)
    {
        var result = await _handler.HandleAsync(
            new SignupCommand("ali@example.com", password), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        _users.DidNotReceive().Add(Arg.Any<User>());
    }

    [Fact(DisplayName = "Biçimsiz e-posta domain'de reddedilir")]
    public async Task Handle_WithMalformedEmail_ReturnsValidationError()
    {
        var result = await _handler.HandleAsync(
            new SignupCommand("bicimsiz-eposta", "gizli-sifre-123"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.InvalidEmail);
    }
}
