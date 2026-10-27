# Gün 5 — Girdi, iş kuralı, kısıt ve HTTP hatası

- **Binding:** ASP.NET JSON'u CreateCourseRequest/UpdateCourseRequest'e çevirir.
  Bozuk JSON ve yanlış tür handler'a ulaşmadan 400 olur. Program.cs strict sayı
  ayarıyla sayısal metinleri de reddeder. Request entity değildir; Id içermez.
- **Input validation:** `Endpoints/CourseInput.Validate` Code Trim/ToUpperInvariant,
  Name Trim uygular; zorunluluk, uzunluk ve pozitif krediyi bağımsız toplar.
  ValidationProblem alanları 400'dür. Geçersiz POST için DB sorgusu yapılmaz.
- **İş kuralı:** Ders kodu kullanılmamalı. `CourseEndpoints.CreateAsync`/UpdateAsync
  AnyAsync ön kontrolü yapar. Güncelleme `c.Id != id` ile kendisini dışlar.
  Ön kontrol kodu ayırmaz; başka context kontrol ve SaveChanges arasına girebilir.
- **DB constraint:** Unique index son korumadır. `CourseWriteErrors.IsDuplicateCode`
  2601 + doğru tablo/index tanımlayıcılarını arar. SqlError'da ayrı index adı alanı
  olmadığından mesajın tırnak sırasına sınırlı bağımlılık vardır; bilinmeyen biçim
  fail-closed davranır. Tüm DbUpdateException'lar veya tüm unique hatalar 409 değildir.
- **Add/SaveChanges:** Add nesneyi Added durumuna alır; henüz DB kaydı değildir.
  SaveChangesAsync başarılı olunca DB sequence kimliği entity'ye gelir ve 201
  Created döner. Location detay GET adresidir. GET için SaveChanges gerekmez.
- **Tracked update:** PUT önce kaydı bulur (yoksa 404), sonra doğrulama/çakışma
  kontrolü yapar. Kontroller geçmeden nesne değişmez. Sonra Code/Name/Credits set
  edilir ve SaveChangesAsync çağrılır; Add/Update yoktur, Id aynı kalır.
  Course.Id init kaldı; yalnızca düzenlenebilir alanlar basit set oldu.
- **HTTP ayrımı:** Oluşturma 201, detay/güncelleme/liste 200; alan/binding 400,
  olmayan ders 404, gerçek kod çakışması 409, beklenmeyen sistem hatası 500.
  Program.cs merkezi hata mekanizması genel Problem Details verir, yalnızca
  exception türü/trace loglar. İptaller 400/409'a çevrilmez.
- **Yanıt seçimi:** Liste eski id/code/name sözleşmesini korur; detay/POST/PUT
  id/code/name/credits döndürür. Entity'yi doğrudan bind/serialize etmek zorunlu değil.
- **Kimlik:** `UniversityDbContext.OnModelCreating` Course.Id için sequence/default
  ve ValueGeneratedOnAdd kullanır. GenerateCourseIds migration'ı mevcut gerçek
  maksimum kimliğe göre başlangıç seçer. Bu bakım işlemi tüm yazıcılar dururken
  yapılır; runtime kimlik tahsisi SQL Server sequence'indedir. Boşluksuzluk yoktur.

## Kanıt ve sınırlar

`scripts/CourseWriteChecks.cs` gerçek izole LocalDB ve gerçek HTTP kullanır.
SaveChangesInterceptor sadece integration context'inde rakip ekler; production'a
test dalı konmaz. Gerçek kod hatası 409; gerçek PK/başka index hataları classifier'da
false; beklenmeyen CHECK HTTP'de güvenli 500 olarak doğrulandı. Aynı kodla paralel
POST tek başına catch dalı kanıtı değildir. Ayrı request'ler ayrı scoped context
kullanır; tek context içinde Task.WhenAll yoktur. Lost update/optimistic concurrency
henüz çözülmedi. Kaynak ayrımı sürüyor: dersler DB, dönem/açılış listeleri bellek.

Kaynaklar: [SQL Server 2601](https://learn.microsoft.com/en-us/sql/relational-databases/replication/mssql-eng002601),
[EF sequence mapping](https://learn.microsoft.com/en-us/ef/core/modeling/sequences),
[SQL sequence ve boşluklar](https://learn.microsoft.com/en-us/sql/t-sql/statements/create-sequence-transact-sql),
[ASP.NET hata yanıtları](https://learn.microsoft.com/en-us/aspnet/core/web-api/handle-errors).
