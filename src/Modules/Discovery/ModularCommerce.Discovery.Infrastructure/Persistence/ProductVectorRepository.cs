using ModularCommerce.Discovery.Application.Abstractions;
using ModularCommerce.Discovery.Domain.Embeddings;
using Npgsql;
using Pgvector;

namespace ModularCommerce.Discovery.Infrastructure.Persistence;

/// <summary>
/// pgvector destekli embedding kalıcılığı. EF yerine ham NpgsqlDataSource + Pgvector kullanır (vektör
/// tipi EF10'da eşlenemediğinden). Upsert PK=ProductId ile idempotenttir; arama kosinüs mesafesiyle
/// (`&lt;=&gt;`) sıralar. DataSource, DiscoveryModule'da `.UseVector()` ile kurulur (Vector tip eşlemesi).
/// </summary>
public sealed class ProductVectorRepository(NpgsqlDataSource dataSource) : IProductVectorRepository
{
    public async Task UpsertAsync(ProductEmbedding embedding, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO discovery.product_embeddings ("ProductId", "SourceTextHash", "Embedding", "UpdatedAtUtc")
            VALUES (@id, @hash, @vec, @ts)
            ON CONFLICT ("ProductId") DO UPDATE SET
                "SourceTextHash" = EXCLUDED."SourceTextHash",
                "Embedding" = EXCLUDED."Embedding",
                "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc";
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", embedding.ProductId);
        command.Parameters.AddWithValue("hash", embedding.SourceTextHash);
        // pgvector parametresi: hedef tip açıkça "vector" verilmeli — Npgsql CLR tipinden çıkaramaz.
        command.Parameters.Add(new NpgsqlParameter("vec", new Vector(embedding.Embedding)) { DataTypeName = "vector" });
        command.Parameters.AddWithValue("ts", embedding.UpdatedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<string?> GetSourceTextHashAsync(Guid productId, CancellationToken cancellationToken)
    {
        const string sql = """SELECT "SourceTextHash" FROM discovery.product_embeddings WHERE "ProductId" = @id;""";

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", productId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    public async Task<IReadOnlyList<VectorMatch>> SearchAsync(
        float[] query,
        int topN,
        CancellationToken cancellationToken)
    {
        // Kosinüs benzerliği = 1 − kosinüs mesafesi (`<=>`). En yakın (en küçük mesafe) önce.
        const string sql = """
            SELECT "ProductId", 1 - ("Embedding" <=> @q) AS score
            FROM discovery.product_embeddings
            ORDER BY "Embedding" <=> @q
            LIMIT @n;
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.Add(new NpgsqlParameter("q", new Vector(query)) { DataTypeName = "vector" });
        command.Parameters.AddWithValue("n", topN);

        var matches = new List<VectorMatch>(topN);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            matches.Add(new VectorMatch(reader.GetGuid(0), reader.GetDouble(1)));
        }

        return matches;
    }
}
