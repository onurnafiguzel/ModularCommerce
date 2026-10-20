# Follow-ups

Bu dosya, bir işin ortasında bulunan ama kapsam dışı bırakılan veya sonraya bırakılan bulguları
tutar — hemen düzeltilmeyen ama kaybolmaması gereken bilgiler.

## Checkout iptalinde rezervasyon sızıntısı riski (CheckoutHandler.HandleAsync)

**Bulundu:** 2026-10-20, MCP Faz 1 Step 6-7 sırasında, tam solution test koşusunda (100-paralel
place_order testinde `Available` 2 düştü, 1 değil).

**Kök neden (doğrulandı, izole unit testle):** `CheckoutHandler.HandleAsync`
([CheckoutHandler.cs](src/Modules/Ordering/ModularCommerce.Ordering.Application/Orders/Checkout/CheckoutHandler.cs))
içinde `stockReservation.ReserveAsync(...)`, `paymentService.ChargeAsync(...)` ve
`orders.AddAsync(...)` çağrıları ambient (çağıranın HTTP isteğine bağlı, iptal edilebilir)
`cancellationToken`'ı kullanıyor. Eğer bu token, bir rezervasyon BAŞARIYLA alındıktan (`reservedIds`'e
eklendi, `Available` zaten -1) SONRA ama ödeme/sipariş adımları tamamlanmadan ÖNCE iptal edilirse
(ör. client zaman aşımına uğrayıp isteği gerçekten keser, `HttpContext.RequestAborted` sunucuya
yayılır), o adımın `await`'i `OperationCanceledException` FIRLATIR — `Result.Failure` DÖNMEZ. Kod
yalnızca domain-seviyeli `IsFailure` sonuçlarında `ReleaseAllAsync` çağırıyor (ve `orders.AddAsync`
etrafındaki tek `catch` bloğu hariç), THROWN bir iptal istisnasını YAKALAMIYOR — istisna
`ReleaseAllAsync` hiç çağrılmadan `HandleAsync`'ten dışarı fırlıyor.

**Doğrulama:** İzole bir unit testte (`IPaymentService.ChargeAsync` mock'u
`OperationCanceledException` fırlatacak şekilde ayarlandı, `IStockReservationService.ReserveAsync`
başarıyla "rezerve edildi" döndü) → `_stockReservation.DidNotReceive().ReleaseAsync(...)` VE
`DidNotReceive().CommitAsync(...)` doğrulandı — rezervasyon ne release ne commit ediliyor, "Active"
durumda asılı kalıyor.

**Ciddiyet — KALICI DEĞİL:** `ReservationTtlSweeper`
([ReservationTtlSweeper.cs](src/Modules/Inventory/ModularCommerce.Inventory.Infrastructure/BackgroundJobs/ReservationTtlSweeper.cs))
her 30 saniyede bir, TTL'i (5 dakika) geçmiş `Active` rezervasyonları tarar; `IOrderReservationReconciler`
ile sınıflandırıp Paid bir siparişe bağlıysa Commit'e, yetimse Expire'a çevirir. Yani sızıntı
en kötü durumda ~5-5.5 dakika içinde kendiliğinden düzelir (`Available` o süre boyunca gerçek
değerinden düşük görünür — geçici bir "undersell" görünümü, ASLA oversell değil).

**Nasıl tetiklenir:** Gerçek bir client (web/mobile/MCP agent) istekten vazgeçip bağlantıyı
kapatırsa (zaman aşımı, kullanıcı sayfayı kapatır, retry mantığı eskiyi iptal edip yeni istek
açarsa) VE bu, rezervasyon alındıktan sonraki dar pencerede olursa. 100-paralel yük testinde,
MCP tarafının kendi Polly client-timeout'u (1s per-attempt) + retry mantığı bu pencereyi daha sık
tetikledi — ama altta yatan zafiyet production kodunun kendisinde, test değişikliğinden ÖNCE de
vardı.

**Önerilen düzeltme yönü (UYGULANMADI, onay bekliyor):** `ReleaseAllAsync`'in zaten yaptığı gibi
(`CancellationToken.None` kullanıp "istek iptal edilse bile telafi tamamlanmalı" prensibini
koruyarak), `HandleAsync`'in reserve→charge→add-order gövdesini ya (a) genel bir
`try { ... } catch (OperationCanceledException) { await ReleaseAllAsync(reservedIds); throw; }`
bloğuna alıp istisnayı tekrar fırlatmadan önce telafiyi çalıştırmak, ya da (b) mutasyon yapan
adımların (`ReserveAsync`/`ChargeAsync`/`AddAsync`) kendi içindeki DB çağrılarını
`CancellationToken.None` ile çalıştırıp sadece dış sınırda (endpoint) iptali onurlandırmak
arasında seçim gerektiriyor — (a) daha az invazif, (b) "iptal edilen istek hemen 499 dönsün"
davranışını değiştirebilir. **Hangi yönün tercih edileceği henüz tartışılmadı.**

**İlgili olmayan ama aynı ara.ştırmada ölçülen, zaten düzeltilen konu:** Aynı test koşusunda ayrıca
Postgres "too many clients already" hatası bulundu ve düzeltildi (bkz. commit geçmişi,
`Npgsql:MaxPoolSizePerModule` — her modülün AYRI Npgsql pool'u kurduğu, varsayılan Max Pool
Size=100 ile 8 modülün toplam talebinin Postgres'in max_connections'ını kolayca aştığı
doğrulandı: 100-paralel tek bir checkout testinde pg_stat_activity 98/100'e çıktı, pool başına
10'a sabitlenince 13'e düştü). Bu, rezervasyon-sızıntısı bulgusundan BAĞIMSIZ bir konu, ayrıca
not düşülmüyor çünkü zaten çözüldü.
