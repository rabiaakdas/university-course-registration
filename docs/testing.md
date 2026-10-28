# Test kılavuzu — 10. gün

## Kısa test haritası

| Davranış | Dosya / seçim | Sınır ve önemli assertion | Önceki eksik / karar |
|---|---|---|---|
| Kredi, boş metin, normalizasyon, birlikte alan hataları | `CoreBehaviorChecks.Unit`, `-Mode unit` | Saf `CourseInput.Validate`, alan anahtarları ve sonuç değerleri | Önceden HTTP üzerinden dolaylıydı; DB'siz seçim eklendi |
| Başarılı / olmayan açılış / mükerrer kayıt | `CoreBehaviorChecks.Run`, `-Mode core` | Gerçek HTTP + ayrı EF context: tam 201/400/409, Location, FK değerleri, sayı farkı, eski kayıtların bütün alanları | Önceden geniş senaryoya bağlıydı; bağımsız fixture ve korunma assertion'ları eklendi |
| Öğrenci ve çoktan çoğa kayıt, yarış, gerçek save hatası, restart | `EnrollmentChecks.cs`, `-Mode full` | LocalDB/HTTP ve handler entegrasyonu; unique/FK sınıflandırması, tek çift, JOIN | Mevcut kapsam korunur; çekirdek mod bunların yerine geçmez |
| Ders yazma, güvenli hata yanıtı, migration/seed | `CourseWriteChecks.cs`, `-Mode full` | HTTP, SQL Server, gerçek constraint | Yeniden kullanılır |
| Dönem/açılış, tracking, dört transaction sınırı | `RelatedWriteChecks.cs`, `-Mode full` | EF + LocalDB; yeni context'te commit/rollback sonucu | Yeniden kullanılır |
| Katalog filtre/sayfa, gerçek SQL, tracking karşılaştırması | `CatalogChecks.cs`, `-Mode full` | HTTP + interceptor ile SQL yürütme | Yeniden kullanılır |
| Atomik batch, ön kontrol ve gerçek yazma hatası | `BatchEnrollmentChecks.cs`, `-Mode full` | HTTP/DB, koordineli interceptor; rakip commit korunur | Yeniden kullanılır |
| Üç liste sözleşmesi | `verify-http.ps1` (full içinden de çağrılır) | Gerçek HTTP; seed alanları, ek kayıtlara izin | Yeniden kullanılır |

## Komutlar

Repo kökünde .NET SDK 10.0.401 gerekir. DB kontrolleri ayrıca Windows LocalDB
`MSSQLLocalDB`, SQL Server 17.0.4025.3, `sqlcmd` ve PowerShell 7 (`pwsh`) kullanır.
EF paket/araç 10.0.14 değiştirilmedi. Yeni paket, test framework'ü veya ürün
projesi eklenmedi. Mevcut konsol projesi `obj/day5-checks` altında üretilir;
web projesinin `Compile Remove="scripts/**/*.cs"` kuralı testleri dışarıda tutar.

```powershell
dotnet restore
dotnet build --no-restore
# Saf birim kontrolleri: LocalDB bağlantısı/API süreci oluşturulmaz.
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit
# Tek komut: birim + üç bağımsız gerçek HTTP/DB senaryosu.
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
# Sıra bağımsızlığı:
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode core -Reverse
# Önceki günlerin geniş regresyonları (varsayılan seçim):
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
# Bilinçli çalıştırıcı kontrolü: beklenen exit code 1, normal test komutu değildir.
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit -ProbeFailure
```

Core her koşuda GUID son ekli `university_registration_tests_day10_*` DB seçer.
İsim/LocalDB hedefi ve DB'nin daha önce bulunmadığı mutasyondan önce kontrol edilir.
App/lab hedeflenmez. Migration'lar yeni DB'ye uygulanır; sabit seed gerekmez.
Her senaryo kendi çalışma etiketli ders/dönem/öğrenci/açılışını kurar ve üretilen
kimlikleri kullanır. Olmayan kimlik istekten önce DB'de gerçekten aranır.
Senaryolar seri çalışır; her biri kendi başlangıç kaydını hazırlar. Diğer
senaryonun başarılı olmasına dayanmaz. API bounded readiness polling ile beklenir.

Finally Enrollment → açılış → öğrenci/dönem/ders sırasıyla yalnız fixture
kimliklerini temizler. Ayrı context doğrulaması sonunda beş tablo boş olmalıdır.
API process kill/wait/dispose edilir. Test DB şeması ve migration geçmişi inceleme
amacıyla tutulur; DB silinmez, sequence sıfırlanmaz. Full modun eski day6 isimli
DB yaklaşımı ve seed tutma davranışı korunur. Core, önceki fault/concurrency
kontrollerini kopyalamaz.

Assertion hatası senaryo, beklenen/gerçekleşen sonuç ve gereken üretilmiş kimlikleri
bildirir (exit 1). Ortam/çalıştırma engelleri tip adıyla ayrı raporlanır (exit 2);
bağlantı veya SQL hata metni basılmaz. Temizlik hatası ayrıca `CLEANUP` olarak
bildirilir; önceki hata varsa onu örtmez. Alt dotnet komutunun başarısız kodu
PowerShell tarafından üst çağırana iletilir. Atlanan bir DB kontrolü PASS değildir.
Eski full mod raporları kendi PASS/exception düzenini korur. Temizlik hata yoluna
ayrıca fault enjekte edilmedi.

## Öğrenme notu

Unit kontrolü gerçek `CourseInput.Validate` imzasını çağırır; DB/HTTP kurmaz.
Integration kontrolleri HTTP binding/endpoint/EF/SQL Server sınırını birlikte
çalıştırır. Dosyanın `.cs` olması onu unit test yapmaz. Arrange fixture'ı kurar,
Act isteği gönderir, Assert durum koduyla birlikte DB sonucunu doğrular.
Sadece herhangi bir exception ya da “201 gelmedi” beklentisi yeterli değildir.

Birim kontrolü SQL benzersizliği, transaction veya HTTP sözleşmesini kanıtlamaz;
DbSet mock'u veya sahte DB de SQL Server kısıtlarının karşılığı değildir.
Yeni context okumaları izlenen eski nesneyi sonuç sanmayı engeller. Zaman UTC
ve istek öncesi/sonrası aralıkla doğrulanır; tam saat eşitliği aranmaz. Beklenen
hatalar metnin Türkçe yazımına değil status/alan anahtarına dayanır.

İzolasyon, deterministik beklenti ve finally temizliği tekrar çalıştırmayı sağlar.
İki geçiş bütün olası hataların yokluğunu kanıtlamaz. Kontenjan, akademik uygunluk,
yetkilendirme, genel idempotency ve altyapı arızaları bu çekirdek takımın güvencesi
değildir. Ürün davranışı ve DB şeması değiştirilmedi.

## 11. gün bağlantı güncellemesi

Kayıt fault kontrolleri aynı interceptor/context'i `EnrollmentOperations` ile
endpoint'e sağlar. Senaryolar ve HTTP beklentileri korunur. Tekli kayıt save
hatalarında üç ön kontrol SELECT'i ayrıca doğrulanır; batch'in 50 öğede üç sorgu
kontrolü korunur. Mevcut batch dosyasında işlemin iptal token'ını ret sonucuna
çevirmeden yaydığı ve DB'yi değiştirmediği doğrudan kontrol edilir. Runner ve test
framework'ü değişmedi. [Sorumluluk notu](day11-responsibilities.md).
