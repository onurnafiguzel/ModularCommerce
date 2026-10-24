using Xunit;

namespace ModularCommerce.Mcp.IntegrationTests;

/// <summary>Testcontainer + WebApplicationFactory pahalı — koleksiyondaki tüm testler paylaşır.</summary>
[CollectionDefinition(nameof(McpHostCollection))]
public sealed class McpHostCollection : ICollectionFixture<McpHostFixture>;
