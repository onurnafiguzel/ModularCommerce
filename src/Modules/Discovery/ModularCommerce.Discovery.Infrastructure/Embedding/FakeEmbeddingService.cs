using System.Security.Cryptography;
using System.Text;
using ModularCommerce.Discovery.Application.Abstractions;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Discovery.Infrastructure.Embedding;

/// <summary>
/// Deterministik, sırsız embedding (default sağlayıcı). Gerçek bir dil modeli DEĞİL: "feature hashing"
/// (hashing trick) ile bag-of-words vektörü üretir — her token sabit bir boyuta düşer, paylaşılan
/// kelimeler benzerliği artırır. Bu sayede (a) testler deterministik, (b) ortak-terimli arama gerçekten
/// çalışır ("kulaklık" sorgusu "...Kulaklık..." ürününü bulur) ve (c) API anahtarı/sır gerekmez.
/// Gerçek semantik yakınlık (eşanlam vb.) için Http sağlayıcı gerekir.
/// </summary>
public sealed class FakeEmbeddingService : IEmbeddingService
{
    public Task<Result<float[]>> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        var vector = new float[EmbeddingConstants.Dimensions];

        foreach (var token in Tokenize(text))
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            var index = (int)(BitConverter.ToUInt32(hash, 0) % (uint)vector.Length);
            vector[index] += 1f;
        }

        Normalize(vector);
        return Task.FromResult(Result.Success(vector));
    }

    // Harf/rakam dizilerine böler (Türkçe harfler dahil), küçük harfe indirger.
    private static IEnumerable<string> Tokenize(string text)
    {
        var current = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                current.Append(char.ToLowerInvariant(ch));
            }
            else if (current.Length > 0)
            {
                yield return current.ToString();
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    private static void Normalize(float[] vector)
    {
        double sumOfSquares = 0;
        foreach (var value in vector)
        {
            sumOfSquares += value * (double)value;
        }

        if (sumOfSquares <= 0)
        {
            return; // boş/tokensiz metin → sıfır vektör (arama sırası anlamsız ama kırılmaz).
        }

        var norm = (float)Math.Sqrt(sumOfSquares);
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= norm;
        }
    }
}
