using System.Security.Cryptography;
using System.Text;
using ModularCommerce.Discovery.Application.Abstractions;
using ModularCommerce.Discovery.Domain.Embeddings;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Discovery.Application.Indexing;

/// <summary>
/// Bir ürünü indeksler: aranabilir metni kurar, özetini (hash) çıkarır, DEĞİŞMEMİŞSE atlar
/// (idempotent + gereksiz embedding maliyeti yok — at-least-once tekrar teslimatına ve no-op
/// güncellemelere karşı koruma), aksi halde embedding üretip upsert eder.
/// </summary>
public sealed class IndexProductHandler(
    IEmbeddingService embeddingService,
    IProductVectorRepository repository)
{
    public async Task<Result> HandleAsync(ProductIndexRequest request, CancellationToken cancellationToken)
    {
        var searchableText = BuildSearchableText(request);
        var sourceTextHash = ComputeHash(searchableText);

        var existingHash = await repository.GetSourceTextHashAsync(request.ProductId, cancellationToken);
        if (existingHash == sourceTextHash)
        {
            // Metin değişmemiş → yeniden embed etme (kopya event ya da alakasız alan güncellemesi).
            return Result.Success();
        }

        var embedding = await embeddingService.EmbedAsync(searchableText, cancellationToken);
        if (embedding.IsFailure)
        {
            return Result.Failure(embedding.Error);
        }

        var productEmbedding = ProductEmbedding.Create(request.ProductId, embedding.Value, sourceTextHash);
        if (productEmbedding.IsFailure)
        {
            return Result.Failure(productEmbedding.Error);
        }

        await repository.UpsertAsync(productEmbedding.Value, cancellationToken);
        return Result.Success();
    }

    // Aranabilir metin = ad + açıklama + SKU (Catalog'da kategori alanı yok). Boş alanlar atlanır.
    private static string BuildSearchableText(ProductIndexRequest request)
        => string.Join(' ', new[] { request.Name, request.Description, request.Sku }
            .Where(s => !string.IsNullOrWhiteSpace(s)))
            .Trim();

    private static string ComputeHash(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
