using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace ModularCommerce.ArchitectureTests;

/// <summary>
/// Modül sınırlarını derleme sonrası doğrulayan testler.
/// Kural: bir modül, başka bir modülün yalnızca Contracts katmanına bağımlı olabilir.
/// Bu testler kırmızıysa mimari ihlal edilmiş demektir; CI build'i kırar.
/// </summary>
public class ModuleBoundaryTests
{
    private static readonly string[] Modules =
    [
        "Identity", "Catalog", "Cart", "Inventory",
        "Ordering", "Payment", "Shipping", "Notification",
    ];

    private static readonly string[] InternalLayers =
    [
        "Domain", "Application", "Infrastructure", "Api",
    ];

    public static TheoryData<string> ModuleNames()
    {
        var data = new TheoryData<string>();
        foreach (var module in Modules)
        {
            data.Add(module);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_should_not_depend_on_other_modules_internals(string module)
    {
        var assembly = System.Reflection.Assembly.Load($"ModularCommerce.{module}.Api");

        var forbidden = Modules
            .Where(other => other != module)
            .SelectMany(other => InternalLayers.Select(layer => $"ModularCommerce.{other}.{layer}"))
            .ToArray();

        var result = Types.InAssembly(assembly)
            .Should()
            .NotHaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"{module} modülü diğer modüllerin iç katmanlarına bağımlı olmamalı; " +
            "yalnızca Contracts katmanına referans verilebilir");
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Domain_should_not_depend_on_infrastructure(string module)
    {
        var assembly = System.Reflection.Assembly.Load($"ModularCommerce.{module}.Domain");

        var result = Types.InAssembly(assembly)
            .Should()
            .NotHaveDependencyOnAny(
                $"ModularCommerce.{module}.Infrastructure",
                $"ModularCommerce.{module}.Application",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"{module}.Domain saf kalmalı: EF Core dahil hiçbir dış katmana bağımlılık olamaz");
    }
}
