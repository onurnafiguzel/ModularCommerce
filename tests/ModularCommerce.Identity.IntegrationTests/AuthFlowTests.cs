using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ModularCommerce.Identity.Application.Auth.Login;
using ModularCommerce.Identity.Application.Auth.Logout;
using ModularCommerce.Identity.Application.Auth.Refresh;
using ModularCommerce.Identity.Application.Auth.Signup;
using ModularCommerce.Identity.Domain.Users;
using ModularCommerce.Identity.Infrastructure.Persistence;
using ModularCommerce.Identity.Infrastructure.Persistence.Repositories;
using ModularCommerce.Identity.Infrastructure.Security;
using ModularCommerce.Identity.IntegrationTests.Fixtures;
using ModularCommerce.Shared.Infrastructure.Auth;
using Xunit;

namespace ModularCommerce.Identity.IntegrationTests;

/// <summary>
/// Gerçek Postgres + gerçek hasher + gerçek JwtTokenService ile handler seviyesi
/// uçtan uca akış (WebApplicationFactory YOK — repo deseni). HTTP katmanının
/// kanıtı T5/T9 manuel akışı + k6 smoke'tadır.
/// </summary>
[Collection("IdentityPostgres")]
public sealed class AuthFlowTests(PostgresContainerFixture fixture)
{
    private static readonly IdentityPasswordHasher Hasher = new();

