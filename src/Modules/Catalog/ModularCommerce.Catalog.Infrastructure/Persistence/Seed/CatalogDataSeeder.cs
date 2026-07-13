using Microsoft.EntityFrameworkCore;
using ModularCommerce.Catalog.Domain.Products;
using ModularCommerce.Shared.Infrastructure.Persistence;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Catalog.Infrastructure.Persistence.Seed;

/// <summary>
/// Geliştirme ortamı demo verisi. İdempotenttir (ürün varsa dokunmaz) ve ürünleri
/// YALNIZCA domain fabrikası üzerinden kurar — seed dahi invariant'lardan muaf değildir.
/// </summary>
public sealed class CatalogDataSeeder : IDataSeeder<CatalogDbContext>
{
    public async Task SeedAsync(CatalogDbContext context, CancellationToken cancellationToken)
    {
        if (await context.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        (string Name, string? Description, string Sku, decimal Price, int Stock)[] seedData =
        [
            ("Kablosuz Kulaklık X200", "Aktif gürültü engelleme, 30 saat pil ömrü.", "ELK-1001", 2499.90m, 120),
            ("Mekanik Klavye Pro", "Hot-swap anahtarlar, RGB aydınlatma.", "ELK-1002", 1899.00m, 45),
            ("4K Web Kamerası", "Otomatik odaklama, çift mikrofon.", "ELK-1003", 1349.50m, 60),
            ("Akıllı Saat Fit S", "Nabız, uyku ve adım takibi; 7 gün pil.", "ELK-1004", 3299.00m, 80),
            ("Taşınabilir SSD 1TB", "USB-C, 1050 MB/s okuma hızı.", "ELK-1005", 2799.00m, 150),
            ("Oyuncu Faresi V8", "26K DPI optik sensör, 59 gram.", "ELK-1006", 899.90m, 200),
            ("USB-C Hub 7'si 1 Arada", "HDMI 4K60, PD 100W, SD kart okuyucu.", "ELK-1007", 649.00m, 90),
            ("Temiz Kod", "Robert C. Martin — yazılım zanaatkarlığı klasiği.", "KTP-2001", 289.50m, 300),
            ("Domain-Driven Design", "Eric Evans — karmaşık yazılımın kalbinde tasarım.", "KTP-2002", 459.00m, 75),
            ("Mikroservis Desenleri", "Chris Richardson — örneklerle dağıtık mimari.", "KTP-2003", 399.90m, 55),
            ("Termos 500 ml", "Çift katman çelik, 12 saat sıcak tutar.", "EV-3001", 349.90m, 400),
            ("Kahve Öğütücü Manuel", "Seramik değirmen, ayarlanabilir çekim.", "EV-3002", 549.00m, 35),
            ("Yoga Matı 6 mm", "Kaymaz yüzey, taşıma askısı dahil.", "SPR-4001", 279.00m, 180),
            ("Koşu Ayakkabısı AirRun", "Nefes alan üst yüzey, hafif taban.", "SPR-4002", 1799.00m, 95),
            ("Kamp Feneri LED", "3 mod, USB-C şarj, IPX4 suya dayanıklılık.", "SPR-4003", 429.90m, 10),
        ];

        var products = seedData.Select(item =>
        {
            var price = Money.Create(item.Price);
            if (price.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Seed verisi geçersiz fiyat içeriyor ({item.Sku}): {price.Error.Message}");
            }

            var product = Product.Create(item.Name, item.Description, item.Sku, price.Value, item.Stock);
            if (product.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Seed verisi domain kurallarını ihlal ediyor ({item.Sku}): {product.Error.Message}");
            }

            // Seed geçmiş veri kurulumudur; event'ler dispatch edilmeyecek (outbox Hafta 7).
            product.Value.ClearDomainEvents();
            return product.Value;
        }).ToList();

        context.Products.AddRange(products);
        await context.SaveChangesAsync(cancellationToken);
    }
}
