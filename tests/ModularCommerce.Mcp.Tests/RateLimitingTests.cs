using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModularCommerce.Mcp.Observability;
using Xunit;

namespace ModularCommerce.Mcp.Tests;

/// <summary>
/// Tool-seviyeli (route değil) rate limiting: ToolExecutionWrapper, limit aşılınca McpException
/// fırlatır; Read ve Write policy'leri birbirinden bağımsız sayaçlar tutar.
/// </summary>
public sealed class RateLimitingTests
{
    private static ToolExecutionWrapper CreateWrapper(int readLimit, int writeLimit)
    {
        var options = Options.Create(new McpRateLimitingOptions
        {
            Read = new RateLimitPolicyOptions { PermitLimit = readLimit, WindowSeconds = 60 },
            Write = new RateLimitPolicyOptions { PermitLimit = writeLimit, WindowSeconds = 60 },
        });

        return new ToolExecutionWrapper(
            NullLogger<ToolExecutionWrapper>.Instance,
            new HttpContextAccessor(),
            new McpRateLimiter(options));
    }

    [Fact(DisplayName = "Limit aşılınca ToolExecutionWrapper McpException fırlatır, operasyon hiç çalıştırılmaz")]
    public async Task ExceedingLimit_ThrowsMcpException_WithoutRunningOperation()
    {
        var wrapper = CreateWrapper(readLimit: 1, writeLimit: 1);
        var callCount = 0;
        Task<string> Operation()
        {
            callCount++;
            return Task.FromResult("ok");
        }

        await wrapper.ExecuteAsync("check_stock", ToolRateLimitPolicy.Read, Operation);

        var act = async () => await wrapper.ExecuteAsync("check_stock", ToolRateLimitPolicy.Read, Operation);

        await act.Should().ThrowAsync<McpException>();
        callCount.Should().Be(1, "limit aşıldığında operasyon hiç çalıştırılmamalı");
    }

    [Fact(DisplayName = "Write policy'nin limiti dolması, Read policy'yi etkilemez (bağımsız sayaçlar)")]
    public async Task WriteLimitExhausted_DoesNotAffectReadPolicy()
    {
        var wrapper = CreateWrapper(readLimit: 100, writeLimit: 1);

        await wrapper.ExecuteAsync("set_cart_item", ToolRateLimitPolicy.Write, () => Task.FromResult("ok"));
        var writeAct = async () =>
            await wrapper.ExecuteAsync("set_cart_item", ToolRateLimitPolicy.Write, () => Task.FromResult("ok"));
        await writeAct.Should().ThrowAsync<McpException>();

        // Write tükendi ama Read hâlâ serbest olmalı.
        var readResult = await wrapper.ExecuteAsync(
            "search_products", ToolRateLimitPolicy.Read, () => Task.FromResult("ok"));
        readResult.Should().Be("ok");
    }
}
