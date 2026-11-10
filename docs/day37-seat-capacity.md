# Gün 37 — Atomik kontenjan ve kontrollü geçişler

## İhtiyaç ve iş kuralı
Okunan boş yer sayısı daha sonra yazma hakkını garanti etmez. Gün36'nın kusuru
gösteren laboratuvarı korunur; çözüm gerçek EnrollmentOperations yolundadır.
Draft/Rejected sıfır, Submitted/Approved bir yer tüketir. OccupiedSeats istemci
DTO'sunda bulunmaz, setter private'dır. DB CHECK aralığı korur; sayaç ile kayıt
sayısının eşitliğini tek başına CHECK sağlamaz, koordineli yazma yolu sağlar.

## İşlem ve kod konumları
- `Models/CourseOffering.cs`, `Data/UniversityDbContext.cs`: sayaç/default0/CHECK;
  mevcut öğrenci–açılış UNIQUE ve FK'ler değişmez.
- `Application/EnrollmentOperations.cs`: önce akademik hazırlık/değerlendirme,
  sonra açık transaction; açılış satırı UPDLOCK/HOLDLOCK, kayıt durumu yeniden
  kontrolü, koşullu UPDATE ve etkilenen satır sayısı. SQL değerleri parametrelidir.
  Submit'in +1'i ile Draft→Submitted aynı commit'tedir. Reject'in yalnız kazanan
  Submitted→Rejected işlemi -1 yapar. Approve yer artırmaz.
- Entity TrySubmit/TryApprove/TryReject ve CleanRejectionReason kuralları, bulk
  güncelleme öncesinde kullanılır. AsNoTracking kaynakları SaveChanges ile geri
  yazılmaz; eski tracker değerleri yeni sayacı ezmez.
- `Application/EnrollmentResults.cs`, `Endpoints/EnrollmentEndpoints.cs`: dolu
  açılışın açıklanabilir 409 sonucu; yetki/CSRF filtreleri yerinde kalır.
- `EnrollmentApprovedPublisher`: yalnız gerçek commit sonrasında çağrılır;
  hata/iptal politikası değişmez. Dış transaction reddedildiğinden SaveChanges'i
  dış commit sanarak erken yayın yapılmaz.

## Migration
`Data/Migrations/20261110110730_AddOccupiedSeats.cs`: sütun, guarded backfill,
CHECK. Designer/snapshot EF aracıyla üretildi. COUNT_BIG ve kapasite kontrolünden
sonra int dönüşümü yapılır; geçmiş Submitted/Approved sayılır. SQL51037 aşımda
migration'ı durdurur, kayıt/kapasite değiştirilmez. Uygulama yazmaları durdurulmalıdır.
Down CHECK'i sonra sütunu kaldırır; bu çalışmada Down çalıştırılmadı.

## Doğrulama düzeni
`scripts/SeatCapacityChecks.cs`: ayrı DbContext/açık SQL bağlantıları, farklı SPID,
kilit öncesinde TaskCompletionSource buluşması, 15 saniye buluşma ve 45 saniye çift
çalışan sınırı; bir hata diğerini iptal eder, Task.WhenAll bitmeden temizlik başlamaz.
Task.Delay ile sıra tahmin edilmez. Yeni context sayaç=Submitted+Approved kontrolü
yapar. Gerçek geçici CHECK, +1 sonrasındaki durum yazımını bozarak tam rollback'i
kanıtlar. Observer başka context ile kesinleşmiş Approved okur.

Eski aktif fixture'lar yalnız test setup'ında, kendi açılışları için aynı transaction
ile sayacı hazırlar; bu yardımcı üretim geçişinden sonra hatayı gizlemek için çağrılmaz.
State HTTP testi kapasite409, Draft korunması, yer bırakma/yeniden kullanımı kapsar.
Observer sorgu karşılaştırması kilit ve durum kontrolü nedeniyle 7'den 9'a çıktı;
log observer hâlâ ek SELECT yapmaz. SQL şema ve negatif kapasite kontrolleri yeni
sütun/CHECK'e uyarlanmıştır; iki CHECK'i ihlal eden negatif kapasitede SQL Server'ın
hangi kısıtı önce raporlayacağı varsayılmaz.

## Sınırlar
Aynı açılış yazmaları sıraya girer; yoğun açılışta bekleme/deadlock/timeout maliyeti
vardır, otomatik retry yoktur. Farklı açılışlardaki öğrenci kredi/saat yarışları ve
yetki kontrolü ile atama değişikliği arasındaki TOCTOU çözülmedi. Doğrudan yönetim
SQL'i koordinasyonu atlayabilir. Commit/yayın boşluğu ve HTTP yanıt kaybı hâlâ vardır;
Outbox/exactly-once yoktur. Bu çalışma genel lost-update çözümü veya benchmark değildir.
Gerçek çalıştırma sonuçları `docs/progress.md` Gün37 kaydındadır.
