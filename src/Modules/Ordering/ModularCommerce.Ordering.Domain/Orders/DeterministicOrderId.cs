using System.Security.Cryptography;
using System.Text;

namespace ModularCommerce.Ordering.Domain.Orders;

/// <summary>
/// Order.Id'yi (CustomerId, IdempotencyKey) çiftinden RFC 9562 §5.5 (adlandırılmış, SHA-1
/// tabanlı UUIDv5) ile DETERMİNİSTİK üretir — aynı key için yarışan TÜM paralel checkout
/// denemeleri, hangisi "kazansa" farketmeksizin AYNI Order.Id'yi hesaplar. Bu, Payment'ın
/// hakemi (kendi unique index'i) ile Ordering'in hakeminin (orders tablosunun unique index'i)
/// birbirinden BAĞIMSIZ, farklı "kazananlar" seçebilmesinden kaynaklanan tutarsızlığı
/// (Payment.OrderId'nin, gerçekte persist edilen siparişin Id'sinden farklı olabilmesi) kökten
/// kapatır.
///
/// SHA-1 burada KRİPTOGRAFİK güvenlik için değil, UUIDv5 spesifikasyonunun ZORUNLU kıldığı
/// adlandırılmış-UUID algoritması için kullanılır (CA5350 bilerek bastırıldı) — çakışma riski
/// pratikte sıfır (122 bit) ve tehdit modeli "düşman üretsin" değil "aynı girdi aynı Id versin".
/// </summary>
public static class DeterministicOrderId
{
    /// <summary>
    /// SABİT namespace — ASLA değiştirilmemeli: değiştirilirse bu noktadan sonra üretilen TÜM
    /// Order.Id'ler değişir (yeni siparişler, var olan idempotency key'lerle BİLE farklı bir Id
    /// üretmeye başlar). Geçmiş siparişler zaten rastgele Id taşıdığı için (bkz. migration notu,
    /// aşağıda) bu sabit yalnızca BUNDAN SONRAKİ siparişler için anlamlıdır.
    /// </summary>
    private static readonly Guid Namespace = Guid.Parse("ed25bc62-6e4e-4744-a360-aab59e310e46");

    /// <summary>
    /// Kanonik ad formatı SABİTTİR: "{CustomerId:N}:{IdempotencyKey}", UTF-8, key büyük/küçük
    /// harfe duyarlı (hiçbir normalizasyon yapılmaz — "ABC" ve "abc" FARKLI siparişler üretir,
    /// idempotency key'in kendisi zaten client tarafından sabit/aynen tekrar gönderilir).
    /// </summary>
    public static Guid Create(Guid customerId, string idempotencyKey)
    {
        var name = $"{customerId:N}:{idempotencyKey}";
        return CreateVersion5(Namespace, name);
    }

    /// <summary>
    /// RFC 9562 §5.5 adlandırılmış UUID (versiyon 5, SHA-1). Genel amaçlı, Order'a özgü değil —
    /// test edilebilirlik için public (bilinen RFC test vektörüyle doğrulanır).
    /// .NET'in <see cref="Guid"/> byte düzeni (ilk üç alan little-endian) RFC'nin ağ byte
    /// sırasından (big-endian) farklıdır; namespace'i hash'lemeden ÖNCE ve sonucu hash'ten
    /// SONRA byte-swap gerekir — bu adım atlanırsa algoritma RFC'ye uymaz (test vektörü bunu
    /// yakalar).
    /// </summary>
#pragma warning disable CA5350 // UUIDv5 spesifikasyonu SHA-1 zorunlu kılar; kriptografik güvenlik amaçlı değildir.
    public static Guid CreateVersion5(Guid @namespace, string name)
    {
        var namespaceBytes = @namespace.ToByteArray();
        SwapByteOrder(namespaceBytes);

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var data = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(data, 0);
        nameBytes.CopyTo(data, namespaceBytes.Length);

        var hash = SHA1.HashData(data);
        var guidBytes = hash[..16];

        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50); // version = 5
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80); // variant = RFC 9562

        SwapByteOrder(guidBytes);
        return new Guid(guidBytes);
    }
#pragma warning restore CA5350

    private static void SwapByteOrder(byte[] guid)
    {
        Swap(guid, 0, 3);
        Swap(guid, 1, 2);
        Swap(guid, 4, 5);
        Swap(guid, 6, 7);
    }

    private static void Swap(byte[] guid, int left, int right)
        => (guid[left], guid[right]) = (guid[right], guid[left]);
}
