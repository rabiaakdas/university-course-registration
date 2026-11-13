# Gün 44 — Yarış testleri ve kalıcı invariant'lar

## Yeni özellik değil, mevcut kanıtın tamamlanması
ConcurrencyInvariantChecks.Run mevcut SeatCapacityChecks, IdempotencyChecks ve
DropPlacementChecks senaryolarını çağırır; iş akışları kopyalanmadı.
Üretim kodu, model, migration ve paketler değişmedi. Bu yüzden yeni ADR yoktur.

    dotnet restore
    dotnet build --no-restore
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-course-writes.ps1 -Mode concurrency-invariants
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-course-writes.ps1 -Mode full

Full, mevcut senaryolarına ek olarak daha önce ayrı modda çalışan seat-capacity
senaryolarını da çalıştırır. Yeni genel test framework'ü veya uygulama kilidi yoktur.

## İş bittikten sonra ölçülenler
scripts/ConcurrencyInvariantChecks.cs, yalnız fixture'a ait açılışlarda ve yeni
context ile:
- Beklenen açılışların hâlâ var olması.
- 0 ≤ OccupiedSeats ≤ Capacity.
- OccupiedSeats = Submitted + Approved sayısı.
- Her öğrenci/açılış için tek enrollment.
- Her öğrenci/açılış için en fazla bir Waiting.
- Placed başvuru için ilgili enrollment'ın bulunması.

Son madde enrollment'ı sonsuza kadar Submitted tutmaz. Placed tarihsel bilgidir;
sonraki Approved/Rejected/Dropped mümkündür. Yeni yerleştirme anında beklenen
adayın Submitted olduğu ayrıca DropPlacementChecks.State tarafından sınanır.
Placed→sonradan Dropped deneyi iki kaydın geçerli birlikte kalmasını doğrular.

## İzin verilen sonuçlar
| Senaryo | Beklenti |
|---|---|
| Farklı iki Draft, son yer | Updated + CapacityUnavailable; kaybeden Draft |
| Aynı Draft'a çift submit | Updated + InvalidTransition; sayaç +1 |
| Aynı Submitted'a çift reject | Updated + InvalidTransition; sayaç -1 |
| Approve–reject | Bir Updated, bir InvalidTransition; terminal duruma uygun sayaç/event |
| Aynı kayda çift drop | İki Updated/HTTP204 mümkün; tek bırakma ve tek yerleştirme |
| İki farklı drop | İkisi Updated; doğru iki aday ve aktif sayaç |
| Drop–submit, Waiting varken | Drop Updated; submit QueuePriority veya CapacityUnavailable |
| İptal–yerleştirme | Drop204; iptal204 ise Cancelled ve sonraki aday,409 ise ilk aday Placed |
| Aynı idempotency key | Birden çok201, aynı ilk sonuç; tek kalıcı Draft/receipt |
| Ara gerçek SQL hatası | Başarı değil; bütün bağlı yazmalar rollback |

Sıralama kazananı sabitlenmez; lifecycle sonucu ve kalıcı kayıtlar birlikte
incelenir. Evrensel bir başarı/bir ret assertion'ı kullanılmaz.

## Hedef korumaya ulaşma ve işlerin toplanması
Seat/drop SQL yarışları iki açık context/bağlantının @@SPID ayrımını doğrular.
Bariyer açılış kilidini almadan öncedir; kilitte bekleyen işten bariyere varması
istenmez. Süreli TCS ve cancellation kullanılır; Task.Delay ile sıra tahmini yoktur.
SQL yarışında hata eşinin token'ını iptal eder; Task.WhenAll bütün işleri toplar.

HTTP denemeleri mevcut izole cookie/CSRF fixture ve sabit test sertifikası
doğrulamasıyla çalışır. Drop/cancel yarışında süre doğrudan HTTP isteklerinin
token'ına bağlandı: dış WaitAsync timeout'undan sonra hâlâ yazan işler bırakılmaz.
Idempotency HTTP isteklerinin ayrı30 saniyelik sınırı vardır; WhenAll hepsini
toplar. Timeout başarı kabul edilmez.

Son yer testi özel CapacityUnavailable sonucunu, çift geçişler özel lifecycle
sonucunu ve DB durumunu birlikte arar; bir akademik ret başarı değildir.
Create yarışında tüm201 yanıtlar ve gerçek UNIQUE claim çakışması doğrulanır.
Ara koltuk yazmasında gerçek CHECK547 ve çalışmış sayaç UPDATE sayısı; yerleştirmede
gerçek51043 trigger noktası ve fresh-context rollback kanıtı vardır.
Cancellation deneyinde yalnız OCE yeterli değildir: gerçek sayaç azaltma
noktasına erişim ve son kalıcı verinin geri alınması ayrıca doğrulanır.
Deadlock veya timeout'u gerçek SQL yazma hatası yerine sayan assertion yoktur.

## İz ve temizlik
SCENARIO / RESULT / REACHED / PASS invariant satırları yalnız sabit senaryo adı,
fixture sıra numarası, sonuç türü, HTTP kodu ve bağlantı sayısını içerir.
Parola/token/cookie/ham gövde/bağlantı bilgisi basılmaz.
Seat senaryosunun mevcut hata-koruyan temizliği korunur; idempotency ve drop
finally blokları da asıl hatayı saklar. Temizlik ayrıca başarısızsa güvenli türü
loglanır; asıl hata yoksa CleanupFailure ile sıfır olmayan çıkış üretilir.
Yalnız testin oluşturduğu satırlar/trigger/DB/süreçler temizlenir.

## Sınırlar
Farklı açılışlarda aynı öğrencinin kredi/saat yarışı, danışman/sahiplik TOCTOU,
yük/uzun transaction adaleti, crash recovery ve gerçek ağ yanıt kaybı çözülmüş
veya test edilmiş sayılmaz. Idempotency'de commit yanıtını kullanmadan tekrar
etmek gerçek ağ kaybı enjeksiyonu değildir. Bir koşunun geçmesi bütün olası
interleaving'lerin matematiksel kanıtı değildir. Gerçek sonuçlar progress kaydında.
