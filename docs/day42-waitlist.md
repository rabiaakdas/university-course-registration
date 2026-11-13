# Gün 42 — Bekleme listesi ve deterministik sıra

## HTTP sınırı
Bütün yollar Identity application cookie, Student rolü ve route'taki Student.UserId
sahipliği ister. Advisor/StudentAffairs için bypass yoktur. Kimlik yok 401, rol
yok 403, bulunmayan veya sahibi olunmayan kaynak 404. POST'lar mevcut
X-CSRF-TOKEN doğrulamasını kullanır. Bearer tek başına yeterli değildir.

| İşlem | Route | Başarı |
|---|---|---|
| Katıl | POST /students/{studentId}/waitlist; JSON courseOfferingId | 201 + Location + kendi başvurusu |
| Oku | GET /students/{studentId}/waitlist/{entryId} | 200, kendi başvuru/pozisyonu |
| İptal | POST /students/{studentId}/waitlist/{entryId}/cancel | 204 |

Gövdede StudentId/UserId/Status/QueueNumber kabul edilmez. Başvurunun gerçek
StudentId ilişkisi ve route birlikte filtrelenir.
Pozitif olmayan courseOfferingId 400, olmayan açılış 404.
Submitted/Approved/Rejected enrollment 409 waitlist_enrollment_exists;
Draft izinlidir. O anda OccupiedSeats < Capacity ise 409
waitlist_capacity_available. İkinci Waiting başvurusu gerçek filtered UNIQUE
üzerinden 409 waitlist_already_waiting olur. Beklenmeyen DB hataları 409'a çevrilmez.

Waiting→Cancelled koşullu UPDATE ile yapılır. Kendi Cancelled satırını tekrar
iptal 204, ilk kapanış zamanı/nedeni değişmez. Placed/Ineligible 409
waitlist_closed; bu durumlara geçiş bugün üretimde yoktur. Yeniden katılma eski
satırı açmaz: yeni satır ve yeni numara alır. Kapanmış satırın position alanı null.

## Sıra ve pozisyon
WaitlistQueueNumbers SQL Server bigint sequence, QueueNumber için DB default'tur.
Global benzersiz numara açılışlar arasında da artar; FIFO tanımı her açılışta
Waiting satırlarının QueueNumber artan sırasıdır. Tıklama zamanı, JoinedAtUtc veya
commit sırası garantisi değildir. Rollback ve sequence tahsisi boşluk bırakabilir;
numara boşlukları hata değildir. MAX+1 kullanılmaz.

QueueNumber sabit sıra anahtarıdır; position aynı açılışta Waiting olup numarası
bu satırdan küçük/eşit olanların sayısıdır. Önceki başvuru iptal olunca pozisyon
değişir. Tek SQL projection içindeki korelasyonlu COUNT hesaplar; diğer
öğrencilerin kimliği/başvurusu dışarı çıkmaz, N+1 veya tüm tabloyu belleğe alma yok.
Okuma gelecekteki sıra/yer garantisi değildir.

## Veri ve koordinasyon
Models/WaitlistEntry.cs; Data/UniversityDbContext.cs:
- Öğrenci/açılış FK NO ACTION, pozitif benzersiz QueueNumber.
- UNIQUE(StudentId,CourseOfferingId) yalnız Status=Waiting (0) filtresiyle.
- Waiting için kapanış alanları null; diğer tanımlı durumlarda kapanış zamanı
  JoinedAtUtc'den önce olamaz ve boş olmayan sınırlı neden gerekir.
- Status/kapanış/numara public setter ile değiştirilemez; HTTP DTO yalnız açılış alır.

Endpoints/WaitlistEndpoints.cs küçük HTTP akışını mevcut Accounts/StudentAccess.cs
ve CatalogAntiforgeryFilter ile birleştirir. Data/RegistrationWriteErrors.cs yalnız
beklenen tablo+indeks SQL2601'i duplicate olarak sınıflandırır.

Katılım enrollment, OccupiedSeats, aktif kredi/program veya event üretmez.
Kontenjan/enrollment okuması ile INSERT arasında değişiklik olabilir. İptal
koşullu tek satır yazmasıdır; katılımı submit/drop ile birlikte seri hale getiren
kilit/transaction yoktur. Sahiplik TOCTOU sınırı da devam eder. Bunların
koordinasyonu 43. güne kalır; bugün otomatik yerleştirme/job/drop/bildirim yoktur.

## Çalıştırma
    dotnet restore
    dotnet build --no-restore
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-course-writes.ps1 -Mode waitlist
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-course-writes.ps1 -Mode full
    dotnet tool run dotnet-ef database update
    dotnet tool run dotnet-ef migrations has-pending-model-changes

Migration yalnız doğrulanmış app DB hedefinde, uygulama yazmıyorken uygulanır.
Testler kendi izole LocalDB/HTTPS sertifikasını kullanır; app/lab fixture hedefi
değildir. scripts/WaitlistChecks.cs gerçek paralel HTTP, sıra/pozisyon, iptal/
yeniden katılım, retler, SQL FK/filtered UNIQUE/CHECK ve kayıt/sayaç değişmezliğini
sınar. CookieSessionChecks/JwtChecks geçerli bearer'ın üç yolu açamadığını sınar.
Gerçek koşu sonuçları docs/progress.md'dedir; test kodunun varlığı testin geçtiği
anlamına gelmez. Sequence boşluğu, commit sırası veya yerleştirme garantisi test
beklentisi yapılmaz.
