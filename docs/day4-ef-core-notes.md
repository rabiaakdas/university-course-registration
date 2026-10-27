# Gün 4 — EF Core, context, mapping ve migrations

- **DbSet yüklenmiş liste değildir.** `UniversityDbContext.Courses` sorgulanabilir
  model girişidir. `Program.cs` `/courses` handler'ındaki OrderBy/Select sorguyu
  kurar; `await ToListAsync(cancellationToken)` onu SQL olarak çalıştırıp sonuçları
  materialize eder. Sadece id/code/name seçilir; entity izlemeye ihtiyaç yoktur.
  GET okumasında SaveChanges gerekmez ve çağrılmaz.
- **Scoped yaşam süresi:** `Program.cs` AddDbContext kaydı varsayılan scoped'dur.
  Her HTTP request kendi context'ini DI'den alır; scope sonunda DI dispose eder.
  Endpoint context'i elle dispose etmez, static/singleton tutmaz, arka plana taşımaz.
  DbContext thread-safe değildir; aynı örnekte paralel DB işlemi başlatılmaz.
  Asenkron çağrılar tamamlanmadan yeni işlem başlatmak yerine sırayla await edilir.
  8 eşzamanlı HTTP isteği farklı request scope'larıdır; aynı context'te paralellik
  veya ders kaydı/kapasite yarışı çözümü anlamına gelmez.
- **Mapping ve gerçek şema farklıdır.** `Data/UniversityDbContext.cs` içindeki
  OnModelCreating EF modelini tanımlar. DB ancak migration uygulandıktan sonra
  değişir. `sql/app/verify.sql` gerçek sys catalog bilgilerini kontrol eder;
  sadece C# koduna bakarak DB kısıtlarının var olduğu varsayılmaz.
- **Migration/snapshot:** EF güncel modeli son snapshot ile karşılaştırarak
  migration üretir; canlı DB'yi otomatik olarak modelle karşılaştırıp düzeltmez.
  `Data/Migrations` Up, Down, designer ve snapshot dosyaları sürümlenir.
  `migrations has-pending-model-changes` model/snapshot farkını; ikinci
  `database update` uygulanmamış migration bulunmadığını gösterdi.
- **Down yedek değildir.** İlk migration'ın Down'ı tabloları kaldırır ve veri
  kaybettirir; bugün çalıştırılmadı. Migration geçmişine elle kayıt yazılmadı.
- **Açık kimlik:** Üç Id alanı ValueGeneratedNever'dır. PK benzersizliği sağlar,
  numara üretmez; `sql/app/seed.sql` kimlikleri açık verir.
- **Benzersizlik:** HasIndex/IsUnique SQL Server unique index üretir. UQ constraint
  de unique index ile desteklenir ama nesne ve hata sınıfı farklıdır (2627/2601).
  Yalnızca benzersizlik gerektiği için alternate key yerine unique index seçildi.
- **Geçici veri kaynağı ayrımı:** Courses DB'den, diğer endpoint'ler bellekten.
  `offeringCourseSamples` sadece açılış yanıtını zenginleştirir. DB'de ders adı
  değişikliği diğer endpoint'in adını henüz değiştirmez; bu 4. gün sınırıdır.

## Kod ve deney bağlantısı

`sql/shared/verify-constraints.sql` tek ortak davranış kontrolü; `sql/app/verify.sql`
uygulama metadata kontrolüdür. `scripts/verify-http.ps1 -VerifyDatabaseSource`
başka bağlantının uncommitted değişikliği okuyacağını varsaymaz: geçici SQL adı
commit eder, HTTP'den okur, finally ile eski değeri karşılaştırarak geri yükler.
Sonraki HTTP, yeniden başlatma ve eşzamanlı istek kontrolleri gerçek DB'de geçti.

Kaynaklar: [DbContext yaşam süresi/thread-safety](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/),
[CLI migration komutları](https://learn.microsoft.com/en-us/ef/core/cli/dotnet),
[EF Core 10](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew).
