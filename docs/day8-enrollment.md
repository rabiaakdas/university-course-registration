# Gün 8 — Çoktan çoğa ilişkiyi açık entity ile temsil etmek

Student ile CourseOffering arasındaki çoktan çoğa ilişki iki bire çok ilişkiye
ayrılır: Student → Enrollments ve CourseOffering → Enrollments. Enrollment'ın
her satırı iki mevcut kaydı bağlar. CreatedAtUtc öğrenci veya dersin değil, bu
bağlantının oluşturulma zamanıdır. Bu nedenle ilişki açık bir modeldir.

Ayrı Enrollment.Id detay URL'sini ve kayıt sıralamasını basitleştirir; ayrıca
UNIQUE(StudentId, CourseOfferingId) tekrar çifti engeller. Alternatif iki FK'den
birleşik PK kullanmaktır: ayrı kimlik/sequence gerekmezdi fakat tek sayısal kayıt
adresi olmazdı. Seçimin maliyeti ayrı PK ve unique index'tir.

FK değerlerini atamak mevcut öğrenci/açılışı yeniden Add etmek değildir. Endpoint
üst kayıtları AnyAsync ile kontrol eder, yeni ilişki nesnesini Add eder ve
SaveChangesAsync çağırır. Kontrollerin ardından üst kayıt kaybolabilir veya rakip
çift eklenebilir; gerçek FK/unique kısıtları ve dar hata eşlemesi bu aralığı ele alır.
Hatalı context'le kör retry yoktur. Bütün DB işlemleri aynı request context'inde
sırayla await edilir; CancellationToken iletilir.

UNIQUE anahtarında CourseId değil CourseOfferingId vardır. Aynı dersin A/B
şubeleri farklı açılışlar olduğundan ikisine kayıt bugün mümkündür. Kontenjan,
ön koşul ve akademik kararlar bu kısıtın verdiği garantiler değildir. Student
iş profilidir; kimlik doğrulanan hesap değildir. StudentId almak yetkilendirme
sağlamaz; bugün self-servis güvenliği kurulmamıştır.

Kod karşılıkları:

- `Models/Student.cs`, `Models/Enrollment.cs`: profil ve açık ilişki entity'si.
- `Data/UniversityDbContext.cs`: iki bire çok FK, NO ACTION, unique ve sequence.
- `Endpoints/StudentEndpoints.cs`: oluşturma, detay ve üç SQL JOIN ile kayıt listesi.
- `Endpoints/EnrollmentEndpoints.cs`: FK kontrolü, sunucu UTC zamanı, Add/SaveChanges.
- `Data/RegistrationWriteErrors.cs`: sadece ilgili sağlayıcı hata numarası/nesnesi.
- `scripts/EnrollmentChecks.cs`: birden çoğa iki yönde ilişki, gerçek yarış,
  NO ACTION, server zamanı ve sabit iki okuma sorgusu kanıtı.

Resmî kaynaklar: [açık ilişki entity'si ve ilişkiye ait alanlar](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/many-to-many),
[sequence eşlemesi](https://learn.microsoft.com/en-us/ef/core/modeling/sequences).
