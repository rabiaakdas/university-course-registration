# Gün 15 — Birleşik kurallar ve açıklanabilir uygunluk

Zorunlu kurallar mantıksal AND'dir: hepsi geçmelidir. OR alternatiflerden birinin
geçmesi, NOT bir koşulun tersidir; bugün bu operatörler için genel birleşim
framework'ü yoktur. `a && b && c` kısa devre yapıp sonraki hesapları atlayabilir.
`AcademicEligibility.Evaluate` üç koşulu bağımsız değerlendirip bütün retleri
toplar. Önceki günün akademik fail-fast tercihi bilinçli olarak kaldırılmıştır.
Varlık, istek yapısı ve mükerrer kayıt hataları hâlâ erken rettir: eksik açılışın
kredisini/saatini uydurmayız; DB hatasını uygunsuzluk diye gizlemeyiz.

## Kod ve sorumluluk

- `Models/CourseOfferingTimeSlot.cs`, `Data/UniversityDbContext.OnModelCreating`:
  sequence/default kimlik, zorunlu FK/NO ACTION, gün 0..6, `StartsAt < EndsAt`,
  birebir dört alanlı unique index. `DayOfWeek` sayısı: Pazar=0, Pazartesi=1, …
  Cumartesi=6. Gün adı metin olarak saklanmaz.
- `Specifications/WeeklyTimeSlot.cs`: kontrollü, get-only yerel saat değeri;
  tek ortak formül `a.Start < b.End && b.Start < a.End`, aynı gün koşuluyla.
  `[09,10)` ve `[10,11)` çakışmaz. Eşit/ters/gece yarısını aşan aralık yoktur.
- `Application/EnrollmentOperations.EvaluateAsync`: yalnız istenen derslerin
  ön koşulları, ilgili öğrencinin başarıları ve ilgili dönem yükleri; SQL
  JOIN/GROUP BY/SUM(bigint) korunur. İki ek toplu saat sorgusu: seçilen açılışlar
  ve öğrencinin ilgili dönemlerdeki mevcut kayıtları. Filtre/projection SQL'de,
  bütün tablolar indirilmez. Aynı scoped context'te sıralı await ve token iletimi.
- `Application/AcademicEligibility.cs`: DB/HTTP bilmeyen saf değerlendirme;
  önceki iki specification yeniden kullanılır. `OfferingSchedule` saatleri
  savunmacı kopyalar. Hazırlanmış listeler çağrı sırasında kullanılır, saklanmaz.
- `Application/AcademicRejection.cs`: ayrık ret tipleri, kararlı code/message ve
  gerekli kimlikler. `AcademicEvaluation` listeyi kopyalayıp salt okunur sunar;
  IsEligible yalnız bu listenin boşluğundan türetilir.
- `Endpoints/AcademicRejectionHttp.cs`: akademik sonuçları HTTP'ye çevirir.
  Önizleme ile batch `BatchEnrollmentEndpoint` içindeki aynı yapı doğrulamasını
  ve `EnrollmentOperations.EvaluateBatchAsync` yolunu kullanır. Önizlemede Add
  veya SaveChanges yoktur; gerçek kayıt yeniden sorgular. Tekli kayıt da aynı
  akademik değerlendirmeyi kullanır. Ret yoksa önceki tek SaveChanges sınırı sürer.

## Çiftler ve sıra

Ön koşul retleri istek sırası; kredi retleri dönemlerin ilk görünme sırası.
Sonra her yeni seçim için mevcut açılışlar Id artan, ardından istekteki sonraki
yeni seçimler karşılaştırılır. Yeni–yeni yalnız i<j. Farklı dönemler elenir;
mevcut–mevcut taranmaz. Standartlaştırılmış Id çifti kümesi aynı çiftin birden
fazla günde çakışmasını tek nedene indirir; çıktı rollerinde yeni seçim veya
istekte önce gelen seçim korunur. SQL sırasına güvenilmez.

## Hassasiyet ve sınırlar

SQL Server `time(7)` 100ns çözünürlüktedir; bu bir TimeOnly tick'idir.
[Microsoft SQL time belgesi](https://learn.microsoft.com/en-us/sql/t-sql/data-types/time-transact-sql?view=sql-server-ver17)
ve [EF'nin TimeOnly desteği](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-8.0/whatsnew#dateonlytimeonly-supported-on-sql-server)
esas alındı. Test, iki bitişik tick ile günün minimum/maksimum değerlerini gerçek
sağlayıcıdan geri okuyarak eşitliği kontrol eder. Daha düşük time hassasiyeti
değerleri yuvarlayabilir; burada precision düşürülmez, ek dönüştürücü yoktur.

Saatler haftalık üniversite yerel saatleridir; timezone, tatil ve haftalık istisna
yoktur. Saat yoksa yalnız **bilinen aralıklarla çakışma yok** denir; programın
eksiksizliği garanti değildir. Unique index birebir tekrarı engeller, farklı
örtüşen aralıkları engellemez (SQL deneyinde ayrıca gösterilir).

Güncel Course.Credits kullanılır; geçmiş yük değerleri sonradan değişebilir.
Enrollment başarı kaydı değildir. Önizleme rezervasyon/onay değildir. Okuma ile
yazma arasındaki veya iki eşzamanlı istek arasındaki kredi/saat yarışları henüz
çözülmez. Atomik SaveChanges bu akademik yarışları tek başına engellemez.
Kontenjan, yetki, tarihsel kredi veya genel takvim altyapısı eklenmedi.

## Doğrulama

`scripts/EligibilityChecks.cs`: saf aralık/sonuç; gerçek HTTP üçlü ret, deterministik
çiftler, önizleme sonrası değişen program, DB gidiş-dönüşü, dar SQL hata kontrolleri
ve gerçek komut interceptor'ı. `SpecificationChecks.cs` önceki koşulları yeni
failures sözleşmesiyle korur. `EnrollmentChecks.cs` ve `BatchEnrollmentChecks.cs`
gerçek yazma yarışlarını korur; artık sekiz toplu SELECT bekler. Bu sabit okuma
sayısı INSERT komutlarının sağlayıcı batching biçimine dair iddia değildir.
`sql/app/verify-time-slots.sql` metadata; `time-slot-example.sql` yalnız işaretli
izole test DB'sinde sentetik, her zaman rollback edilen örnektir.