    private static readonly JwtTokenService TokenService = new(
        Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Issuer = "ModularCommerce.IntegrationTest",
            Audience = "ModularCommerce.IntegrationTest",
            SigningKey = "integration-test-signing-key-32-krkter!",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
        }));

    private static string UniqueEmail() => $"kanit-{Guid.NewGuid():N}@example.com";

    private SignupHandler CreateSignupHandler(IdentityDbContext context)
        => new(new UserRepository(context), Hasher, new SignupCommandValidator());

    private LoginHandler CreateLoginHandler(IdentityDbContext context)
        => new(
            new UserRepository(context),
            new RefreshTokenRepository(context),
            Hasher,
            TokenService,
            new LoginCommandValidator());

    [Fact(DisplayName = "Migration uygulanır: identity şemasında users + refresh_tokens sorgulanabilir")]
    public async Task Migration_CreatesIdentitySchema()
    {
        await using var context = fixture.CreateContext();

        var applied = await context.Database.GetAppliedMigrationsAsync();

        applied.Should().NotBeEmpty();
        (await context.Users.AnyAsync(u => false)).Should().BeFalse("users tablosu sorgulanabilir olmalı");
        (await context.RefreshTokens.AnyAsync(t => false)).Should().BeFalse("refresh_tokens tablosu sorgulanabilir olmalı");
    }

    [Fact(DisplayName = "Check-then-insert yarışı: unique index 23505'i EmailAlreadyExists/409'a çevirir (FR-1.5)")]
    public async Task DuplicateEmailRace_IsCaughtByUniqueIndex()
    {
        var email = Email.Create(UniqueEmail()).Value;

        // İki ayrı scope AYNI e-postayla kullanıcı hazırlar — ikisi de ön kontrolü
        // geçmiş gibi doğrudan Add + Save yapar (yarışın en kötü senaryosu).
        await using var firstContext = fixture.CreateContext();
        var firstRepo = new UserRepository(firstContext);
        firstRepo.Add(User.Create(email, Hasher.Hash("gizli-sifre-123")).Value);

        await using var secondContext = fixture.CreateContext();
        var secondRepo = new UserRepository(secondContext);
        secondRepo.Add(User.Create(email, Hasher.Hash("gizli-sifre-123")).Value);

        var firstSave = await firstRepo.SaveChangesAsync(CancellationToken.None);
        var secondSave = await secondRepo.SaveChangesAsync(CancellationToken.None);

        firstSave.IsSuccess.Should().BeTrue();
        secondSave.IsFailure.Should().BeTrue();
        secondSave.Error.Should().Be(IdentityErrors.EmailAlreadyExists);
    }

    [Fact(DisplayName = "Uçtan uca akış: signup → login → refresh (rotasyon: eski token geçersizleşir) → logout")]
    public async Task FullAuthFlow_WorksAgainstRealDatabase()
    {
        var email = UniqueEmail();

        // Signup
        await using (var context = fixture.CreateContext())
        {
            var signup = await CreateSignupHandler(context).HandleAsync(
                new SignupCommand(email, "gizli-sifre-123"), CancellationToken.None);
            signup.IsSuccess.Should().BeTrue();
        }

        // Login → token çifti
        string refreshToken;
        await using (var context = fixture.CreateContext())
        {
            var login = await CreateLoginHandler(context).HandleAsync(
                new LoginCommand(email, "gizli-sifre-123"), CancellationToken.None);
            login.IsSuccess.Should().BeTrue();
            login.Value.AccessToken.Should().NotBeNullOrWhiteSpace();
            refreshToken = login.Value.RefreshToken;
        }

        // Yanlış şifre → InvalidCredentials
        await using (var context = fixture.CreateContext())
        {
            var wrongLogin = await CreateLoginHandler(context).HandleAsync(
                new LoginCommand(email, "yanlis-sifre"), CancellationToken.None);
            wrongLogin.IsFailure.Should().BeTrue();
            wrongLogin.Error.Should().Be(IdentityErrors.InvalidCredentials);
        }

        // Refresh → rotasyon
        string rotatedToken;
        await using (var context = fixture.CreateContext())
        {
            var refresh = await CreateRefreshHandler(context).HandleAsync(
                new RefreshCommand(refreshToken), CancellationToken.None);
            refresh.IsSuccess.Should().BeTrue();
            rotatedToken = refresh.Value.RefreshToken;
            rotatedToken.Should().NotBe(refreshToken);
        }

        // ESKİ refresh token ikinci kez kullanılamaz (rotasyon kanıtı)
        await using (var context = fixture.CreateContext())
        {
            var replay = await CreateRefreshHandler(context).HandleAsync(
                new RefreshCommand(refreshToken), CancellationToken.None);
            replay.IsFailure.Should().BeTrue();
            replay.Error.Should().Be(IdentityErrors.RefreshTokenInvalid);
        }

        // Logout → yeni token da geçersizleşir
        Guid userId;
        await using (var context = fixture.CreateContext())
        {
            var user = await new UserRepository(context)
                .GetByEmailAsync(Email.Create(email).Value, CancellationToken.None);
            userId = user!.Id;

            var logout = await CreateLogoutHandler(context).HandleAsync(
                new LogoutCommand(rotatedToken, userId), CancellationToken.None);
            logout.IsSuccess.Should().BeTrue();
        }

        await using (var context = fixture.CreateContext())
        {
            var afterLogout = await CreateRefreshHandler(context).HandleAsync(
                new RefreshCommand(rotatedToken), CancellationToken.None);
            afterLogout.IsFailure.Should().BeTrue();
            afterLogout.Error.Should().Be(IdentityErrors.RefreshTokenInvalid);
        }

        // Logout idempotent: aynı token'la ikinci logout da 204 (Success)
        await using (var lastContext = fixture.CreateContext())
        {
            var secondLogout = await CreateLogoutHandler(lastContext).HandleAsync(
                new LogoutCommand(rotatedToken, userId), CancellationToken.None);
            secondLogout.IsSuccess.Should().BeTrue();
        }
    }

    private RefreshHandler CreateRefreshHandler(IdentityDbContext context)
        => new(
            new UserRepository(context),
            new RefreshTokenRepository(context),
            TokenService,
            new RefreshCommandValidator());

    private LogoutHandler CreateLogoutHandler(IdentityDbContext context)
        => new(
            new RefreshTokenRepository(context),
            TokenService,
            new LogoutCommandValidator());
}
