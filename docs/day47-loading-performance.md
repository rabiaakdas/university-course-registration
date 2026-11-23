# Gün 47 — ilişkili veri yükleme ve öğrenci programı

## İhtiyaç ve mevcut yaklaşım

Öğrenci dönem programının kayıt sayısı arttıkça ayrı sorgular üretip
üretmediği ölçüldü. Üretim kodu zaten ihtiyaca uygun olduğundan değiştirilmedi.
Yeni endpoint, model, migration, paket, index, cache veya ADR eklenmedi.

Kod konumları:

- `Application/TermRegistrationSummaryFacade.cs`: ardışık, filtreli DTO
  sorguları; kredi toplamı ve toplu aktif program saatleri.
- `Contracts/TermRegistrationSummaryDto.cs`: mevcut yanıt sözleşmesi.
- `Endpoints/TermRegistrationSummaryEndpoint.cs`: mevcut GET ve kaynak erişimi.
- `scripts/SummaryChecks.cs`: küçük/büyük fixture, gerçek SQL ve HTTPS kontrolleri.
- `scripts/CatalogChecks.cs`: mevcut `CatalogSqlCapture` interceptor'ı.
- `scripts/CourseWriteChecks.cs`: summary/full içindeki ortak kontrolün çağrılması.

N+1, her kayıt için ayrıca ilişkili veri sorgulanmasıdır. Burada iki varlık
kontrolü (öğrenci/dönem), bir kayıt/derse projection, bir aktif kredi SUM ve
aktif açılış varsa tek toplu saat sorgusu vardır. Aynı DbContext sorguları
sıralı await edilir. Aktif açılış yoksa saat sorgusu atlanır; aktif ama saatsiz
bir açılış varsa sorgu çalışır ve boş sonuç döner.

Kayıt listesi mevcut sözleşme gereği bütün durumları içerir. Aktif kredi ve
program yalnız Submitted/Approved içindir. Draft/Rejected/Dropped listelenir
ama aktif yüke veya programa girmez. Saatsiz aktif açılış kimlikleri ayrı
listelenir. Öğrenci ve dönem filtreleri SQL'dedir; kredi SQL bigint/C# long'dur.
Saatleri kayıtlarla JOIN etmemek, çok saatli dersin kredi ve kayıt satırlarını
çoğaltmasını önler. Programın her satırı tek gerçek saat kaydını temsil eder.

## Include, projection ve split query

| Seçenek | Bu akıştaki değerlendirme |
| --- | --- |
| Include | Entity grafiğine ihtiyaç yok; mevcut modellerde navigation ekleyerek daha fazla alan yüklemek somut kazanç sağlamaz. Tracking varsayılanını ayrıca düşünmek gerekir. |
| DTO projection | İhtiyaç duyulan alanları seçer; entity içermeyen sonuç tracking oluşturmaz. Mevcut tercih korundu. |
| AsSplitQuery | Collection Include JOIN çoğalmasını azaltabilir; ek round-trip ve birden fazla okuma anı maliyeti vardır. Mevcut ayrı toplu DTO sorgularına eklenmesi gerekmiyor. |

Global split ayarı veya lazy loading eklenmedi. Sorgu sayısının sabit olması
veri miktarının sabit olduğu anlamına gelmez; sonuç/bellek hacmi kayıt ve saat
sayısıyla büyür. Bu ders büyük veri yük testi veya indeks optimizasyonu değildir.

## Gerçek doğrulama — 23 Kasım 2026

`dotnet restore` ve `dotnet build --no-restore --nologo`: başarılı,
0 uyarı/0 hata. Mevcut çalıştırıcıyla `-Mode summary`: exit 0.
İzole gerçek LocalDB ve test sertifikasına bağlı HTTPS istemcileri kullanıldı.

| Fixture | Kayıt | Program saati | Çalışan SQL | Tracked entity |
| --- | ---: | ---: | ---: | ---: |
| Küçük, dört durum | 4 | 4 | 5 | 0 |
| Boş | 0 | 0 | 4 | 0 |
| Yalnız Draft | 1 | 0 | 4 | 0 |
| Draft + saatsiz Approved | 2 | 0 | 5 | 0 |
| Büyük, farklı şubeler | 24 | 6 | 5 | 0 |
| Büyük + saatli Dropped | 25 | 6 | 5 | 0 |

Sayılar facade'ın gerçekten tamamlanan SQL komutlarının interceptor kaydıdır;
ToQueryString tahmini değildir. HTTP authorization veya aralıklı cookie stamp
kontrolü sorguları bu sayılara dahil değildir. Olmayan öğrenci/dönem için
mevcut erken dönüşler sırasıyla 1/2 sorguyla ayrıca doğrulandı.

Gerçek SELECT'lerde öğrenci/dönem parametreleri, WHERE/ORDER BY, aktif SUM ve
bigint incelendi. Enrollment projection'ı saat tablosuna JOIN yapmıyor;
RowVersion, Capacity, OccupiedSeats ve kişisel entity alanlarını almıyor.
Saat projection'ı yalnız CourseOfferingId/Day/StartsAt/EndsAt alıyor.
Tam SQL metni veya alias/whitespace snapshot'ı kullanılmadı; parametre değerleri
ve secret'lar çıktıya yazılmadı.

Yanıtlar yalnız önce/sonra eşitliğiyle değil bilinen fixture değerleriyle
kontrol edildi: küçük örnekte 9 kredi; büyükte 9 + 2*Int32.MaxValue + 18*4;
aynı dersin şubeleri ayrı katkı verir. Altı saatin tamamının açılış/gün/zaman
ve deterministik sırası, tick hassasiyeti, saatsiz kimlikler, farklı öğrenci
ve dönem, bütün durumlar ve Dropped dışlaması doğrulandı.
Yeni context snapshot'ları okumaların fixture verisini değiştirmediğini gösterdi.

Gerçek HTTPS: anonymous 401, StudentAffairs 403; Student sahibi ve atanmış
Advisor 200/no-store, başka öğrenci 404. Mevcut geçersiz girdi 400 ve bulunamama
kontrolleri korundu. GET CSRF gerektirmez; üretim POST/CSRF kodu değiştirilmedi.
Bu koşu bütün cookie/bearer/CSRF regresyonlarının yeniden çalıştırıldığı anlamına
 gelmez. Üretim değişmediğinden full paket yeniden çalıştırılmadı.

## Tutarlılık ve açık sınırlar

Birden fazla SELECT kendiliğinden tek anlık snapshot sağlamaz. Kayıt listesi,
kredi toplamı ve program farklı commit'leri görebilir; sırf ölçüm için transaction
veya global isolation değiştirilmedi. Eşzamanlı okuma interleaving'leri bu testte
enjekte edilmedi. Milisaniye ölçümü/performans garantisi yapılmadı.

Fixture temizliği FK sırasıyla, API kapatma ve model/snapshot/pending migration
kontrolü başarılıydı. Yalnız bu koşunun izole DB'si ayrıca kaldırıldı; app/lab
verilerine dokunulmadı. Sıradaki gün 48: indeks ve sorgu planı optimizasyonu.
