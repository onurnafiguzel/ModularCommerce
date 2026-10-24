using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModularCommerce.Mcp.Observability;

namespace ModularCommerce.Mcp.Tests;

internal static class TestSupport
{
    /// <summary>Testlerde rate-limit'e hiç takılmayacak kadar gevşek bir ToolExecutionWrapper.</summary>
    public static ToolExecutionWrapper CreateExecutionWrapper()
    {
        var rateLimitingOptions = Options.Create(new McpRateLimitingOptions
        {
            Read = new RateLimitPolicyOptions { PermitLimit = 1000, WindowSeconds = 60 },
            Write = new RateLimitPolicyOptions { PermitLimit = 1000, WindowSeconds = 60 },
        });

        return new ToolExecutionWrapper(
            NullLogger<ToolExecutionWrapper>.Instance,
            new HttpContextAccessor(), // HttpContext=null -> "unknown" partition key, testler için yeterli
            new McpRateLimiter(rateLimitingOptions));
    }
}
