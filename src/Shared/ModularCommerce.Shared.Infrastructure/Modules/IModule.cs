using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModularCommerce.Shared.Infrastructure.Modules;

/// <summary>
/// Her modülün Host'a kendini tanıtmak için uyguladığı sözleşme.
/// Host, modülün iç yapısını bilmez; sadece bu arayüzü bilir.
/// </summary>
public interface IModule
{
    string Name { get; }

    void Register(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
