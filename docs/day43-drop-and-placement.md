# Gün 43 — Bırakma ve atomik bekleme listesi yerleştirmesi

## HTTP ve durumlar
POST /enrollments/{id}/drop: Identity application cookie, Student rolü,
enrollment'ın gerçek Student.UserId sahipliği ve X-CSRF-TOKEN gerekir.
Kimlik yok 401, rol yok 403, kaynak/ilişki yok ortak 404.
Submitted veya Approved → Dropped başarı 204; kendi Dropped kaydına tekrar 204,
ek sayaç/yerleştirme yok. Draft/Rejected 409. Bearer tek başına yeterli değil.

EnrollmentStatus eski değerleri korur: Draft=0, Submitted=1, Approved=2,
Rejected=3, Dropped=4. Yalnız Submitted/Approved yer, kredi ve program yüküdür.
Detay/listede status Dropped; dönem özeti counts.dropped alanı eklendi.
Bırakma fiziksel silme değildir; öğrenci+açılış UNIQUE korunur, terminal kaydı
yeniden açma veya aynı açılışa yeni enrollment oluşturma yolu eklenmedi.

## Atomik sınır ve öncelik
Application/OfferingCoordination.cs, mevcut açık transaction içinde parametreli
UPDLOCK,HOLDLOCK ile CourseOffering satırını alır. Sıra açılış→enrollment/başvuru.
Submit/approve/drop/reject ve waitlist katılım/iptal aynı kapıdan geçer.
Kilit öncesi sorgu yalnız açılışı bulur; durum ve akademik veri kilit sonrası okunur.
Katılım QueueNumber'ı kilit alındıktan sonra INSERT sırasında üretir.

Application/EnrollmentOperations.cs:
- Drop/reject koşullu geçişi, -1 sayaç, Ineligible kararları, Submitted enrollment,
  +1 sayaç ve Placed başvurusu aynı transaction'da kesinleşir.
- PlaceWaitingAsync private yardımcıdır; dış transaction'ı commit etmez, public
  bypass veya sahte principal yoktur.
- Waiting varsa normal submit 409 waitlist_priority; boş yer olsa da sıra atlanmaz.
- Approve sayacı artırmaz. Onay olayı yalnız approve commit sonrasında yayımlanır.
  Drop/yerleştirme Approval veya başka bildirim üretmez.
- ExecuteUpdate sonuçları eski tracked entity ile SaveChanges üzerinden geri
  yazılmaz; yeni yerleştirme entity'si kaydedildikten sonra detach edilir.
- DB hatası veya cancellation tüm yazmaları rollback eder. Başarısız işlemde
  tracker içeriği kalıcı kanıt değildir; testler yeni context ile doğrular.

## Aday değerlendirmesi
Waiting satırları QueueNumber artan keyset sayfalarıyla 32'şer alınır; toplam
aday sınırı yoktur. Sayfanın enrollment, ön koşul, tamamlanan ders, aktif kredi ve
saat verileri toplu sorgulanır. Mevcut evaluator karar verir.
Her aday yalnız bir başvuruya sahiptir; aynı açılışta yerleştirilen farklı öğrenciler
birbirinin akademik yükünü değiştirmez.

- Submitted/Approved mevcutsa Ineligible: already_active; ikinci yer tüketmez.
- Rejected/Dropped mevcutsa Ineligible: terminal_enrollment; yeniden açılmaz.
- Akademik ret: academic: ve mevcut kararlı ret kodları; DB arızası ret sayılmaz.
- Uygun Draft aynı Id ile Submitted olur. Enrollment yoksa yeni Submitted eklenir.
- Başvuru Placed kapanır; iptal yalnız Waiting→Cancelled olduğundan Placed'i açmaz.
- Yer kalmadığında işlem biter; önceki adayın akademik reti sıradakini engellemez.

## Maliyet ve kalan sınırlar
FIFO, kilit altında üretilen QueueNumber sırasıdır; tarayıcı tıklama sırası veya
bağımsız commit zamanı garantisi değildir. Sequence boşlukları normaldir.
Büyük listede bellekte yalnız küçük sayfa tutulsa da çok sayıda uygunsuz aday uzun
transaction, kilit beklemesi ve rollback maliyeti doğurur. Otomatik retry/job yoktur.

Drop/reject varsa mevcut bütün boş yerler helper tarafından doldurulabilir.
Eski boş yer+Waiting durumunu periyodik toparlayan job veya public repair endpoint'i
yoktur. Sonraki başarılı yer açma işlemi toparlar; yalnız tekrarlanan Dropped isteği
toparlama başlatmaz. Yer açacak kayıt yoksa bugün otomatik toparlama yoktur.

Aynı öğrencinin farklı açılışlardaki kredi/saat yarışı ve sahiplik/danışman ataması
TOCTOU çözülmedi. Açılış kilidi tüm hesap/öğrenci işlemlerini seri hale getirmez.
Harici servis çağrısı transaction içinde yoktur. Observer commit/yayın boşluğu sürer.

## Doğrulama ve komutlar
    dotnet restore
    dotnet build --no-restore
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-course-writes.ps1 -Mode drop-placement
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-course-writes.ps1 -Mode full
    dotnet tool run dotnet-ef database update
    dotnet tool run dotnet-ef migrations has-pending-model-changes

scripts/DropPlacementChecks.cs: gerçek izole SQL/HTTPS; aday çeşitleri, 32'yi aşan
sayfa, sıra, Draft/yeni/terminal/aktif, çift/farklı drop ve drop-submit için ayrı
context'leri açılış kilidinden hemen önce süreli TCS ile buluşturma; HTTP iptal
yarışı, approve sıraları, ara SQL hatası ve yazma sonrası cancellation rollback.
Uzun Task.Delay ile sıra tahmini yoktur. Her sonucun sayacı=aktif sayısı ve doğru
aday/durumları yeni context ile incelenir.
scripts/StateChecks.cs ve SummaryChecks.cs Dropped sözleşmesini kapsar.
Gerçek sonuçlar progress kaydındadır; crash/ağ kopması deneyi yapılmış sayılmaz.

Migration yalnız status CHECK'i genişletir; veri dönüştürmez. Dropped satırlar
varken Down eski CHECK'i kabul edemez: satır silme/dönüştürme otomatik yapılmaz.
Down uygulanmadı. App hedefi doğrulanır, lab/test hedefleri ayrıdır.
