namespace ModularCommerce.Mcp.Tests;

/// <summary>Gerçek ağ olmadan HTTP yanıtlarını senaryolamak için test çifti.</summary>
public sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(respond(request));
}
