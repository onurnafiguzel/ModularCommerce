using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ModularCommerce.Shared.Infrastructure.Configuration;

public static class OptionsExtensions
{
    /// <summary>
    /// Bir options tipini config'ten bağlar, DataAnnotations (`[Range]` vb.) ile doğrular ve
    /// **başlangıçta** (ValidateOnStart) fail-fast eder — her modülün elle "Get + if + throw"
    /// blokunu yazması yerine tek, deklaratif desen. Ayrıca somut <typeparamref name="T"/>'yi
    /// singleton olarak köprüler: mevcut tüketiciler `IOptions&lt;T&gt;` yerine doğrudan <c>T</c>
    /// enjekte etmeye devam edebilir (PspOptions/NotificationOptions deseni bozulmaz).
    /// </summary>
    public static IServiceCollection AddValidatedOptions<T>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where T : class
    {
        services.AddOptions<T>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<T>>().Value);

        return services;
    }
}
