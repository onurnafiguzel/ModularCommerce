using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularCommerce.Identity.Api.Endpoints;
using ModularCommerce.Identity.Application.Abstractions;
using ModularCommerce.Identity.Application.Auth.Login;
using ModularCommerce.Identity.Application.Auth.Logout;
using ModularCommerce.Identity.Application.Auth.Refresh;
using ModularCommerce.Identity.Application.Auth.Signup;
using ModularCommerce.Identity.Domain.Users;
using ModularCommerce.Identity.Infrastructure.Persistence;
using ModularCommerce.Identity.Infrastructure.Persistence.Repositories;
using ModularCommerce.Identity.Infrastructure.Security;
using ModularCommerce.Shared.Infrastructure.Modules;
using ModularCommerce.Shared.Infrastructure.Persistence;

namespace ModularCommerce.Identity.Api;

public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<IdentityDbContext>(configuration, IdentityDbContext.Schema);

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddScoped<SignupHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddValidatorsFromAssemblyContaining<SignupCommandValidator>(includeInternalTypes: true);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/identity");

        group.MapAuthEndpoints();
    }
}
