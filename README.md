# Üniversite Ders Kayıt ve Kontenjan Yönetim Sistemi

106 öğrenme birimlik programın ilk otuz bir gün uygulaması: HTTP istek akışını görmek,
ders/dönem/açılış ayrımını temsil etmek ve ayrı SQL laboratuvarında ilişkisel
şema ile kısıtları öğrenmek; EF Core ile dersleri okumak, doğrulayarak oluşturmak ve düzenlemek.
Proje tek bir ASP.NET Core Minimal API'dir. Güncel durumda bütün listeleme
endpoint'leri DB kullanır; dönem ve açılış oluşturma da desteklenir.
Güncel kayıt akışında doğrudan ön koşullar, dönemlik kredi sınırı ve bilinen saat çakışmaları birlikte değerlendirilir.
Önceki gün başlıkları o günkü kapsamı anlatır; akademik failures sözleşmesi 15., güncel durum ve yük politikası 16. gün bölümündedir.

## Ön koşullar ve çalıştırma

.NET 10 LTS kullanılır; doğrulanan SDK sürümü **10.0.401**, hedef framework
`net10.0`'dır. `global.json` aynı özellik bandındaki kararlı düzeltme sürümlerine
izin verir. EF Core SQL Server/Design ve repo içi `dotnet-ef` sürümü **10.0.14**.
LocalDB ve `sqlcmd` gereklidir. İlk çalıştırmadan önce aşağıdaki **4. gün kurulum**
adımlarıyla uygulama DB'sine migration ve seed uygulayın.
[Microsoft destek bilgisi](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support)
.NET 10'un Kasım 2028'e kadar desteklendiğini belirtir.

PowerShell ile proje kökünden çalıştırın:

```powershell
cd C:\Users\rabia\OneDrive\Desktop\universite
dotnet restore .\UniversityRegistration.csproj
dotnet build .\UniversityRegistration.csproj --no-restore
dotnet run --project .\UniversityRegistration.csproj --no-build --urls http://127.0.0.1:5080
```

Uygulamayı durdurmak için `Ctrl+C` kullanın.

## Endpoint

```powershell
curl.exe -i http://127.0.0.1:5080/courses
```

Beklenen durum: `200 OK`. Content-Type: `application/json` (charset eki olabilir).
Kimlik doğrulama gerekmez. Beklenen JSON:

```json
[
  { "id": 1, "code": "CSE201", "name": "Veri Yapıları" },
  { "id": 2, "code": "CSE203", "name": "Veritabanı Yönetimi" }
]
```

6. günde `/courses`, `/academic-terms` ve `/course-offerings` uygulama DB'sinden
okunur; sabit bellek listeleri kaldırıldı.
3. gün laboratuvarı ayrı kalır; API ona bağlanmaz.

## İstek akışının kod karşılığı

`Program.cs`, `CourseEndpoints.MapCourseEndpoints` ile ders rotalarını kaydeder.
Endpoint'in uygulama işlemi ders listesini veritabanından okumaktır.
`Results.Ok`, 200 durum kodlu başarılı JSON yanıtını oluşturur.
`UniversityDbContext`, DI üzerinden request kapsamında alınır. `OrderBy` ve
`Select` sorguyu tanımlar; `ToListAsync(cancellationToken)` SQL'i çalıştırır.
Anonim yanıtta yalnızca id/code/name seçilir; `Credits` yanıta eklenmez.

## 1. gün değişiklik ve doğrulama notu

- Tek API ve `/courses` endpoint'i eklendi; şablonun Hello World endpoint'i ve
  kullanılmayan ayar/başlatma dosyaları kaldırıldı.
- `dotnet restore` başarılı; `dotnet build --no-restore` başarılı (0 uyarı, 0 hata).
- Derlenen uygulama gerçekten başlatıldı ve `Invoke-WebRequest` ile kimlik
  doğrulama bilgisi olmadan `GET /courses` çağrıldı.
- Durum kodu 200, Content-Type `application/json; charset=utf-8` olarak doğrulandı.
- `System.Text.Json.JsonDocument` ile kökün dizi olduğu, tam iki ders bulunduğu,
  yalnızca `id`, `code`, `name` alanlarının olduğu, alan tipleri ve değerlerinin
  beklenenle eşleştiği, Türkçe karakterlerin doğru çözümlendiği kontrol edildi.
- Geçici uygulama süreci kontrol sonunda kapatıldı. Ayrı test projesi eklenmedi.

Karar kaydı: [0001](docs/adr/0001-simple-api-start.md).
Öğrenme ve açık noktalar: [İlerleme](docs/progress.md).

## 2. gün: Ders, dönem ve açılış

Bu bölüm 2. günün tarihsel durumudur; güncel akış 6. gün bölümündedir.

`Models/Course.cs` dersin kimliğini, kodunu, adını ve kredisini tutar.
`Models/AcademicTerm.cs` dönemin kimliğini, adını ve `DateOnly` tarihlerini tutar.
`Models/CourseOffering.cs` bir dersin belirli dönem ve şubedeki açılışıdır.
Kontenjan (`Capacity`) açılışa aittir; öğrenci ileride `CourseOffering`'e kaydolacak.
Her açılış ayrı `Id` alır. `CourseId` ve `AcademicTermId` ilgili kayıtları gösterir.
Ders ve dönem adları açılış modeline kopyalanmaz; yanıtta kimlik üzerinden `Single`
ile bulunur. Liste sırasına bağlı olmayan bu LINQ işlemleri bellekte çalışır;
SQL veya EF Core sorguları değildir.

C# kimlik alanları tek başına ilişki bütünlüğü sağlamaz. Örnek verilerin
tutarlılığı HTTP betiğiyle kontrol edilir; eksik veya tekrar eden ilişkili kayıt
`Single` eşleştirmesinde hata verir. `init`, oluşturma sonrası atamayı sınırlar;
geçerli kimlik, pozitif kapasite veya doğru tarih aralığı garantisi sağlamaz.
Bugün kapsamlı doğrulama altyapısı yoktur. Veriler hâlâ sabit/bellekte, DB yoktur.

### GET /academic-terms

```powershell
curl.exe -i http://127.0.0.1:5080/academic-terms
```

```json
[
  { "id": 1, "name": "2026-2027 Güz", "startsOn": "2026-09-21", "endsOn": "2027-01-15" },
  { "id": 2, "name": "2026-2027 Bahar", "startsOn": "2027-02-15", "endsOn": "2027-06-11" }
]
```

Tarihler örnek takvim tarihleridir, resmî üniversite takvimi değildir.

### GET /course-offerings

```powershell
curl.exe -i http://127.0.0.1:5080/course-offerings
```

```json
[
  { "id": 1, "courseId": 1, "courseCode": "CSE201", "courseName": "Veri Yapıları", "academicTermId": 1, "termName": "2026-2027 Güz", "sectionCode": "A", "capacity": 40 },
  { "id": 2, "courseId": 1, "courseCode": "CSE201", "courseName": "Veri Yapıları", "academicTermId": 1, "termName": "2026-2027 Güz", "sectionCode": "B", "capacity": 30 },
  { "id": 3, "courseId": 1, "courseCode": "CSE201", "courseName": "Veri Yapıları", "academicTermId": 2, "termName": "2026-2027 Bahar", "sectionCode": "A", "capacity": 35 }
]
```

Her iki endpoint de kimlik doğrulaması olmadan `200 OK` ve `application/json`
döndürür. JSON'da charset eki ve Türkçe karakterler için Unicode escape geçerlidir.

### 2. gün doğrulaması

PowerShell 7 ile proje kökünden:

```powershell
dotnet restore .\UniversityRegistration.csproj
dotnet build .\UniversityRegistration.csproj --no-restore
pwsh -File .\scripts\verify-http.ps1
```

Betik derlenen uygulamayı 5081 portunda başlatır, üç endpoint'e gerçek HTTP
istekleri gönderir ve başarısız koşulda hata verir. Gerekirse `-Port 5082` kullanın.
Başlattığı süreci başarılı/başarısız kontrolün sonunda kapatır.

2. gün restore ve build başarılı (0 uyarı, 0 hata). HTTP betiği üç endpoint için
200 ve `application/json; charset=utf-8` doğruladı. `/courses` alanları tam olarak
`id/code/name`; iki dersin değerleri korundu. Dönem tarihleri, açılışların kimlik
eşleşmeleri, benzersiz şubeleri, 40/30 kapasiteleri ve ikinci dönem açılışı geçti.
Geçici süreç kapandı. Ek test projesi veya NuGet paketi eklenmedi.

Karar kaydı: [0002](docs/adr/0002-course-term-offering-separation.md).

## 3. gün: Ayrı SQL laboratuvarı

**3. gün sonunda** API bellekteydi. **4. günde yalnızca `/courses` ayrı uygulama
DB'sine taşındı.** Aşağıdaki SQL laboratuvarı API'nin veri kaynağı değildir.

Seçilen ve gerçek deneylerde kullanılan sunucu: SQL Server LocalDB
`(localdb)\MSSQLLocalDB`, sürüm **17.0.4025.3**, Express Edition (64-bit).
PostgreSQL 18.6 istemcisi bulundu ancak localhost:5432 yanıt vermedi; erişilebilir
LocalDB tercih edildi. Yazılım kurulmadı. Laboratuvar veritabanı:
`university_registration_lab`; tablolar `dbo` şemasındadır.

Gerekli araçlar: SQL Server LocalDB, ODBC `sqlcmd` istemcisi (bu ortamda ODBC
Driver 17) ve komutlar için PowerShell. `-E` Windows kimlik doğrulamasıdır;
parola veya bağlantı secret'ı dosyaya/komuta yazılmaz. LocalDB kullanıcıya ait
olduğundan sunucuya erişebilen aynı Windows hesabıyla çalıştırın.

### Oluşturma ve çalıştırma sırası

Komutları proje kökünden çalıştırın (`:r` yolları bu köke göredir).
`-b` SQL hatasında sıfırdan farklı çıkış kodu, `-f 65001` UTF-8 dosya okuma sağlar.
Her adım başarısızsa sonraki adıma geçmeyin.

```powershell
cd C:\Users\rabia\OneDrive\Desktop\universite
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -b -f 65001 -i sql/00-create-lab.sql
if ($LASTEXITCODE -ne 0) { throw 'Laboratuvar oluşturulamadı; mevcut içeriği inceleyin.' }
foreach ($file in @('sql/01-schema.sql', 'sql/02-seed.sql', 'sql/03-verify.sql', 'sql/04-join.sql')) {
    sqlcmd -S '(localdb)\MSSQLLocalDB' -E -d university_registration_lab -b -f 65001 -W -i $file
    if ($LASTEXITCODE -ne 0) { throw "SQL adımı başarısız: $file" }
}
```

- `00-create-lab.sql`: Ad zaten varsa durur; silmez veya yeniden kullanmaz.
  Yeni DB'ye `UniversityRegistrationLab=day3-v1` işareti koyar.
- `guard-lab.sql`: Yazma/okuma betiklerinde hedef DB adını ve laboratuvar işaretini
  kontrol eder. Bu işaret bir güvenlik sınırı değil, yanlış hedefe karşı kontroldür.
- `01-schema.sql`: Üç tabloyu bir transaction içinde oluşturur. Mevcut tabloları
  `IF NOT EXISTS` ile atlamaz; yeniden çalıştırma hata verir.
- `02-seed.sql`: İki ders, iki dönem ve üç açılışı tek transaction ile ekler.
  Kimlikler açıktır; yeniden seed eklemek PK hatası verir, çoğaltma/upsert yapmaz.
- `03-verify.sql`: Tam seed içeriğini ve kısıt deneylerini doğrular. Her deney ayrı
  transaction'da çalışır ve rollback yapılır. Beklenen hatalar numara ve ilgili
  kısıt adıyla; NOT NULL hataları 515 ve sütun adıyla kontrol edilir.
- `04-join.sql`: Ders/dönem/açılış ilişkilerini üç satırlık sonuçta gösterir.

### Tekrar çalıştırma ve kontrollü temizlik

Bu çalışma sırasında laboratuvar zaten oluşturuldu. Doğrulama ve JOIN'i tekrar
çalıştırmak için yalnızca `03-verify.sql` ve `04-join.sql` komutlarını kullanın.
Doğrulama temel verileri değiştirmez; iki ardışık çalıştırmayla doğrulandı.

Ad önceden kullanılıyorsa otomatik temizlik yapmayın. Önce sahibi ve içeriği inceleyin:

```powershell
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -W -Q "SELECT name,SUSER_SNAME(owner_sid) AS owner FROM sys.databases WHERE name=N'university_registration_lab';"
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -d university_registration_lab -W -Q "SELECT name,CONVERT(nvarchar(128),value) AS value FROM sys.extended_properties WHERE class=0; SELECT SCHEMA_NAME(schema_id) AS schema_name,name FROM sys.tables;"
```

**Yalnızca bu laboratuvara ait üç tablonun verilerini silip yeniden kurmak
istiyorsanız**, içerik/sahiplik incelemesinden sonra aşağıdaki isteğe bağlı komutu
kullanın. Bu komut bugün çalıştırılmadı; seed verileri DB'de korundu.

```powershell
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -d university_registration_lab -b -f 65001 -i sql/90-clean-lab-tables.sql
```

Betik yalnızca `dbo.CourseOfferings`, `dbo.AcademicTerms`, `dbo.Courses` tablolarını
çocuk-üst sırasıyla kaldırır; DB'yi veya diğer nesneleri silmez, CASCADE kullanmaz.
Sonrasında 01 → 02 → 03 → 04 sırasını kullanın; 00'ı yeniden çalıştırmayın.
İşaret yoksa veya alan başka çalışmaya aitse bu dosyalarla üzerine yazmayın.

### Kurallar ve gerçek sonuçlar

PK'ler açık `int` değerlerdir, IDENTITY değildir. Kapasitenin DEFAULT'u yoktur.
FK'ler `ON DELETE NO ACTION ON UPDATE NO ACTION` ile bağlı üst kayıtların
silinmesini ve kimliklerinin değiştirilmesini engeller. Code benzersizdir;
`(CourseId, AcademicTermId, SectionCode)` birleşimi de benzersizdir.
Kredi/kapasite pozitif; dönem bitişi başlangıçtan önce olamaz. Zorunlu sütunlar
NOT NULL'dır. SQL Server'da NOT NULL ayrı adlandırılmış constraint değil sütun
özelliğidir; PK/FK/UNIQUE/CHECK kısıtlarının açık adları vardır.

Gerçek DB collation'ı `SQL_Latin1_General_CP1_CI_AS`: büyük/küçük harfe duyarsız,
aksanlara duyarlıdır. Dolayısıyla `CSE201` ile `cse201` UNIQUE açısından aynı
sayılır; bu davranış ayrıca denendi. Collation değiştirilmedi, otomatik trim veya
harf dönüştürme eklenmedi. Örnek kodlar büyük harfli ve baş/son boşluksuzdur.
Türkçe içerik `nvarchar` ve `N'...'` sabitleriyle korunur. `nvarchar(n)` uzunluğu
UTF-16 kod birimleriyle ölçülür; örnek Türkçe karakterlerde her harf bir birimdir.
[Unicode türleri](https://learn.microsoft.com/en-us/sql/t-sql/data-types/nchar-and-nvarchar-transact-sql?view=sql-server-ver17),
[collation](https://learn.microsoft.com/en-us/sql/relational-databases/collations/collation-and-unicode-support?view=sql-server-ver17) ve
[kısıt/silme davranışları](https://learn.microsoft.com/en-us/sql/t-sql/statements/create-table-transact-sql?view=sql-server-ver17)
Microsoft belgeleriyle kontrol edildi.

İstenen 20 davranışın tamamı gerçek DB'de doğrulandı: geçerli seed, yinelenen
PK/kod/birleşim, geçersiz FK, farklı şube/dönem kabulü, sıfır/negatif/NULL/eksik
kapasite, sıfır/negatif kredi, NULL ad, hatalı tarih, iki engellenen silme ve
kayıtların korunması, açılışsız ders, tek ad değişikliğinin bütün JOIN satırlarına
yansıması ve Türkçe metinler. Betik bunları 23 numaralı kontrol altında raporlar;
ek olarak CI kod karşılaştırmasını ve iki üst kimliğin cascade edilmediğini sınar.
Tüm kontroller iki çalıştırmada da geçti; son seed yine 2 ders/2 dönem/3 açılış.

API restore/build başarılı (0 uyarı, 0 hata); mevcut `scripts/verify-http.ps1`
üç endpoint'i gerçek HTTP ile doğruladı. Geçici API süreci kapatıldı. Engellenen
doğrulama yok; PostgreSQL üzerinde deney yapılmadı, seçilen hedef LocalDB'dir.

[Öğrenme notu](docs/day3-relational-notes.md) ·
[ADR 0003](docs/adr/0003-relational-lab.md)

## 4. gün: EF Core ile uygulama DB'si

**Güncel kaynaklar:** `/courses` → `university_registration_app`;
`/academic-terms` ve `/course-offerings` → `Program.cs` içindeki örnek koleksiyonlar.
`offeringCourseSamples` yalnızca açılış yanıtındaki ders adlarını sağlar. DB'de
ders adı değişirse açılış yanıtı bugün bu değişikliği takip etmez; bu geçici sınırdır.
`university_registration_lab` 3. gün laboratuvarı olarak korunur.

### Araçlar ve bağlantı

.NET SDK 10.0.401/net10.0, SQL Server LocalDB 17.0.4025.3 ve EF Core 10.0.14
kullanıldı. `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`
ve kökteki `dotnet-tools.json` içindeki dotnet-ef aynı sürümdedir. Design paketi
PrivateAssets=all'dır; genel dotnet araçları değiştirilmedi.

SQL istemcisinin dolaylı Pkcs 9.0.11 bağımlılığı restore sırasında NU1902/NU1903
uyarısı verdi. `System.Security.Cryptography.Pkcs` 10.0.14 doğrudan referansı
bu gerçek bağımlılık açığını gidermek içindir; son restore/build uyarısız geçti.
[Microsoft güvenlik duyurusu](https://github.com/dotnet/announcements/issues/468)
ve [EF 10/.NET 10 uyumu](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew)
kontrol edildi.

`appsettings.json` içindeki `ConnectionStrings:UniversityDatabase` parolasız
yerel geliştirme ayarıdır: mevcut MSSQLLocalDB, uygulama DB'si ve Windows kimlik
doğrulaması. Yerel geliştirme sertifikası için TrustServerCertificate kullanılır;
bu ayar üretim bağlantı politikası değildir. Tam bağlantıyı loglamayın.
Gerekirse `ConnectionStrings__UniversityDatabase` ortam değişkeniyle geçersiz
kılın; hassas değerleri repoya veya komut geçmişine yazmayın. Eksik/boş bağlantı
açık hata verir; belleğe fallback yoktur.

Tasarım zamanı EF aracı aynı `Program.cs` yapılandırmasını ve DI kaydını kullanır;
ayrı factory yoktur. Komutları proje kökünde, LocalDB sahibi Windows hesabıyla
çalıştırın. Migration öncesinde bağlantı hedefinin uygulama DB'si olduğunu kontrol
edin; laboratuvara yönlendirmeyin. Bu çalışmanın başında uygulama DB adı yoktu;
mevcut bir DB ile karşılaşırsanız önce tabloları ve `__EFMigrationsHistory`'yi inceleyin.

### Kurulum ve tekrar çalıştırma

```powershell
cd C:\Users\rabia\OneDrive\Desktop\universite
dotnet tool restore
dotnet restore
dotnet build --no-restore
# Mevcut migration'ı uygulamadan önce üretilen SQL'i inceleyin:
dotnet ef migrations script 0 --output sql/app/migrations.tmp
dotnet ef database update
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -d university_registration_app -b -f 65001 -i sql/app/seed.sql
dotnet run --project .\UniversityRegistration.csproj --no-build --urls http://127.0.0.1:5080
```

Her komutun başarılı olmasını bekleyin; hatada sonraki adıma geçmeyin.
İlk migration zaten kaynak kontrolündedir; normal kurulumda yeniden üretmeyin.
Bu çalışmada `dotnet ef migrations add InitialUniversitySchema --output-dir Data/Migrations`
ile üretildi. Migration **oluşturmak** DB'yi değiştirmez; `database update` ayrı
uygulama adımıdır. Başlangıçta otomatik migration/seed, EnsureCreated veya
EnsureDeleted yoktur. DB silme/yeniden oluşturma işlemi yapılmaz.

`sql/app/seed.sql` tek transaction ile üst kayıtları önce ekler. Beklenen kimlikte
aynı değer varsa atlar; farklı değer varsa açık hata verir ve hiçbir kaydı ezmez.
İkinci çalıştırmada yeni satır eklenmedi. Eksik seed kayıtlarını ekler; ilgisiz
mevcut kayıtları silmez. Bilerek değiştirdiğiniz seed değerinde hata almanız normaldir.

### Mapping ve migration kararı

`Data/UniversityDbContext.cs` üç DbSet ve küçük `OnModelCreating` eşlemesini içerir.
4. günde tüm kimlikler açıktı. 5. günde Course.Id sequence/default ile üretilir;
diğer iki model `ValueGeneratedNever` olarak kaldı. SQL'de IDENTITY yoktur.
Metinler nvarchar, tarihler date; CHECK/NOT NULL kuralları laboratuvarla aynıdır.
İki ilişki NoAction'dır. Üretilen SQL'de ON DELETE/UPDATE yazılmaması SQL Server'ın
NO ACTION varsayılanını kullanır; gerçek FK metadata'sı ve deneylerle doğrulandı.

Code ve ders/dönem/şube birleşimi için `HasIndex(...).IsUnique()` kullanıldı.
Laboratuvardaki UQ constraint yerine uygulamada UX unique index vardır: aynı
benzersizlik korunur, SQL hata numarası 2627 yerine 2601 olur. Bu alanlar başka
bir ilişkinin hedef anahtarı olmadığı için alternate key semantiği eklenmedi.
EF'nin AcademicTermId FK index'i de üretildi; CourseId ayrıca indexlenmez,
birleşik index'in ilk sütunudur. Mevcut CI_AS collation ve Türkçe metinler korunur.
[EF index ve CHECK API'leri](https://learn.microsoft.com/en-us/ef/core/modeling/indexes),
[alternate key ayrımı](https://learn.microsoft.com/en-us/ef/core/modeling/keys).

Up, Down, designer/snapshot ve üretilen ileri SQL incelendi. İleri SQL yalnızca
üç model tablosunu, kısıt/indexleri ve migration geçmişini oluşturur. Down bu üç
tabloyu kaldırır; veri yedeği değildir ve bu çalışmada uygulanmadı. CLI ayrıca
yeni uygulama DB'sini oluşturup SQL Server için READ_COMMITTED_SNAPSHOT açtı.

### Doğrulama

```powershell
dotnet ef database update
dotnet ef migrations has-pending-model-changes
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -d university_registration_app -b -f 65001 -i sql/app/verify.sql
pwsh -File scripts/verify-http.ps1
```

Uygulama SQL doğrulaması gerçek sütun/tür/uzunluk/NULL/IDENTITY/default, PK,
unique index, CHECK ve migration geçmişini inceler. 3. günün deneyleri
`sql/shared/verify-constraints.sql` içinde tek kaynaktır; lab/app sarmalayıcıları
yalnızca hedef korumasını ve unique hata kodu/adlarını seçer. App doğrulaması
laboratuvara yazmaz. Deney değişiklikleri rollback edilir.

Gerçek sonuçlar: migration uygulandı; ikinci update bekleyen migration bulmadı;
model/snapshot farkı yok. Seed iki kez çalıştı, sonuç 2/2/3 kayıt. Şema ve ortak
23 numaralı kontrol geçti. Restore/build 0 hata, 0 uyarı. HTTP betiği üç endpoint'in
sözleşmesini, sıralamayı ve Credits'in görünmediğini doğruladı. İsteğe bağlı
4. gündeki `-VerifyDatabaseSource` modu ders 1 adını önce beklenen değere karşı kontrol edip
geçici adla **commit eder**; HTTP'de görür, finally içinde güvenle geri yükler ve
HTTP'den yeniden doğrular. Beklenmeyen değerleri ezmez. Ardından uygulamayı yeniden
başlatır ve 8 eşzamanlı GET isteğini kontrol eder. Bunların hepsi geçti; tüm geçici
API süreçleri kapandı. Bu, kontenjan/concurrency garantisi değildir.
Boş bağlantıyla başlatma kontrolü de açık hata ile reddedildi; fallback yapılmadı.

[4. gün öğrenme notu](docs/day4-ef-core-notes.md) · [ADR 0004](docs/adr/0004-ef-core-course-read.md)

## 5. gün: Doğrulanan ders oluşturma ve düzenleme

Bu bölüm 5. günün durumunu kaydeder. 6. günde diğer listeler de DB'ye taşındı ve
kimlikleri sequence oldu; güncel test DB öneki day6'dır.

Güncel ders endpoint'lerinin tamamı uygulama DB'sini kullanır. Dönem ve açılış
listeleri hâlâ bellekten çalışır. DELETE yoktur. Paket/araç sürümleri değiştirilmedi.
4. günün örnek ders adını değiştiren test modu kaldırıldı; DB kaynağı kanıtı artık
yalnızca yeni test kayıtlarıyla ve izole DB'de yapılır.

| İstek | Başarı yanıtı | Hata durumları |
| --- | --- | --- |
| GET /courses | 200; Id sıralı id/code/name dizisi | Beklenmeyen hata: 500 |
| GET /courses/{id:int} | 200; id/code/name/credits | Yoksa 404 Problem Details |
| POST /courses | 201; id/code/name/credits ve çalışan detay URL'sine Location | 400 alan/binding hatası, 409 kod çakışması |
| PUT /courses/{id:int} | 200; aynı id, güncel id/code/name/credits | Önce bulunamazsa 404, sonra 400/409 |

CreateCourseRequest ve UpdateCourseRequest yalnızca nullable Code/Name ve int
Credits alır. Entity veya Id alınmaz; PUT kimliği URL'dendir. JSON'daki bilinmeyen
Id alanı kullanılıp mevcut kaydın üstüne yazılmaz. PUT üç düzenlenebilir alanın
tamamını gerektirir; eksik alan null/0 olarak doğrulamadan geçmez.

PowerShell örneği (Id sunucu tarafından üretilir):

```powershell
$body = @{ code = ' cse205 '; name = ' Algoritmalar '; credits = 4 } | ConvertTo-Json
$course = Invoke-RestMethod http://127.0.0.1:5080/courses -Method Post -ContentType 'application/json' -Body $body
Invoke-RestMethod "http://127.0.0.1:5080/courses/$($course.id)"
$update = @{ code = 'CSE205'; name = 'Algoritma Analizi'; credits = 5 } | ConvertTo-Json
Invoke-RestMethod "http://127.0.0.1:5080/courses/$($course.id)" -Method Put -ContentType 'application/json' -Body $update
```

Örnek oluşturma yanıtı (3 yalnızca örnek kimliktir):

```json
{ "id": 3, "code": "CSE205", "name": "Algoritmalar", "credits": 4 }
```

Code Trim + ToUpperInvariant, Name yalnızca Trim uygulanarak saklanır. Boş/null/
boşluk kod ve ad, temizlenmiş 20/200 sınırını aşan alanlar ve Credits <= 0
reddedilir. Ek kredi üst sınırı veya regex yoktur. Birbirinden bağımsız hatalar
`ValidationProblem` içinde `errors.code/name/credits` anahtarlarıyla birlikte döner.
JSON sayı alanı strict'tir: `"credits":"3"` dahi sayı türü olmadığı için 400'dür.
Geçersiz POST alanları DB sorgulanmadan reddedilir; PUT önce varlığı kontrol eder.

Kod ön kontrolü DB'nin mevcut `SQL_Latin1_General_CP1_CI_AS` karşılaştırmasına
uyar: büyük/küçük harfe duyarsız, aksana duyarlıdır. Collation değiştirilmedi.
PUT kendi Id'sini çakışma sorgusundan dışlar. Ön kontrol rezervasyon değildir;
araya başka istek girebilir. Son koruma `UX_Courses_Code` unique index'idir.

Kayıt anındaki 409 eşlemesi yalnızca SqlException.Errors içindeki **2601** ve
ilk iki tırnaklı tanımlayıcının **dbo.Courses / UX_Courses_Code** olması halinde
yapılır. 2627 PK, diğer unique index ve CHECK/FK hataları kod çakışması sayılmaz.
SqlError yapılandırılmış index adı sunmadığından isim kontrolü mesajın tırnaklı
tanımlayıcı sırasına bağımlıdır. Yerel sunucunun gerçek hata biçimiyle doğrulandı;
bilinmeyen dil/biçim 409'a zorlanmaz, 500 olarak merkezi mekanizmaya bırakılır.
Bu sınırlama provider hata numarasının güvenilirliğiyle karıştırılmamalıdır.

Beklenmeyen hatalar merkezi UseExceptionHandler/Problem Details ile genel 500
üretir; ham SQL, exception mesajı, stack veya bağlantı bilgisi dönmez. Sunucu logu
yalnızca exception türü ve trace kimliğini kaydeder; değer içerebilen EF command/
update hata logları bastırılmıştır. İptaller iş kuralı hatasına çevrilmez.

### Sequence migration ve seed

**Migration ve geliştirme seed'i sırasında API dahil tüm yazıcıları durdurun.**
`20261027113628_GenerateCourseIds`, mevcut Courses tablosunu silmez/yeniden kurmaz.
`dbo.CourseIds` int sequence ve `DF_Courses_Id` default'u ekler; EF ValueGeneratedOnAdd
ile kaydedilen Id'yi alır. Migration gerçek MAX(Id)'yi transaction/table lock içinde
okuyup başlangıcı üstüne koyar, int taşmasını reddeder. Bu yalnızca bakım sırasında
başlangıç seçimi; isteklerde MAX+1 veya süreç içi sayaç kullanılmaz. Dinamik SQL'e
yalnızca doğrulanmış bigint'ten dönüştürülen sayı girer, kullanıcı girdisi girmez.
Sequence boşlukları normaldir; başarısız işlemler/rollback/cache boşluk bırakabilir.

```powershell
dotnet tool restore
dotnet restore
dotnet build --no-restore
dotnet ef migrations script InitialUniversitySchema GenerateCourseIds --output sql/app/day5-migration.tmp
# SQL'i inceleyip yazıcıların kapalı olduğundan emin olduktan sonra:
dotnet ef database update
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -d university_registration_app -b -f 65001 -i sql/app/seed.sql
dotnet ef migrations has-pending-model-changes
dotnet run --project .\UniversityRegistration.csproj --no-build --urls http://127.0.0.1:5080
```

Seed aynı değerleri atlar, farklı mevcut değeri ezmez. Açık seed kimlikleri sequence
tüketmediğinden seed gerektiğinde sequence'i maksimum kimliğin üstüne **yalnızca
ilerletir**; geri sarmaz veya boşluk doldurmaz. İkinci çalıştırma kayıt çoğaltmaz.
AcademicTerm/CourseOffering kimlikleri açık kalır; kapasitede DEFAULT yoktur.
Down önce default'u, sonra sequence'i kaldırır; kayıtları silmez. Down bu çalışmada
uygulanmadı. İlk migration değiştirilmedi; startup migration eklenmedi.

### Gerçek doğrulama

```powershell
pwsh -File scripts/verify-http.ps1
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -d university_registration_app -b -f 65001 -i sql/app/verify.sql
pwsh -File scripts/verify-course-writes.ps1
```

GET betiği salt okunurdur: ilk iki örneği doğrular, ek dersleri hata saymaz;
tüm liste öğelerinde yalnızca id/code/name ve sıralama kontrol edilir.
Yazma betiği her çalıştırmada `university_registration_tests_day5_<guid>` adında
yeni LocalDB oluşturur; mevcut DB'yi kullanmaz/silmez. Kaynak kontrolünde test
projesi veya test framework yoktur; küçük C# integration kontrolü `obj/day5-checks`
altında geçici konsol projesiyle derlenir. Gerçek API süreçleri başlatılır.

Test, önce eski migration'a Id=500 ekleyip yeni migration'ın bu kaydı koruduğunu
ve sonraki oluşturmanın daha büyük Id aldığını doğrular. Seed iki kez, şema ve
ortak kısıt deneyleri izole DB'de çalışır. `sql/app/verify.sql` artık salt okunur
metadata kontrolüdür; eski 23 deney `sql/app/verify-constraints.sql` üzerinden
yalnızca işaretlenmiş taze test DB'sinde çalışır.

POST/Location/detay/listede görünme, restart sonrası kalıcılık, PUT aynı Id ve
kendi kodu, çakışmada korunma, toplu alan hataları, binding 400 ve bulunamayan 404
geçti. Sekiz farklı eşzamanlı POST benzersiz Id aldı; aynı kod yarışında bir 201
ve yedi 409 görüldü. Bu yarış tek başına catch dalı kanıtı sayılmadı: test context'ine
özel SaveChangesInterceptor, ön kontrolden sonra ayrı context ile rakip kaydı
commit ederek **gerçek SaveChanges hatası → 409 dalını** ayrıca doğruladı.
Üretim endpoint'inde test gecikmesi/anahtarı yoktur. Gerçek PK ve başka unique index
hataları yanlış sınıflandırılmadı; yalnızca test DB'sindeki geçici CHECK hatası,
Development ortamında dahi hassas ayrıntısız 500 verdi.

Finally, yalnızca testin D5_ kodlu kayıtlarını temizler ve API süreçlerini kapatır.
Test DB seed ile tutulur; sequence boşlukları kapatılmaz. App/lab örnekleri
değiştirilmez. Yeni kurulumlar için önce migration/seed gerekir. İleri eşzamanlı
düzenleme/lost update çözümü, silme ve kayıt/kontenjan işlemleri kapsam dışındadır.

[5. gün öğrenme notu](docs/day5-validation-and-errors.md) · [ADR 0005](docs/adr/0005-course-writes.md)

## 6. gün: İlişkili yazma ve change tracking

Bütün liste ve detay endpoint'leri artık `university_registration_app` DB'sinden
okur. `/course-offerings`, üç tabloyu SQL'e çevrilen iki JOIN ile birleştirir;
ders/dönem adı değişikliği sonraki okumada görünür. Bellek listeleri kaldırıldı.
Listeler Id sıralıdır; mevcut JSON alanları korunur. DbContext request kapsamında
scoped kalır, aynı context'te DB işlemleri sırayla await edilir.

| Endpoint | Sonuç |
|---|---|
| POST /academic-terms | 201, dönem yanıtı ve çalışan detay Location |
| GET /academic-terms/{id:int} | 200 veya 404 Problem Details |
| POST /course-offerings | 201, JOIN alanları ve çalışan detay Location |
| GET /course-offerings/{id:int} | 200 veya 404 Problem Details |

Örnek istekler (kimlik istemciden alınmaz):

```http
POST /academic-terms
Content-Type: application/json

{"name":"  2027 Yaz  ","startsOn":"2027-07-01","endsOn":"2027-08-31"}
```

Örnek yanıt (üretilen Id değişebilir):

```json
{"id":3,"name":"2027 Yaz","startsOn":"2027-07-01","endsOn":"2027-08-31"}
```

```http
POST /course-offerings
Content-Type: application/json

{"courseId":1,"academicTermId":3,"sectionCode":" a ","capacity":40}
```

```json
{"id":4,"courseId":1,"courseCode":"CSE201","courseName":"Veri Yapıları","academicTermId":3,"termName":"2027 Yaz","sectionCode":"A","capacity":40}
```

İkinci istekte ilk yanıtın gerçek Id'sini kullanın. Dönem ve açılış oluşturmak
**ayrı işlemlerdir**: açılış başarısız olursa önceden oluşturulan dönem kalır.
İlişkili ders/dönem yeniden Add edilmez; yalnızca açılışın FK değerleri atanır.

Dönem adı Trim, zorunlu ve en fazla 100 karakterdir. İki tarih zorunludur;
EndsOn >= StartsOn (aynı gün kabul). Dönem adı benzersizliği/tarih sınırı eklenmedi.
Şube Trim + ToUpperInvariant, zorunlu ve en fazla 10 karakter; iki FK kimliği ve
kapasite pozitif olmalıdır. Ders/dönem bulunmazsa ilgili alan hatası döner.
Alan hataları birlikte 400 ValidationProblem; bozuk tarih JSON binding aşamasında
400 Problem Details; aynı ders/dönem/şube 409; bilinmeyen sistem hatası güvenli 500.

Ön kontrol kayıt ayırmaz. Son koruma DB FK/CHECK/unique index'leridir. Kayıt anında
yalnızca 2601 + `dbo.CourseOfferings`/`UX_CourseOfferings_Course_Term_Section`
409'a; 547 + `FK_CourseOfferings_Courses` veya `FK_CourseOfferings_AcademicTerms`
ilgili alan 400'üne eşlenir. Diğer CHECK/FK hataları bu eşlemeye girmez. SQL Server
yapılandırılmış kısıt adı sunmadığı için tırnaklı isimlerin mesajdaki sırası hâlâ
bağımlılıktır. Gerçek yerel sunucuda doğrulandı; bilinmeyen biçim 500'e bırakılır.
Mevcut CI_AS collation ve ders kodu hata eşlemesi değişmedi.

### Migration, seed ve çalıştırma

SDK 10.0.401, EF SQL Server/Design/dotnet-ef 10.0.14, LocalDB 17.0.4025.3 korundu.
Yeni migration: `20261027120003_GenerateTermAndOfferingIds`. Ayrı
`AcademicTermIds` ve `CourseOfferingIds` sequence/default'ları mevcut gerçek
MAX(Id)'nin üstünden başlar; mevcut satırlar ve FK'ler korunur. Bu bakım adımındaki
başlangıç hesabıdır; isteklerde MAX+1 kullanılmaz. Course stratejisi değişmedi.
Sequence boşlukları normaldir; rollback kimlik tahsisini geri almış sayılmaz.

**Migration ve seed öncesinde bütün yazıcıları durdurun.** Proje kökünde:

```powershell
dotnet tool restore
dotnet restore
dotnet build --no-restore
dotnet ef migrations script GenerateCourseIds GenerateTermAndOfferingIds --output sql/app/day6-migration.tmp
# Üretilen SQL'i inceleyin; migration oluşturmak/script üretmek DB'yi değiştirmez.
dotnet ef database update
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -d university_registration_app -b -f 65001 -i sql/app/seed.sql
dotnet ef migrations has-pending-model-changes
dotnet run --project .\UniversityRegistration.csproj --no-build --urls http://127.0.0.1:5080
```

Bağlantı mevcut `ConnectionStrings:UniversityDatabase` ayarı/ortam değişkeninden
alınır; Windows kimlik doğrulaması kullanılır. Parola gerekmez; secret commit etmeyin.
Laboratuvar DB'si migration hedefi değildir. Startup migration yoktur. Seed aynı
örnekleri çoğaltmaz, farklı mevcut değeri ezmek yerine hata verir; üç sequence'i
yalnızca gerektiğinde ileri hizalar. Down varsayılanları önce, sequence'leri sonra
kaldırır; incelendi fakat uygulanmadı, geri alma testi yapıldığı iddia edilmez.

### Tekrar çalıştırılabilir kontroller

```powershell
pwsh -File scripts/verify-http.ps1
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -d university_registration_app -b -f 65001 -i sql/app/verify.sql
pwsh -File scripts/verify-course-writes.ps1
```

Son komut mevcut ders yazma regresyonunu ve `RelatedWriteChecks.cs` içindeki
6. gün/Tracking deneylerini birlikte çalıştırır. Her seferinde benzersiz
`university_registration_tests_day6_<guid>` DB'si oluşturur; mevcut DB'yi yeniden
kullanmaz. Yeni test framework'ü yoktur; geçici konsol projesi obj altında kalır.
İlk şemaya 500/600/700 kimlikli ilişkili kayıtlar koyarak migration'ın yalnızca
seed varsaymadığını da kontrol eder. Ortak SQL kısıt deneyleri kopyalanmaz.

HTTP alanları, tarihler, 201/Location, normalizasyon, 400/404/409, paralel kimlikler,
aynı birleşimde tek kayıt, restart, JOIN'de DB adları ve seed tekrarı doğrulanır.
Test interceptor'ları yalnızca test context'inde ön kontrolden sonra gerçek rakip
kayıt veya üst kayıt silinmesi oluşturarak save hata dallarını çalıştırır; üretim
API'sine debug endpoint/gecikme anahtarı eklenmez. Tracking deneyinin gerçek DB
okumaları ayrı context'lerden yapılır; başarısız context yeniden kullanılmaz.

Finally önce test açılışlarını, sonra test dönem ve derslerini temizler; süreçleri
kapatır. Her DB yeni ve teste özel olduğundan 700 üzerindeki açılışlar testin
ürettiği kayıtlardır; 700 migration sentinel'idir. D6_ dönem/dersleri ve eski
regresyonun D5_ dersleri kaldırılır. Kullanıcı/app/lab kayıtlarına dokunulmaz.
Test DB seed ile tutulur; otomatik DROP yapılmaz. Lost update, kontenjan tüketimi
ve ileri transaction/isolation konusu bu kapsamda çözülmez.

[6. gün öğrenme notu](docs/day6-related-writes.md) · [ADR 0006](docs/adr/0006-related-writes.md)

## 7. gün: Filtreli ve sayfalı ders kataloğu

`GET /courses/catalog` ayrı bir endpoint'tir. Eski `GET /courses` bütün dersleri
Id sıralı JSON dizisi ve yalnızca id/code/name ile döndürmeye devam eder.
Detay, POST, PUT, dönem ve açılış sözleşmeleri korunur. Şema/model/paket değişmedi;
bu gün için yeni migration yoktur.

| Parametre | Varsayılan ve kural |
|---|---|
| search | İsteğe bağlı; Trim; boşsa filtre yok. Code **veya** Name içinde literal metin arar. |
| credits | İsteğe bağlı pozitif int; tam eşitlik. Aramayla birlikte AND uygulanır. |
| page | 1; en az 1. |
| pageSize | 20; 1–100. |

```powershell
curl.exe -i "http://127.0.0.1:5080/courses/catalog?search=CSE201&credits=4&page=1&pageSize=20"
```

Başlangıç verileriyle örnek yanıt:

```json
{"items":[{"id":1,"code":"CSE201","name":"Veri Yapıları"}],"page":1,"pageSize":20,"totalCount":1}
```

`totalCount`, sayfanın uzunluğu değil bütün filtrelenmiş sonucun sayısıdır.
Eşleşme yoksa 200/boş items/0; geçerli ama son sayfayı aşan istekte 200/boş items
ve gerçek totalCount döner. Liste öğesine Credits eklenmez.

Geçersiz aralıklar birlikte alan bazlı 400 ValidationProblem döner. Offset hesabı
long ile yapılır; int.MaxValue üstündeki Skip 400/page hatasıdır. Sayısal biçim
ve Int32 taşması binding aşamasında 400 Problem Details verir. Sıfır/negatif
değerler varsayılana çevrilmez. Search için ilave uzunluk/karakter kuralı yoktur.

Mevcut `SQL_Latin1_General_CP1_CI_AS` collation korunur: büyük/küçük harfe duyarsız,
aksana duyarlıdır. `%`, `_` ve apostrof Contains içinde literal olarak aranır;
LINQ parametreleri SQL metnine birleştirilmez. Bu `string.Contains` aramasıdır,
SQL Server full-text CONTAINS altyapısı değildir. Her ölçekte hızlı veya index
dostu olduğuna dair iddia/benchmark yoktur.

Filtreler IQueryable üzerinde kurulur; aynı filtreli sorgudan önce CountAsync,
ardından OrderBy(Id)/Skip/Take/DTO projection/ToListAsync çalışır. Aynı scoped
context'te iki sorgu sırayla await edilir. Bunlar ayrı SQL sorgularıdır; eşzamanlı
yazmalarda count ve items aynı anın garantili görüntüsü değildir. Transaction
eklenmedi. Offset büyüdükçe maliyet artabilir; sayfalar arasında veri değişirse
kayma olabilir. Id sırası sabit veri üzerinde deterministiktir.

`CourseListItemDto` yalnızca üç alan taşır; entity içermez. Saf projection zaten
Course entity'si izlemez; kataloğa AsNoTracking eklenmedi. AsNoTracking karşılaştırması
yalnızca testte entity sorgusuyla yapılır; genel tracking ayarı ve PUT değişmedi.

### Doğrulama

```powershell
dotnet restore
dotnet build --no-restore
dotnet ef migrations has-pending-model-changes
pwsh -File scripts/verify-http.ps1
pwsh -File scripts/verify-course-writes.ps1
```

Mevcut runner `CatalogChecks.cs` kontrolünü de çalıştırır; yeni test framework'ü
yoktur. Mevcut `university_registration_tests_day6_<guid>` izole DB adlandırması
ve sahiplik işareti korunur; adın day6 olması 7. gün kontrollerini dışlamaz.
Katalog için 53 sayfalanan kayıt ve 5 literal/aksan örneği eklenir. Katalog finally
bloğu yalnızca bu kayıtlara ait Id'leri temizler; dış runner ilişkili test verilerini
ve süreçleri temizler. Uygulama/laboratuvar DB'sine test yazması yapılmaz.

SQL kanıtı production handler'ını test context'iyle gerçekten çalıştıran
`ReaderExecutedAsync` interceptor'ıdır. Tamamlanan COUNT ve sayfa komutlarında
WHERE, ORDER BY, OFFSET/FETCH, üç sütun ve parametreler kontrol edilir; iki komut
olması satır başına sorgu olmadığını gösterir. HTTP testleri ayrıca gerçek API
sürecinde yapılır. SQL/parametreler yalnızca bellekte incelenir, çıktı dosyası veya
snapshot commit edilmez; alias/parametre adlarının birebir eşleşmesi aranmaz.

[7. gün öğrenme notu](docs/day7-querying.md) · [ADR 0007](docs/adr/0007-course-catalog.md)

## 8. gün: Öğrenci ve temel ders kaydı

Student bir **iş profilidir**, parola/token/Identity hesabı değildir. Bu API'de
kimlik doğrulama ve yetkilendirme yoktur. İstemciden StudentId kabul eden bu
geliştirme akışı güvenli öğrenci self-servisi olarak kullanılmamalıdır.

Enrollment, öğrenci ile **ders açılışı** arasındaki açık ilişki entity'sidir.
Bir öğrenci birçok açılışa, bir açılış birçok öğrenciye bağlanır. Kayıt zamanı
ilişkiye aittir. İki zorunlu FK, ayrı Id ve UNIQUE(StudentId, CourseOfferingId)
vardır. Aynı dersin farklı şubeleri farklı açılışlardır ve bugün kabul edilir.

| Endpoint | Başarı / hata |
|---|---|
| POST /students | 201 + Location; alan 400, kullanılan numara 409 |
| GET /students/{id:int} | 200; yoksa 404 |
| POST /enrollments | 201 + Location; alan/olmayan FK 400, tekrar çift 409 |
| GET /enrollments/{id:int} | 200; yoksa 404 |
| GET /students/{id:int}/enrollments | 200 dizi; öğrencinin kaydı yoksa boş; öğrenci yoksa 404 |

```http
POST /students
Content-Type: application/json

{"studentNumber":"001234","fullName":"Rabia Nur Akdaş"}
```

```json
{"id":1,"studentNumber":"001234","fullName":"Rabia Nur Akdaş"}
```

İki metin Trim edilir; boş/null/boşluk kabul edilmez. Numara en fazla 30, ad soyad
en fazla 200 karakterdir. Numara metindir; sıfırlar ve harf biçimi korunur, rakam
regex'i yoktur. FullName benzersiz değildir. Numara benzersizliği mevcut
SQL_Latin1_General_CP1_CI_AS collation'a uyar: harfe duyarsız, aksana duyarlı.
Örneğin `0008Ab` saklanır, `0008ab` ikinci öğrenci olarak kabul edilmez.

```http
POST /enrollments
Content-Type: application/json

{"studentId":1,"courseOfferingId":1}
```

```json
{"id":1,"studentId":1,"courseOfferingId":1,"createdAtUtc":"2026-10-27T12:00:00+00:00"}
```

Id ve zaman örnektir; gerçek öğrenci/açılış kimliklerini kullanın. CreatedAtUtc
sunucuda DateTimeOffset.UtcNow ile üretilir; istemcinin Id/zamanı istek sözleşmesinde
yoktur. Pozitif kimlikler, iki üst kaydın varlığı ve çift benzersizliği kontrol
edilir. Sadece yeni Enrollment Add edilir; mevcut üst kayıtlar yeniden eklenmez.
SaveChangesAsync tamamlanınca 201 ve çalışan detay Location döner.

Öğrencinin kayıt listesi örneği:

```json
[{"id":1,"courseOfferingId":1,"courseCode":"CSE201","courseName":"Veri Yapıları","termName":"2026-2027 Güz","sectionCode":"A","createdAtUtc":"2026-10-27T12:00:00+00:00"}]
```

Liste Enrollment.Id artan sırasındadır; öğrenci varlık sorgusundan sonra tek
SQL JOIN/projection çalışır. Sabit iki sorgu N+1 değildir; her satır için sorgu
yoktur. DTO'lar entity içermez. Genel öğrenci listesi ve yeni PUT/DELETE yoktur.

**Bugünkü sınır:** Kontenjan kontrolü/tüketimi, ön koşul, kredi sınırı, saat
çakışması, farklı şubeye kayıt engeli, danışman onayı, durum geçişi ve bırakma yoktur.
Aynı çifte eşzamanlı isteklerde tek kayıt koruması, kontenjan veya genel idempotency
garantisi değildir. Ön kontrol kayıt anını garanti etmez; son koruma DB kısıtıdır.

### Migration ve hata eşlemesi

`20261027123407_AddStudentsAndEnrollments` iki yeni tabloyu ve ayrı StudentIds/
EnrollmentIds sequence/default'larını oluşturur. Yeni boş tablolar için başlangıç
1'dir; istek başına MAX+1 yoktur. Mevcut tablolar/kimlikler korunur. CreatedAtUtc
zorunlu datetimeoffset, metinler nvarchar'dır. İki FK NO ACTION; kayıt varken
öğrenci/açılış silinemez. Birleşik unique index StudentId ile başladığından EF'nin
ayrıca oluşturduğu ilişki index'i CourseOfferingId üzerindedir.

Dar hata eşlemesi: 2601 + Students/UX_Students_StudentNumber → öğrenci 409;
2601 + Enrollments/UX_Enrollments_Student_Offering → kayıt 409; 547 + ilgili
FK_Enrollments_Students veya FK_Enrollments_CourseOfferings → doğru alan 400.
Diğer unique/PK/CHECK/altyapı hataları bu sonuçlara zorlanmaz. Sağlayıcı ayrı kısıt
adı alanı sunmadığı için mesajın tırnaklı nesne adlarına/sırasına bağımlılık sürer;
bilinmeyen biçim mevcut güvenli merkezi 500'e gider. Ham hata istemciye verilmez.

Proje kökünde, API yazıcılarını durdurarak:

```powershell
dotnet tool restore
dotnet restore
dotnet build --no-restore
dotnet ef migrations script GenerateTermAndOfferingIds AddStudentsAndEnrollments --output sql/app/day8-migration.tmp
# Up/Down ve SQL'i inceledikten sonra, yalnızca uygulama DB'sine:
dotnet ef database update
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -d university_registration_app -b -f 65001 -i sql/app/seed.sql
dotnet ef migrations has-pending-model-changes
dotnet run --project .\UniversityRegistration.csproj --no-build --urls http://127.0.0.1:5080
```

SDK 10.0.401 ve EF paket/araç 10.0.14 değişmedi. Mevcut güvenli Windows bağlantı
ayarı kullanılır; secret eklenmedi. Laboratuvar hedef değildir. Startup migration
yoktur. Down yeni kayıt tablolarını kaldırır ve içlerindeki veriyi kaybettirir;
incelendi, uygulanmadı. İlk migration'lar değiştirilmedi.

### Gerçek doğrulama ve temizlik

```powershell
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -d university_registration_app -b -f 65001 -i sql/app/verify.sql
pwsh -File scripts/verify-http.ps1
pwsh -File scripts/verify-course-writes.ps1
```

Mevcut runner yeni `EnrollmentChecks.cs` kontrollerini de çalıştırır; izole
`university_registration_tests_day6_<guid>` yaklaşımı korunur. Öğrenci/kayıt HTTP,
UTC zaman aralığı, restart, gerçek save yarışı, NO ACTION, SQL sorgu sayısı ve
mevcut bütün regresyonlar test edilir. Üretim endpoint'lerine test kancası eklenmez.
Yeni öğrenciler SQL seed'e eklenmedi; seed tekrarları öğrenci/kayıt verilerini
değiştirmez. Şema doğrulaması yeni metadata betiğini içerir.

Finally önce test Enrollment'larını, sonra yalnızca test öğrencilerini ve testin
oluşturduğu açılışı temizler; dış runner diğer testleri/süreçleri temizler. Yeni
DB'de test öğrencileri D8_ numaraları ve bilinen `0008Ab` ile ayırt edilir. Uygulama
ve kullanıcı verilerine test yazması yapılmaz. Test DB seed ile tutulur, otomatik
DROP yoktur; sequence boşlukları doldurulmaz.

[8. gün öğrenme notu](docs/day8-enrollment.md) · [ADR 0008](docs/adr/0008-explicit-enrollment.md)

## 9. gün: Atomik toplu kayıt

`POST /students/{id:int}/enrollments/batch` öğrencinin seçimlerini **ya tümüyle
oluşturur ya hiçbirini oluşturmaz**. Öğrenci yalnızca URL'den alınır:

```http
POST /students/1/enrollments/batch
Content-Type: application/json

{"courseOfferingIds":[3,1]}
```

Örnek 201 yanıtı (üretilen Id ve zamanlar değişir):

```json
{"items":[
  {"id":501,"studentId":1,"courseOfferingId":3,"createdAtUtc":"2026-10-27T13:00:00+00:00"},
  {"id":502,"studentId":1,"courseOfferingId":1,"createdAtUtc":"2026-10-27T13:00:00+00:00"}
]}
```

Öğeler istek sırasındadır; her biri mevcut `/enrollments/{id:int}` adresinden
okunur. Batch kaynağı oluşturulmaz ve hayalî batch Location verilmez. Öğrenci
kayıt listesinde tamamı görünür. Id ve CreatedAtUtc istemciden alınmaz.

- Liste zorunlu, 1–50 pozitif ve birbirinden farklı kimlik. **50 teknik istek
  sınırıdır**, akademik ders/kredi sınırı değildir. Tekrarlar ayıklanmaz, 400 döner.
- Yapı/alan hataları önce 400 ValidationProblem; yanlış JSON türü binding 400.
- Yapı geçerli, öğrenci yoksa 404. Herhangi bir açılış yoksa courseOfferingIds
  alanında 400; hiçbir ekleme yok.
- Seçimlerden birine mevcut kayıt varsa bütün istek 409 ile reddedilir. Mevcut
  kayıt korunur; diğer seçimler için yeni kayıt oluşmaz.
- Tam başarı, SaveChanges tamamlandıktan sonra 201. Beklenen save unique hatası
  409; ilgili FK hatası tekli akıştaki studentId/courseOfferingId alanıyla 400.
  Diğer hatalar mevcut güvenli merkezi 500 mekanizmasına gider.

Ön kontroller üç toplu sorgudur: öğrenci, seçilen açılış kimlikleri, seçilen mevcut
kayıtlar. Her açılış için ayrı sorgu yoktur. Ardından ortak UTC zamanıyla yalnızca
yeni Enrollment'lar AddRange edilir ve **tek SaveChangesAsync** çağrılır.
Üretim endpoint'inde BeginTransaction gerekmedi. Ön kontroller yarış garantisi
değildir; kayıt anındaki DB kısıtları ve EF'nin atomik yazma sınırı esas korumadır.
Başarısız context yeniden kaydedilmez, rakip transaction'ın kaydı silinmez.

Kontenjan tüketimi, ön koşul, kredi sınırı, saat çakışması, danışman onayı, bırakma
ve yetkilendirme hâlâ yoktur. Genel retry/idempotency garantisi eklenmedi.
Başarılı commit sonrasında HTTP yanıtı kaybolursa kayıtlar yine oluşmuş olabilir.

### Çalıştırma ve gerçek doğrulama

```powershell
dotnet restore
dotnet build --no-restore
dotnet ef migrations has-pending-model-changes
pwsh -File scripts/verify-http.ps1
pwsh -File scripts/verify-course-writes.ps1
dotnet run --project .\UniversityRegistration.csproj --no-build --urls http://127.0.0.1:5080
```

Şema/model değişmedi; migration üretilmedi. Paket/araç sürümleri korundu.
Mevcut app bağlantısında MARS kapalı, otomatik retry yapılandırması yok; test
context'inde de MARS=false, RetriesOnFailure=false ve SupportsSavepoints=true
kontrol edildi. Ayarlar değiştirilmedi. Daha sonra MARS/retry açılması bu deneyin
koşullarını değiştirir; mevcut açık transaction örneklerini körlemesine taşımayın.

`RelatedWriteChecks.Tracking` içindeki mevcut A/B deneylerine C/D eklendi:

| Deney | Gerçek DB sonucu (yeni context) |
|---|---|
| A: Tek SaveChanges, ikinci kayıtta CHECK ihlali | İki yeni kayıt da yok |
| B: Bağımsız iki SaveChanges, ikinci hata | İlk kayıt var, ikinci yok |
| C: Açık transaction, iki SaveChanges, ikinci hata, tam rollback | İkisi de yok |
| D: Açık transaction, iki başarılı SaveChanges, commit | İkisi de var |

C'de ikinci save hatasından sonra aynı transaction içinde ilk kayıt hâlâ görüldü;
savepoint'ten dönmek dış transaction'ı geri almak değildir. Tam rollback sonrası
yeni context ilk kaydı bulamadı. Rollback tracker'ı temizlemedi. Temizlik token'ı
CancellationToken.None; rollback hataları ayrıca tür adıyla kaydedilir ve asıl
hata korunur. Transaction'lar await using ile dispose edilir. Rollback temizliğinin
kendisine hata enjekte edilmedi; başarılı rollback ve commit yolları çalıştırıldı.

`BatchEnrollmentChecks` gerçek HTTP'de başarı/ret, sıralama, detay/listeler,
restart ve DB durumunu kontrol eder. Ön kontrol retleri atomiklik kanıtı sayılmaz.
Ayrı test context'inde production batch handler'ı çalıştırılarak ön kontrolden
sonra rakip çift commit edildi. **Testte** MaxBatchSize(1) ile bir INSERT'in
başarıyla yürütülüp sonraki unique hatasında geri alındığı görüldü; rakip kayıt
kaldı. Üretim batching ayarı değişmedi. FK save hataları da kısmi kayıt bırakmadı.
Provider'ın toplam INSERT komut sayısına sabit beklenti konmadı; 50 öğede sadece
üç ön kontrol SELECT'i doğrulandı.

Mevcut GUID adlı izole DB ve tüm regresyonlar kullanılır. Finally yalnız testin
kayıtlarını Enrollment → öğrenci/açılış sırasıyla temizler; dış runner eski deney
verilerini ve API süreçlerini temizler. DB seed ile tutulur. Kullanıcı/app/lab
verilerine deney yazması yapılmaz; sequence boşlukları geri doldurulmaz.

[9. gün öğrenme notu](docs/day9-transactions.md) · [ADR 0009](docs/adr/0009-atomic-batch.md)

## 10. gün — Test sınırları ve seçilebilir çekirdek

Ürün endpoint'leri ve şema değişmedi. Mevcut konsol çalıştırıcısında `-Mode unit`
DB'siz doğrulama, `-Mode core` birim + üç bağımsız gerçek HTTP/LocalDB kayıt
senaryosu, `-Mode full` önceki geniş regresyonları seçer. Yeni framework/paket yok.

```powershell
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode core -Reverse
```

[Test haritası, ön koşullar, komutlar, izolasyon ve öğrenme notu](docs/testing.md).
Core benzersiz ve önceden bulunmadığı doğrulanan test DB'sini kullanır; test
kayıtlarını finally ile temizler, API sürecini kapatır, boş şemayı inceleme için
tutar. App/lab verilerine dokunmaz. Başarısız alt komut başarısız exit code üretir.
[ADR 0010](docs/adr/0010-selectable-behavior-checks.md).

## 11. gün — Kayıt sorumlulukları

Tekli/batch endpoint'leri isteğin biçimini doğrular ve HTTP yanıtını üretir.
Scoped `Application/EnrollmentOperations` varlık/tekrar kontrolleri, UTC kayıt
zamanı, Add/AddRange ve SaveChanges koordinasyonunu taşır. İşlem sonucu HTTP'den
bağımsızdır; mevcut DTO'lar yeniden kullanılır. SQL hata tanıma Data altında kalır.
Batch bütün seçimleri tek SaveChanges ile kaydeder; tekli metot döngüyle çağrılmaz.
Route, alanlar, durum kodları, Location, sorgu sayısı ve transaction sınırı korunur.
Şema/paket değişikliği, interface/repository veya yeni ürün özelliği yoktur.

Çalıştırma komutu hâlâ `dotnet run --project .\UniversityRegistration.csproj --urls http://127.0.0.1:5080`.
Testler repo kökünde:

```powershell
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
```

[Tasarım değerlendirmesi ve öğrenme notu](docs/day11-responsibilities.md) ·
[ADR 0011](docs/adr/0011-enrollment-responsibilities.md) · [Test kılavuzu](docs/testing.md).

## 12. gün — Kayıt önceliği önizlemesi

`POST /registration-priority/preview` DB sorgusu/yazması yapmadan örnek politika
hesaplar. İstek:

```json
{"policy":"graduation","completedCredits":225,"requiredCredits":240}
```

200 yanıtı:

```json
{"policy":"graduation","score":100}
```

`graduation`: kalan kredi `Max(0, required-completed)`; en fazla 20 ise 100,
aksi halde 0. `completed-credits`: tamamlanan kredi kadar puan (aynı örnekte 225).
Bunlar örnek formüllerdir, üniversite yönetmeliği değildir. Farklı politikaların
puanları doğrudan karşılaştırılmaz; eşit puan sıralaması tanımlanmadı.

Policy Trim ve invariant küçük harfe çevrilir; yanıtta normalize adı döner.
Üç alan zorunludur. Completed >= 0, required > 0; completed'ın required'ı aşması
geçerlidir. Eksik/null/boş veya bilinmeyen policy ve geçersiz krediler alan bazlı
400 ValidationProblem verir; bağımsız hatalar birlikte döner. Yanlış JSON türü,
bozuk JSON ve Int32 taşması 400 verir. Ek üst kredi sınırı yoktur.

Bu bir yerleştirme/bekleme listesi veya kontenjan sistemi değildir. Kredilerin
bir öğrenciye ait olduğu doğrulanmaz. Gerçek kullanım güvenilir veri ve yetkili
politika seçimi gerektirir. Mevcut tekli/batch kayıt kararı, sırası ve atomikliği
etkilenmez. Yeni DB alanı veya migration yoktur.

Çalıştırma önceki komutla aynıdır:

```powershell
dotnet run --project .\UniversityRegistration.csproj --urls http://127.0.0.1:5080
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode preview
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
```

Unit saf formülleri sınar. Preview gerçek HTTP'yi erişilemeyen SQL test hedefiyle
çalıştırır; LocalDB gerektirmez. Full preview kontrollerini de içerir ve sonra
izole LocalDB regresyonlarına devam eder. [Öğrenme notu](docs/day12-strategy.md) ·
[ADR 0012](docs/adr/0012-priority-preview-strategy.md).

## 13. gün — Factory alternatifleri

Üretim politika seçimi `PriorityPreviewEndpoint.Preview` içindeki küçük switch'te
kaldı: tek tüketici var, validation seçimin sonucunu kullanıyor. Simple Factory'ye
taşımak bugün ek tür maliyetini karşılamıyor. Önizleme route/JSON/400 davranışları,
normalizasyon ve puanlar değişmedi.

Simple Factory, Factory Method ve Abstract Factory **yalnız öğrenme deneyi**
olarak `scripts/FactoryExperiments.cs` içinde bulunur. Mevcut gerçek stratejileri
kullanır; web üretim derlemesine/DI'a dahil değildir. Açıklama ürünü API'ye eklenmedi.

```powershell
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode factories
```

Bu mod DB gerektirmez. Mevcut unit/core/full modları da deneyleri çalıştırır.
[Karşılaştırma ve öğrenme notu](docs/day13-factories.md) ·
[ADR 0013](docs/adr/0013-factory-alternatives.md).

## 14. gün — Ön koşul ve dönemlik kredi sınırı

Tekli ve batch kayıt önce mevcut varlık/mükerrer kontrollerini, sonra doğrudan
ön koşulu, ardından dönemlik krediyi değerlendirir. Enrollment başarı değildir;
StudentCompletedCourse ayrı başarı kaydıdır. Batch'te ön koşul dersini seçmek
onu tamamlamak sayılmaz. CoursePrerequisite doğrudan ilişkiyi tutar; self CHECK
bütün döngüleri engellemez. Geniş akademik CRUD endpoint'i eklenmedi.

Açık örnek sunucu politikası `appsettings.json` içindedir:

```json
{"EnrollmentPolicy":{"MaxCreditsPerTerm":30}}
```

Eksik/geçersiz/pozitif olmayan ayarda uygulama başlangıcı başarısızdır; sınırsız
fallback yoktur. Ortam değişkeni karşılığı `EnrollmentPolicy__MaxCreditsPerTerm`.
İstemci limit gönderemez. 30 bir örnek proje politikasıdır, üniversite yönetmeliği
iddiası değildir. Güncel Course.Credits kullanılır; değişmesi geçmiş kayıt yükünü
etkiler. Her mevcut Enrollment (aynı dersin iki şubesi dahil) kredi yüküdür.
Dönemler ayrı hesaplanır; yeni batch seçimleri kendi döneminde birlikte toplanır.

Yeni retler 400 Problem Details'tir:

```json
{"status":400,"title":"Ders ön koşulu sağlanmadı.","code":"prerequisite_not_satisfied","courseOfferingId":101}
```

```json
{"status":400,"title":"Dönemlik kredi sınırı aşılıyor.","code":"credit_limit_exceeded","academicTermId":1}
```

Örnekler `detail` gibi standart Problem Details alanlarını kısaltır. İlk retle
durulur; ön koşul açılışları ve dönemler istek sırasıyla değerlendirilir. Önceden
kayıt409 ve mevcut404/alan400/FK/unique sözleşmeleri korunur. Retlerde yeni kayıt
yoktur; başarılı batch tek SaveChanges kullanır. **İki eşzamanlı istek aynı yükü
okuyabilir:** kredi kontrolü eşzamanlı aşımı kesin engellemez. Kontenjan, saat
çakışması, danışman istisnası ve dolaylı ön koşul zinciri yoktur.

Repo kökünde, uygulama yazmalarını durdurup hedefin app DB'si olduğunu doğrulayın.
Mevcut güvenli bağlantı yapılandırması tasarım zamanı araçları tarafından da okunur;
parola komutlara/dosyalara gömülmez. Lab migration hedefi değildir.

```powershell
dotnet tool restore
dotnet ef migrations script AddStudentsAndEnrollments AddPrerequisitesAndCompletedCourses --output obj/day14-migration.sql
# SQL'i inceleyin; iki yeni tablo/PK/FK/CHECK/index dışında değişiklik yoktur.
dotnet ef database update
dotnet ef database update
dotnet ef migrations has-pending-model-changes
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -d university_registration_app -b -i sql/app/verify.sql
dotnet run --project .\UniversityRegistration.csproj --urls http://127.0.0.1:5080
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode specifications
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
```

Varsayılan `sql/app/seed.sql` akademik ilişki eklemez. Opsiyonel
`sql/app/academic-example.sql`, **yalnız test runner'ın sahiplik işaretli day6
DB'sinde** çalışır (app/lab'de hata verir): `sqlcmd -S '(localdb)\MSSQLLocalDB' -E -d <runner-test-db> -b -f 65001 -i sql/app/academic-example.sql`.
Sentetik kayıtları kendi üretir, kimlikleri OUTPUT ile kontrol eder ve rollback
ile kaldırır. Tekrar koşmak kullanıcı verisini ezmez; sequence boşlukları normaldir.
Full modu bu örneği de doğrular. Gerçek öğrenciye keyfi başarı kaydı verilmez.

[Öğrenme notu ve kod bağlantıları](docs/day14-specifications.md) ·
[ADR 0014](docs/adr/0014-academic-specifications.md).


## 15. gün — Saatler ve açıklanabilir uygunluk

Kayıt artık doğrudan ön koşul, dönemlik kredi ve **bilinen haftalık saat**
çakışmalarını birlikte değerlendirir. Önceki tek akademik `code` yanıtı bilinçli
olarak aşağıdaki `failures` listesine dönüştü; ilk rette durulmaz.

```http
POST /students/1/enrollments/eligibility
Content-Type: application/json

{"courseOfferingIds":[101,102]}
```

Geçerli değerlendirme her zaman 200; uygun örnek:

```json
{"isEligible":true,"failures":[]}
```

Uygun olmayan örnek (kimlikler örnektir):

```json
{
  "isEligible": false,
  "failures": [
    {"code":"prerequisite_not_satisfied","message":"Dersin doğrudan ön koşulları başarıyla tamamlanmalıdır.","courseOfferingId":101},
    {"code":"credit_limit_exceeded","message":"Mevcut kayıtlar ve seçimler dönemlik kredi sınırını aşıyor.","academicTermId":1},
    {"code":"schedule_conflict","message":"Seçilen açılışın haftalık saatleri başka bir açılışla çakışıyor.","courseOfferingId":101,"conflictingCourseOfferingId":102}
  ]
}
```

Tekli/batch **gerçek kayıt** akademik rette 400 `application/problem+json`,
`title: "Kayıt koşulları sağlanmadı."` ve aynı `failures` listesini döndürür.
Ret varsa hiçbir yeni Enrollment eklenmez. Başarı yanıtları ve tek SaveChanges
batch atomikliği korunur. Alan hataları 400 ValidationProblem, varlık/mükerrer
kontrolleri önceki 400/404/409 davranışındadır. Önizleme de batch'in zorunlu,
1–50, pozitif ve tekrarsız kimlik listesi doğrulamasını paylaşır; öğrenci yok404,
açılış yok400, mevcut kayıt409. 50 teknik istek sınırıdır.

Önizleme rezervasyon veya kayıt onayı değildir; gerçek kayıt aynı değerlendirmeyi
**yeniden** çalıştırır. İstemcinin önceden uygun bulduğu seçim kabul garantisi
vermez. Priority preview (puan hesabı) ayrı kalır. Yetkilendirme ve kontenjan
henüz yok; StudentId alan geliştirme akışı güvenli öğrenci self-servisi değildir.
İki istek aynı yük/programı okuyabilir; eşzamanlı kredi/program garantisi yoktur.

`CourseOfferingTimeSlot`: DB tarafından sequence/default Id, açılışa zorunlu FK
(NO ACTION), `DayOfWeek` sayısal0..6, zorunlu `time(7)` başlangıç/bitiş.
Aralıklar `[başlangıç,bitiş)`; 09–10 ve10–11 çakışmaz. Aynı gün içinde
başlangıç<bitiş şarttır. Üniversite yerel haftalık saatleri kullanılır; gece
geçişi, timezone/tatil/haftaya özel istisna yoktur. Farklı dönemler karşılaştırılmaz.
Saat kaydı olmayan açılış için yalnız **bilinen aralıklarla çakışma yok** kabulü
vardır; program eksiksizliği iddiası yoktur. Birebir UNIQUE farklı ama örtüşen
aralıkları engellemez. Aynı açılış çifti birçok saatte çakışsa bile tek neden döner.

Varsayılan seed saat atamaz. `sql/app/time-slot-example.sql` yalnız işaretli
izole day6 test DB'sinde kendi sentetik ders/dönem/açılışını oluşturur ve her
zaman rollback yapar; app/lab hedefini reddeder. Mevcut açılışlar değiştirilmez.
Tekrar çalışır; sequence boşlukları normaldir. Zaman CRUD endpoint'i yoktur.

Repo kökünden, uygulamayı durdurup hedefin `university_registration_app` olduğunu
kontrol ederek (bağlantı yapılandırması yukarıdaki güvenli yöntemle):

```powershell
dotnet tool restore
dotnet restore
dotnet build
dotnet ef migrations script AddPrerequisitesAndCompletedCourses AddCourseOfferingTimeSlots --output obj/day15-migration.sql
# SQL'i incele; migration oluşturmak/script üretmek DB'yi değiştirmez.
dotnet ef database update
dotnet ef database update
dotnet ef migrations has-pending-model-changes
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -d university_registration_app -b -f 65001 -i sql/app/verify.sql
dotnet run --project .\UniversityRegistration.csproj
```

İzole gerçek LocalDB/HTTP testleri; app/lab test hedefi değildir:

```powershell
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode eligibility
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode specifications
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
# full çıktısındaki yalnız bu koşuya ait işaretli DB adını kullan:
# sqlcmd -S '(localdb)\MSSQLLocalDB' -E -d <izole-test-DB-adı> -b -f 65001 -i sql/app/time-slot-example.sql
```

Yeni kontrol saf saat hesabı, gerçek time(7) tick gidiş-dönüşü, PK/FK/CHECK/UNIQUE,
üçlü ret, önizleme sonrası değişen program ve yazmama davranışını sınar. Mevcut
batch ilk INSERT sonrası rollback/FK/unique deneyleri yeniden kullanılır.
İlgili veri hazırlığında sekiz toplu SELECT vardır; açılış/saat başına sorgu yok.
Temizlik yalnız fixture kimlikleriyle saat/ilişki/Enrollment ve üst kayıt sırasıyla,
finally içinde yapılır; süreçler kapatılır. Down çalıştırılmadı.

Ayrıntılar: [öğrenme notu](docs/day15-eligibility.md),
[karar kaydı](docs/adr/0015-explainable-eligibility.md), [test kılavuzu](docs/testing.md).

## 16. gün — Kontrollü kayıt durumları

Yeni Enrollment **Draft** başlar. Durum dışarıdan atanamaz; entity metotları
geçişi korur. İzin verilenler:

| Mevcut durum | İşlem | Yeni durum | Kredi/program yükü |
|---|---|---|---|
| Draft | submit | Submitted | Dahil olur |
| Submitted | approve | Approved | Dahil kalır |
| Submitted | reject | Rejected | Hariç olur |

Draft/Rejected mevcut yükten hariç, Submitted/Approved dahildir. Diğer bütün
geçişler ve aynı işlemin tekrarı409'dur. Rejected yeniden açılmaz; durumdan
bağımsız öğrenci–açılış UNIQUE nedeniyle aynı çift için yeniden POST da409'dur.

```http
POST /enrollments/501/submit
```

```http
POST /enrollments/501/approve
```

İkisinde de gövde gerekmez. Başka bir Submitted kaydı reddetme örneği:

```http
POST /enrollments/502/reject
Content-Type: application/json

{"reason":"  Başvurudaki bilgiler yeniden değerlendirilmeli.  "}
```

Başarı200 ve güncel kayıt. Örnek (kimlik/zaman temsili):

```json
{
  "id":502,
  "studentId":1,
  "courseOfferingId":102,
  "createdAtUtc":"2026-10-29T10:00:00+00:00",
  "status":"Rejected",
  "rejectionReason":"Başvurudaki bilgiler yeniden değerlendirilmeli."
}
```

**Bilinçli sözleşme genişlemesi:** tekli oluşturma, batch items, detay, öğrenci
kayıt listesi ve geçiş yanıtlarında `status` isim olarak, `rejectionReason` nullable
alan olarak döner. Yeni kayıt201/Location davranışı korunur; status=Draft,
rejectionReason=null. İstemci Id/Status/gerekçe atayarak oluşturma yapamaz;
oluşturma request türlerinde bu alanlar yoktur. Genel status PATCH/PUT yoktur.

Submit/approve: kayıt yok404 → yanlış mevcut durum409 → güncel akademik
ret400/failures → geçiş ve SaveChanges →200. Hedef kayıt kendi yükünden çıkarılır,
aday olarak bir kez değerlendirilir; öğrencinin diğer aktif kayıtları korunur.
Reject: gerekçe Trim sonrası boş veya500 üstüyse400/`reason` (varlık kontrolünden
önce); sonra yok404 → Submitted değil409 → geçiş/yazma200. Reject akademik
uygunluğu yeniden hesaplamaz. Bozuk JSON binding400 olarak kalır.

Yeni Draft oluşturma ve batch uygunluk kontrollerini atlamaz. Batch adayları
birlikte değerlendirilir ve tek SaveChanges ile atomik kalır. Önizleme yeni aktif
yük politikasını kullanır, rezervasyon yapmaz. Gerçek kayıt/gönderme/onaylama
kendi anında yeniden değerlendirilir.

`20261029131736_AddEnrollmentLifecycle` mevcut satırları **Approved** yapar;
bu eski doğrudan kayıt anlamını korur, geçmiş danışman onayı/audit iddiası değildir.
Kalıcı DB varsayılanı Draft0'dır. Status required int/CHECK0..3;
RejectionReason nullable nvarchar(500), Rejected için non-null ve boşluk dışı,
diğerlerindeNULL. CHECK önceki durumu veya yetkiyi bilemez.

Migration sırasında **uygulamayı ve bütün yazıcıları durdurun**. Güvenli bağlantı
ayarının yalnız `university_registration_app` hedeflediğini kontrol edip kökten:

```powershell
dotnet tool restore
dotnet restore
dotnet build
dotnet ef migrations script AddCourseOfferingTimeSlots AddEnrollmentLifecycle --output obj/day16-migration.sql
# Up/Down ve SQL'i incele; tablo yeniden oluşturma yok.
dotnet ef database update
dotnet ef database update
dotnet ef migrations has-pending-model-changes
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -d university_registration_app -b -f 65001 -i sql/app/verify.sql
dotnet run --project .\UniversityRegistration.csproj
```

Testler app/lab yerine yeni, adı doğrulanan izole LocalDB kullanır:

```powershell
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode state
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode specifications
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode eligibility
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
```

`unit` DB olmadan enum ve üretim dışı State deneyini aynı matrisle sınar.
`state` eski migration seviyesinde kayıt hazırlayıp yükseltmeyi, HTTP geçişlerini,
SQL kısıtlarını, yük/dışlama ve restart davranışını sınar. Full eski fault/rollback
kontrollerini yeniden kullanır. Test kayıtları finally içinde FK sırasıyla
silinir, süreçler kapanır; test DB şemaları tutulur. Sequence geri sarılmaz.

**Geliştirme sınırı:** submit/approve/reject henüz kimlik doğrulama ve
yetkilendirme içermez; gerçek danışman onay sistemi değildir. Eşzamanlı geçişlerde
son yazan kazanabilir; kredi/program ve kontenjan garantisi yoktur. Rejected
satır için tekrar başvuru/ders bırakma, audit/outbox ve bildirim yoktur. Down
çalıştırılmadı ve durum/gerekçe bilgisini koruyan bir geri alma sayılmaz.

[Öğrenme notu](docs/day16-enrollment-state.md) ·
[ADR](docs/adr/0016-controlled-enrollment-lifecycle.md) · [Test kılavuzu](docs/testing.md)

## Gün 17 — Akademik hesaplama süresi

`IAcademicEligibilityEvaluator`, hazırlanmış verilerle çalışan senkron sözleşmedir.
`AcademicEligibility` saf kuralları uygular; `TimingAcademicEligibilityEvaluator`
aynı sonucu değiştirmeden iletir. `Program.cs` somut değerlendiriciyi ve onu saran
interface kaydını scoped kurar. `EnrollmentOperations.EvaluateAsync` DB hazırlığını
bitirdikten sonra bu interface'i çağırır: oluşturma, batch, eligibility, submit ve
approve ortak sınırı kullanır. Erken girdi/varlık/mükerrer retlerinde hesaplama yoktur.

Yapılandırılmış log: EventId **1701**, adı **AcademicEligibilityCalculation**;
`ElapsedMs`, `Outcome` (`eligible`, `ineligible`, `failed`), `FailureCount`.
Sonuç üretilemediyse sayı null'dır; akademik ret teknik hata değildir. Süre yalnız
**akademik değerlendirme hesaplama süresi**dir. DB sorguları, SaveChanges,
HTTP yanıtının gönderilmesi ve toplam istek süresi dahil değildir. Log yayımı
Stopwatch durduktan sonradır. Bu ölçüm benchmark veya süre garantisi değildir.

Sonuç/ret sırası, exception akışı, JSON sözleşmeleri, durumlar ve batch'in tek
SaveChanges sınırı korunur. Yeni migration/paket yoktur. Log sağlayıcısı hata
atarsa sonucu veya asıl hatayı gölgeleyebilir; genel fallback kurulmadı.

Repo kökünden (mevcut bağlantı ve kredi politikası yapılandırmasıyla):

```powershell
dotnet restore
dotnet build
dotnet run --project .\UniversityRegistration.csproj
# Ayrı terminal:
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode decorator
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
dotnet ef migrations has-pending-model-changes
```

`unit` saf decorator/async/sıra deneylerini DB olmadan çalıştırır. `decorator`,
mevcut eligibility ve state senaryolarını yeni izole LocalDB'de yeniden kullanır;
gerçek API'nin yapılandırılmış konsol olaylarını bellekten doğrular. Log dosyası
üretmez. Deneyler `scripts` altındadır ve web derlemesine/DI'a dahil değildir.
[Test düzeni](docs/testing.md) · [Öğrenme notu](docs/day17-decorator.md) ·
[ADR](docs/adr/0017-academic-timing-decorator.md)

## Gün 18 — Tamamen sahte bildirim adapter'ları

Bu öğrenme örneği yalnız `scripts/NotificationAdapterDemo.cs` içinde çalışır;
web derlemesine, DI kayıtlarına, onay/ret/kayıt akışlarına veya DB'ye bağlı değildir.
Ağ erişimi, gerçek e-posta/SMS hesabı ve gerçek alıcı yoktur. Test adresleri
`example.test` kullanır; Fake sağlayıcılar gerçek SDK uyumluluğu iddia etmez.

`INotificationSender` iki farklı API'yi ortak mesaj/sonuçla kullanır:

| Ortak sonuç | Anlam |
|---|---|
| Accepted | Sahte sağlayıcı isteği kabul etti; teslim/okunma garantisi değil |
| RecipientRejected | Bilinen alıcı reddi |
| ProviderUnavailable | Bilinen sağlayıcı kesintisi; güvenli retry garantisi değil |

Mesaj alanları null/boş/yalnız boşluk olamaz; geçerli metinler trim edilmeden ve
kesilmeden taşınır. Referans varsa aynen korunur, yoksa üretilmez. Başarısız
sonuçta referans bulunmaz. Çelişkili/bilinmeyen sağlayıcı cevapları exception'dır;
beklenmeyen exception ve cancellation normal ret sonucuna dönüştürülmez.

Repo kökünde:

```powershell
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode adapter
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
```

`adapter` DB/ağ kullanmadan sonuç/alan/token/çağrı sayısı ve async hata/iptali
kontrol eder; `unit` ve `full` de bu kontrolleri içerir. Retry, fallback, queue,
Outbox, kalıcı bildirim, yeni endpoint veya Facade yoktur. Adapter DB kaydıyla
haricî gönderimi atomik yapmaz; timeout/yanıt kaybı belirsizliğini çözmez.
[Öğrenme notu](docs/day18-notification-adapter.md) ·
[ADR](docs/adr/0018-demo-notification-adapters.md) · [Test kılavuzu](docs/testing.md)

## Gün 19 — Dönem kayıt özeti (Facade)

`GET /students/{studentId:int}/terms/{termId:int}/registration-summary`
öğrencinin seçilen dönemini tek bir işlemde sunar. Pozitif olmayan kimlikler
alan bazlı400; önce öğrenci, sonra dönem yokluğu404; iki kaynak da mevcut fakat
kayıt yoksa200, sıfır sayılar ve boş listeler döner.

Örnek: `GET /students/1/terms/10/registration-summary`:

```json
{
  "studentId": 1,
  "academicTermId": 10,
  "activeCredits": 9,
  "counts": { "draft": 1, "submitted": 1, "approved": 1, "rejected": 1 },
  "enrollments": [
    { "id": 501, "courseOfferingId": 101, "courseCode": "CSE101", "courseName": "Giriş", "credits": 3, "status": "Draft", "rejectionReason": null },
    { "id": 502, "courseOfferingId": 102, "courseCode": "CSE201", "courseName": "Veri Yapıları", "credits": 4, "status": "Submitted", "rejectionReason": null },
    { "id": 503, "courseOfferingId": 103, "courseCode": "CSE203", "courseName": "Veritabanı Yönetimi", "credits": 5, "status": "Approved", "rejectionReason": null },
    { "id": 504, "courseOfferingId": 104, "courseCode": "CSE301", "courseName": "Örnek Ders", "credits": 6, "status": "Rejected", "rejectionReason": "Örnek ret gerekçesi" }
  ],
  "activeSchedule": [
    { "courseOfferingId": 102, "day": "Monday", "startsAt": "09:00:00.1234567", "endsAt": "10:00:00" }
  ],
  "unscheduledActiveOfferingIds": [103]
}
```

Kimlikler örnektir; gerçek hedef dönemin bütün kayıtları gelir. counts bütün
statüleri, activeCredits (`long`) ve program yalnız Submitted/Approved kayıtları
kapsar. Her enrollment güncel Course.Credits ile bir kez sayılır; aynı dersin farklı
şubeleri ayrı kayıt olarak yük oluşturur. Saat sayısı kredi toplamını çoğaltmaz.

Kayıtlar Id artan; program Pazartesi→Pazar, başlangıç, bitiş, açılış Id ve iç slot
Id sırasındadır. TimeOnly/time(7) saniye altı değerleri kırpılmaz. Saatsiz aktif
kimlikler artan döner; bu liste eksik program bilgisini görünür kılar, uygunluk
reddi değildir. Boş program eksiksizlik garantisi vermez.

Scoped `TermRegistrationSummaryFacade` öğrenci/dönem varlığını ve daraltılmış
projection'ları sıralı await ile hazırlar. HTTP kararları endpoint'tedir. Yazma,
iç HTTP çağrısı, yeni akademik kural veya migration yoktur. Sayılar ve kredi aynı
kayıt listesinden hesaplanır; ayrı saat sorgusuyla beraber **tek snapshot garantisi
yoktur**. Bu özet rezervasyon, uygunluk veya çakışma raporu değildir. Endpoint
henüz yetkilendirilmemiş geliştirme akışıdır; öğrenci self-servis güvenliği sağlamaz.

Repo kökünden, mevcut güvenli bağlantı ayarıyla:

```powershell
dotnet restore
dotnet build
dotnet run --project .\UniversityRegistration.csproj
# Ayrı terminalde izole LocalDB kontrolleri:
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode summary
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
dotnet ef migrations has-pending-model-changes
```

[Öğrenme notu](docs/day19-registration-summary-facade.md) ·
[ADR](docs/adr/0019-term-registration-summary-facade.md) · [Test kılavuzu](docs/testing.md)

## Gün 20 — Test senaryosu Builder

Üretim davranışı değişmedi. Bellek içi karmaşık uygunluk fixture'ları için
`scripts/EligibilityScenarioBuilder.cs` eklendi; DB fixture veya genel entity
builder değildir. Bağımsız Build sonuçları, kopyalama ve yapısal koşullar:
[Test kılavuzu](docs/testing.md) · [Öğrenme notu](docs/day20-eligibility-scenario-builder.md) ·
[ADR](docs/adr/0020-eligibility-scenario-builder.md).

```powershell
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode builder
```

## Gün 21 — Kesinleşmiş onay olayı

Onaylama, başarılı bağımsız `SaveChangesAsync` sonrasında `EnrollmentApproved`
üretir. Scoped publisher observer'ları sırayla await eder. Üretimde yalnız
`ApprovalLogObserver` vardır: kararlı `EnrollmentApprovalObserved` (2101) olayı,
EventId ve EnrollmentId loglanır. HTTP/JSON sözleşmeleri değişmedi.

Kritik olmayan observer hatası `EnrollmentApprovalObserverFailed` (2102), EventId,
EnrollmentId, ObserverType ve ExceptionType alanlarıyla güvenli biçimde kaydedilir;
sonraki bağımsız observer çalışır. Onay başarısı observer başarısından farklıdır.
Çağıran iptali sonraki observer'ları durdurur, kesinleşmiş onayı geri almaz; istemcinin
kopmuş bağlantıdan yanıt alacağı garanti edilmez. İptal edilmeyen token ile gelen
OperationCanceledException observer arızasıdır. Null olay ArgumentNullException'dır.
Logger sağlayıcısının arızası için fallback yoktur.

Olay kalıcı değildir: commit/yayın arasında süreç kapanması veya kısmi dağıtımda
olay kaybolabilir. EventId, retry/dedup/exactly-once sağlamaz. Eşzamanlı onay yarışı
çözülmedi. DB transaction'ı dış gönderimi atomik yapmaz; integration event/broker,
gerçek bildirim veya audit kaydı yoktur. Dış transaction içinde onay işlemi erken
reddedilir; henüz commit edilmemiş SaveChanges sonucu yayın için kullanılamaz.

Repo kökünden mevcut bağlantı yapılandırmasıyla:

```powershell
dotnet run --project .\UniversityRegistration.csproj
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode observer
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
```

[Öğrenme notu](docs/day21-approval-observers.md) ·
[ADR](docs/adr/0021-committed-approval-observers.md) · [Test kılavuzu](docs/testing.md)

## Gün 22 — Veri erişim sınırları

Üretimde doğrudan scoped `UniversityDbContext` korunuyor. İzlenen geçiş entity'leri,
DTO projection ve toplu uygunluk sorgularını genel CRUD altında birleştirmek somut
kazanç sağlamıyor. Üretime repository/IUnitOfWork eklenmedi; HTTP, şema, batch tek
SaveChanges ve onay sonrası olay akışı değişmedi.

Üretim dışı `DemoEnrollmentRepository` yalnız tracked Find ve Added yapan Add sunar;
SaveChanges/commit ve context dispose çağırana aittir. Gerçek LocalDB karşılaştırması:

```powershell
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode repository
```

[Test kılavuzu](docs/testing.md) · [Öğrenme notu](docs/day22-persistence-boundaries.md) ·
[ADR](docs/adr/0022-direct-ef-and-repository-experiment.md).

## Gün 23 — Kayıt komutu ve katalog sorgusu

Üretimde aynı uygulama/DB ve **doğrudan çağrı** kullanılır; Mediator yoktur.
`Application/CreateEnrollmentCommand` pozitif kimliklerle kontrollü oluşturulur,
mevcut `EnrollmentOperations.CreateAsync` koordinasyonu yürütür. HTTP request'i
ayrı kalır; alan hataları mevcut400, mükerrer409 ve başarı201 sözleşmesini korur.

`Application/CourseCatalogQuery` normalize arama, kredi ve güvenli sayfa sınırlarını;
`CourseCatalogReader` SQL filtre/count/sıralama/sayfalama/DTO projection'ını üstlenir.
`CourseCatalogEndpoint` bağlama/doğrulama ve HTTP200 ile ilgilenir. HTTP ve doğrudan
uygulama çağrısı aynı doğrulama fonksiyonlarını kullanır; doğrudan geçersiz nesne
oluşturma programlama hatası olarak ArgumentOutOfRangeException üretir.

`/courses` ve `/courses/catalog` JSON sözleşmeleri değişmedi. Query yazmaz; Count ve
items hâlâ iki sorgu olup tek snapshot garantisi vermez. Batch tek SaveChanges ve
onay sonrası observer sınırı değişmedi. Yeni migration/paket/transaction pipeline yok.

```powershell
dotnet run --project .\UniversityRegistration.csproj
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode cqrs
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
```

CQRS modu üretim giriş sınırlarını ve iki typed istekli Mediator deneyini DB olmadan
çalıştırır. Gerçek SQL/HTTP kanıtı full içindeki mevcut katalog/kayıt kontrolleridir.
[Öğrenme notu](docs/day23-cqrs-and-mediator.md) ·
[ADR](docs/adr/0023-command-and-catalog-query.md) · [Test kılavuzu](docs/testing.md).

## 24. gün — Identity hesabı ve öğrenci profili

`Models/ApplicationUser.cs` varsayılan string kimlikli IdentityUser hesabıdır;
`Student` akademik profil olarak kalır. `Student.UserId` isteğe bağlıdır ve yalnız
`TryLinkAccount` ile bir kez bağlanabilir. Dolu kimlikleri benzersiz yapan filtreli
index ve NO ACTION FK, aynı hesabın iki öğrenciye bağlanmasını ve bağlı hesabın
fiziksel silinmesini engeller. Birden fazla bağlantısız öğrenci ve öğrencisiz hesap
geçerlidir. Ad/e-posta/öğrenci numarası üzerinden otomatik eşleştirme yoktur.

`UniversityDbContext`, IdentityDbContext<ApplicationUser> tabanını kullanır;
`Program.cs` yalnız AddIdentityCore ve EF store kaydeder. Identity EF paketi,
EF/araçlarla aynı **10.0.14** sürümündedir. Temel modelin yedi Identity tablosu rol
verisini de içerir; rol servisleri/atama ve giriş akışı kurulmadı.

**Mevcut endpoint'ler hâlâ authentication/authorization ile korunmuyor.**
Public hesap oluşturma/giriş/hesap–öğrenci bağlama endpoint'i, cookie/JWT,
SignInManager veya otomatik hesap seed'i yok. Testte UserManager ile oluşturulan
parolasız hesap, giriş yapılabildiği anlamına gelmez. API response'ları değişmedi.

Proje kökünde, uygulama DB'sine yazan API süreçleri duruyorken:

```powershell
dotnet restore
dotnet tool restore
dotnet build --no-restore
# Mevcut UniversityDatabase ayarı yalnız university_registration_app hedeflemeli.
dotnet ef migrations script 20261029131736_AddEnrollmentLifecycle 20261101104350_AddIdentityAccounts --output obj/day24-migration.sql
# Üretilen SQL'i inceleyin; lab DB'sine uygulamayın.
dotnet ef database update
dotnet ef database update
dotnet ef migrations has-pending-model-changes
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -d university_registration_app -b -i sql/app/verify.sql
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode identity-model
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
dotnet run --no-build --urls http://127.0.0.1:5080
```

Migration üretmek DB'yi değiştirmez; update ayrı, açık adımdır. Yeni migration
Identity tablolarını ve Student.UserId bağlantısını ekler; eski öğrenciler NULL
bağlantıyla kalır. Akademik tablolar yeniden oluşturulmaz. App/lab ayrı amaçlarını
korur; mevcut akademik seed hesap oluşturmaz.

24. gün restore/build, unit/core/full ve identity-model geçti. İzole önceki şema
üzerindeki öğrenci/enrollment/başarı ilişkileri migration sonrasında korundu.
App update iki kez çalıştı; ikinci sefer değişiklik yok, snapshot uyumlu ve sekiz
akademik tablonun eski sütun değerleri aynı. Hesap oluşturma ve sonraki profil
kaydetmesinin ayrı sınırları gerçek UserManager/SQL hatasıyla gösterildi.
Down uygulanmadı. Ayrıntılar: [öğrenme notu](docs/day24-identity-and-student.md),
[ADR](docs/adr/0024-identity-and-student-profiles.md), [test kılavuzu](docs/testing.md).

## 25. gün — Hesap oluşturma ve yerel e-posta doğrulama

`POST /accounts/register`:

```json
{ "email": "student@example.test", "password": "<yerelde girilen en az 12 karakterlik parola>" }
```

Başarı **201**, Location/cookie/access token olmadan:

```json
{ "id": "hesap-kimliği", "emailConfirmationRequired": true, "confirmationDispatchStatus": "queued-locally" }
```

E-posta yalnız Trim edilir ve UserName olarak da kullanılır. Nokta/+ etiketleri
silinmez; normalized alanları Identity hazırlar. Parola değiştirilmez: **12–128
karakter**, null/boş/yalnız boşluk ret; büyük/küçük harf, sayı ve sembol bileşimi
zorunlu değil. Bu uzunluk odaklı ders politikası tam üretim güvenliği değildir.
RequireUniqueEmail açıktır; DB yarış koruması mevcut **UserNameIndex**'ten gelir,
EmailIndex unique değildir. Bu akış e-posta/kullanıcı adı eşitliğini korur.

Alan/Identity doğrulaması400; tekrar e-posta/kullanıcı adı için
`code: account_creation_failed` ve `errors.email`. Harf farkı ve baş/son boşlukla
tekrar aynı politikaya tabidir. Başarı/ret ayrımı hesap varlığını açığa çıkarabilir;
tam enumeration, rate-limit ve lockout çözümü eklenmedi. Beklenmeyen DB hatası500
olur. Student profili oluşturulmaz veya hesaba bağlanmaz.

Yerel kutu **varsayılan kapalıdır**; kapalı register503 verir ve hesap yazmaz.
Yalnız Development ortamında açıkça etkinleştirin. Production'da etkin olması
başlangıç hatasıdır. Proje kökünde:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Accounts__LocalMailboxEnabled = 'true'
$env:Accounts__ConfirmationPageUrl = 'http://127.0.0.1:5080/account-confirmation'
dotnet run --no-launch-profile --urls http://127.0.0.1:5080
```

Bu URL güvenilir yapılandırmadır; Host başlığından üretilmez. HTTPS beklenir;
yalnız yerel geliştirmede loopback HTTP kabul edilir. Hesap/parola isteklerini
uzak bir sunucuya HTTP üzerinden göndermeyin. Bu örnekteki GET adresi için sayfa
**yoktur**: bağlantıyı açmak doğrulamayı tamamlamaz. Dosyadan değerleri okuyup
`POST /accounts/confirm-email` gönderin:

```json
{ "userId": "hesap-kimliği", "token": "dosyadaki-Base64Url-token" }
```

Başarı200: `{ "emailConfirmed": true }`. Eksik alanlar alan bazlı400; bilinmeyen
hesap, bozuk kodlama ve geçersiz token ortak `400 / confirmation_failed` döner.
Hesap zaten doğrulanmış olsa bile token denetlenir.

Mesajlar `.local/accounts/messages` altında ayrı Guid dosyalarıdır; alıcı, konu ve
bağlantı içerir, parola/hash içermez. Kutu ve `.local/accounts/keys` Git dışındadır,
wwwroot altında değildir; public listeleme endpoint'i yoktur. Bağlantı/token'ı
terminale dökmeyin. Bir mesajı değişkene alıp POST etmek için (PowerShell):

```powershell
$messageFile = Get-ChildItem .local/accounts/messages/*.json | Sort-Object LastWriteTime | Select-Object -Last 1
$message = Get-Content -LiteralPath $messageFile.FullName -Raw | ConvertFrom-Json
$query = [System.Web.HttpUtility]::ParseQueryString(([uri]$message.link).Query)
$body = @{ userId = $query['userId']; token = $query['token'] } | ConvertTo-Json
Invoke-RestMethod http://127.0.0.1:5080/accounts/confirm-email -Method Post -ContentType application/json -Body $body
Remove-Variable message,query,body
```

Gerçek e-posta gönderilmez. `queued-locally` yalnız dosyanın yazıldığını belirtir;
broker kuyruğu/teslim garantisi değildir. Mesaj/token hazırlığı hata verirse hesap
oluşmuş kalır ve201 yanıtı `failed` taşır. Commit sonrası çağıran iptalinde
`cancelled` kullanılır; yanıtın istemciye ulaşması garanti edilmez. Otomatik hesap
silme, retry veya resend endpoint'i yoktur.

Identity Data Protection token'ı **iki saat** geçerlidir. Gerçek deneyde aynı token
başarıdan sonra tekrar kabul edildi, yeni token eskisini iptal etmedi. Security
stamp değişince eski token reddedildi. Süreli token tek kullanımlık değildir.
UTF8/Base64Url kodlama şifreleme değildir. Aynı uygun key ring/uygulama adıyla
restart doğrulaması geçti. Windows'ta key-at-rest DPAPI kullanılır; dosya izinleri
ve aynı kullanıcı bağlamı önemlidir. Çok instance/anahtar rotasyonu tasarlanmadı.

Giriş, çıkış, cookie/JWT, e-posta doğrulamasına bağlı authorization ve öğrenci
sahiplenme bugün yoktur; eski akademik endpoint'ler hâlâ korunmamaktadır.
Şema değişmedi, migration eklenmedi. Mevcut app DB'nin 24. gün migration'ı yeterlidir.

```powershell
dotnet restore
dotnet build --no-restore
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode accounts
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
dotnet ef migrations has-pending-model-changes
```

[Öğrenme ve resmi kaynaklar](docs/day25-account-confirmation.md) ·
[ADR](docs/adr/0025-local-account-registration.md) · [Test kılavuzu](docs/testing.md).

## Gün 26 — Cookie ile giriş/çıkış ve korunan hesap

24–25. gün hesabı oluşturur/doğrular; bu gün giriş ekler. Mevcut akademik endpoint'ler
halen yetkilendirilmiş öğrenci self-servisi değildir. Hesap öğrenci profili olmadan
giriş yapabilir; Student bağlantısı otomatik kurulmaz.

| Endpoint | Başarı | Ret |
|---|---|---|
| GET /accounts/antiforgery | 200 requestToken + CSRF cookie (no-store) | HTTPS gerekir |
| POST /accounts/login | 200 + Identity uygulama cookie'si | CSRF/alan/JSON 400; diğer giriş retleri ortak 401 |
| GET /accounts/me | 200 id/email | 401; redirect/HTML yok |
| POST /accounts/logout | 204 + cookie silme | anonim 401; CSRF 400 |

Login yalnız email/password alır. E-posta Trim, parola değişmeden; geçerli biçim,
email en fazla 256/parola en fazla 128 karakter. Doğrulanmış e-posta gerekir.
Yanlış parola/olmayan hesap/doğrulanmamış/kilitli hesap aynı 401 yanıtını alır.
Identity lockout: yeni hesaplarda açık, 5 başarısız parola denemesinde 5 dakika.
"Beni hatırla" yok. Cookie __Host-University.Account, HttpOnly, Secure Always,
SameSite Strict; bilet 20 dakika, sliding kapalı, kalıcı tarayıcı cookie'si değil.
HTTP'de Secure cookie gönderilmez; bu akış HTTPS ile kullanılmalıdır.

Önce güvenilen yerel HTTPS sertifikanızı hazırlayın (`dotnet dev-certs https --trust`
güven deposunu değiştirir; yalnız kendi geliştirme makinenizde bilinçli çalıştırın).
Mevcut DB bağlantısı yapılandırılmışken:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --no-launch-profile --urls https://localhost:7080
```

Yeni hesap/confirmation gerekiyorsa 25. gün yerel kutu ayarlarını ayrıca etkinleştirin;
login için kutunun açık olması gerekmez. Aşağıdaki PowerShell 7 örneğinde test/development
hesabının e-postasını ve parolasını etkileşimli girin. Sertifika hatasını atlamayın.

```powershell
$base='https://localhost:7080'
$session=[Microsoft.PowerShell.Commands.WebRequestSession]::new()
$csrf=Invoke-RestMethod "$base/accounts/antiforgery" -WebSession $session
$email=Read-Host 'Doğrulanmış hesabın e-postası'
$credential=Get-Credential -UserName $email -Message 'Yerel hesap parolası'
$body=@{ email=$email; password=$credential.GetNetworkCredential().Password } | ConvertTo-Json
try {
    Invoke-RestMethod "$base/accounts/login" -Method Post -WebSession $session `
        -Headers @{'X-CSRF-TOKEN'=$csrf.requestToken} -ContentType application/json -Body $body
} finally { Remove-Variable body,credential }
Invoke-RestMethod "$base/accounts/me" -WebSession $session
# Kimlik değişti: anonim token'ı logout için kullanmayın.
$csrf=Invoke-RestMethod "$base/accounts/antiforgery" -WebSession $session
Invoke-WebRequest "$base/accounts/logout" -Method Post -WebSession $session `
    -Headers @{'X-CSRF-TOKEN'=$csrf.requestToken} | Select-Object StatusCode
Invoke-WebRequest "$base/accounts/me" -WebSession $session -SkipHttpErrorCheck | Select-Object StatusCode
# Son çağrı 401. Cookie/token değişkenlerini terminale yazdırmayın.
Remove-Variable csrf,session
```

Antiforgery token kimlik bileti değildir; header ve ayrı cookie birlikte doğrulanır.
Login/logout POST'ları korunur; SameSite tek başına koruma sayılmaz. Çıkış, istemcinin
silme talimatını işlemesine dayanır; kopyalanmış biletin tüm cihazlarda anında iptali
veya genel oturum tablosu yoktur. Data Protection anahtar alanı değişmedi; key dosyaları
repoya girmez. Yeni rol/ownership/2FA/JWT akışı eklenmedi.

```powershell
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode cookie
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
dotnet ef migrations has-pending-model-changes
```

[Öğrenme notu ve resmi kaynaklar](docs/day26-cookie-sessions.md) ·
[Karar kaydı](docs/adr/0026-identity-cookie-session.md).

## Gün 27 — Ayrı JWT bearer doğrulaması

Cookie varsayılanı korunur. `GET /accounts/bearer-me` yalnız UniversityBearer şemasını
kabul eder; başarı `{ "accountId": "..." }`, yok/geçersiz token401. Cookie tek başına
bu endpoint'e yetmez; bearer da `/accounts/me` veya dev-token'a yetmez. Akademik
endpoint'lere yeni yetkilendirme eklenmedi.

JWT varsayılan olarak kapalıdır. Repo dışında, Windows kullanıcı ACL'iyle korunan
bir geliştirme anahtar dizini oluşturun; script mevcut dizini/anahtarı değiştirmez:

```powershell
pwsh -NoProfile -File scripts/New-DevelopmentJwtKey.ps1
$keyDir=Join-Path $env:LOCALAPPDATA 'UniversityRegistration/jwt-development'
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:Jwt__Enabled='true'
$env:Jwt__DevTokenEnabled='true'
$env:Jwt__Issuer='https://university.local.example'
$env:Jwt__Audience='university-registration-api'
$env:Jwt__PublicKeyPath=Join-Path $keyDir 'public.pem'
$env:Jwt__PrivateKeyPath=Join-Path $keyDir 'private.pem'
dotnet run --no-launch-profile --urls https://localhost:7080
```

Güvenilen yerel HTTPS sertifikası ve mevcut DB bağlantısı gerekir; sertifika
hatalarını atlamayın. Anahtar içeriğini appsettings, terminal veya Git'e koymayın.
PEM şifreli değildir; dosya izinlerini koruyun. Anahtar kaybı/değişimi eski JWT'leri
etkiler; otomatik key üretimi/rotasyonu yok. Production'da DevTokenEnabled=true
başlangıç hatasıdır; yalnız doğrulama için Enabled/Issuer/Audience/PublicKeyPath
kullanılır. PrivateKeyPath yüklenmez. Dev-token route'u kapalıysa 404'tür.

26. gün örneğiyle antiforgery → cookie login yaptıktan sonra, aynı `$session` ile:

```powershell
$base='https://localhost:7080'
$csrf=Invoke-RestMethod "$base/accounts/antiforgery" -WebSession $session
$issued=Invoke-RestMethod "$base/accounts/dev-token" -Method Post -WebSession $session `
    -Headers @{'X-CSRF-TOKEN'=$csrf.requestToken}
$headers=@{Authorization="Bearer $($issued.accessToken)"}
Invoke-RestMethod "$base/accounts/bearer-me" -Headers $headers
# $issued, $headers veya token değerini terminale yazdırmayın.
Remove-Variable issued,headers,csrf
```

Önceki örneğin logout adımından önce bu bloğu çalıştırın. Dev-token gövde alanı almaz;
kimlik/süre/claims sunucudan gelir. Gün 28 ile yanıt accessToken/tokenType=Bearer/expiresIn/refreshToken,
Cache-Control:no-store'dur. Token URL'ye konmaz. Bu standart OAuth token endpoint'i
veya gerçek genel token sunucusu değildir.

RS256; sub/jti/iss/aud/iat/nbf/exp; on dakika süre ve 30 saniye clock skew. JWT
şifrelenmez; kişisel/hesap secret'ları veya rol içermez. İmza, issuer, audience ve
süre zorunlu; yalnız tek dolu string sub kabul edilir. Cookie logout, parola değişimi
ve hesap değişimi JWT'yi kendiliğinden iptal etmez. jti tekrar kullanımı engellemez.
Bearer doğrulaması DB hesabı/oturumu sorgulamaz. Gün 28 refresh ekler; access token revocation/denylist yoktur.

```powershell
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode jwt
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode cookie
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
dotnet ef migrations has-pending-model-changes
```

[Öğrenme notu/resmi kaynaklar](docs/day27-jwt-validation.md) ·
[Karar kaydı](docs/adr/0027-separate-jwt-bearer.md).

## Gün 28 — Kalıcı refresh oturumu ve sıralı rotation

> Bu bölüm gün28 kararını açıklar; gün29 aşağıda güncel-özet modelini token geçmişi ve aile kilidiyle genişletir.

`RefreshSession` Identity hesabına bağlıdır, StudentId değildir. DB yalnız güncel
refresh token'ın SHA-256 özetini `binary(32)` olarak tutar; UNIQUE index ve NO ACTION
hesap FK'si vardır. İlişkili oturum varken hesabın fiziksel silinmesi reddedilir.
CreatedAtUtc < ExpiresAtUtc ve varsa RevokedAtUtc >= CreatedAtUtc kısıtları bulunur.
Güncel zaman/iptal denetimi uygulamadadır; otomatik oturum temizleme yoktur.

Gün 27'nin Development + Jwt:Enabled + Jwt:DevTokenEnabled anahtar kurulumunu kullanın.
Bugün `/accounts/refresh` de bu öğretim üreticisiyle birlikte açılır; Production yalnız
public anahtarla doğrulama yapmaya devam eder. Özel anahtar veya token üretimi fallback'i
ve genel OAuth sunucusu eklenmedi. Her açık dev-token çağrısı ayrı oturum oluşturur.
`Jwt:AccessTokenSeconds=600` (1..3600), `Jwt:RefreshSessionDays=7` (1..30) açık ayarlardır;
üretici etkinse eksik/geçersiz değer başlangıçta reddedilir.

Migration'ı yalnız app hedefini kontrol ederek uygulayın (bağlantı değerini yazdırmayın):

```powershell
$connection=(Get-Content appsettings.json -Raw|ConvertFrom-Json).ConnectionStrings.UniversityDatabase
$target=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($connection)
if ($target.InitialCatalog -ne 'university_registration_app' -or $target.DataSource -ne '(localdb)\MSSQLLocalDB') { throw 'Beklenmeyen hedef' }
dotnet ef database update --connection $connection
Remove-Variable connection,target
dotnet ef migrations has-pending-model-changes
dotnet run --no-launch-profile --urls https://localhost:7080
```

HTTPS sertifikasına güvenilen istemcide, yukarıdaki cookie login adımından sonra:

```powershell
$csrf=Invoke-RestMethod "$base/accounts/antiforgery" -WebSession $session
$pair=Invoke-RestMethod "$base/accounts/dev-token" -Method Post -WebSession $session `
    -Headers @{'X-CSRF-TOKEN'=$csrf.requestToken}
Invoke-RestMethod "$base/accounts/bearer-me" -Headers @{Authorization="Bearer $($pair.accessToken)"}
# Refresh için WebSession/Authorization yok. Ham token yalnız JSON gövdesinde.
$body=@{refreshToken=$pair.refreshToken}|ConvertTo-Json
try {
    $pair=Invoke-RestMethod "$base/accounts/refresh" -Method Post -ContentType application/json -Body $body
} finally { Remove-Variable body }
Invoke-RestMethod "$base/accounts/bearer-me" -Headers @{Authorization="Bearer $($pair.accessToken)"}
Remove-Variable pair,csrf
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode refresh
```

Token yanıtlarını, değişkenleri veya HTTP gövdelerini paylaşmayın/loglamayın.
Başarılı yanıt `accessToken`, `tokenType:"Bearer"`, `expiresIn`, `refreshToken` içerir
ve `Cache-Control:no-store` kullanır. Refresh cookie/CSRF/access token yetkisine dayanmaz;
yalnız JSON'daki token yetkidir. Browser saklama tasarımı kapsam dışıdır. Cookie başlangıç
üretimi CSRF korumasını sürdürür. Girdi 43 karakterlik kanonik Base64Url/32 rastgele bayt
olmalıdır; Trim yapılmaz. Eksik/bozuk/aşırı uzun girdi400, biçimi geçerli fakat bilinmeyen,
eski, süresi dolmuş, iptal edilmiş veya hesabı uygun olmayan token ortak401; DB hatası500.

R1→R2→R3 aynı satırı değiştirir; R2 kaydedildikten sonra R1 reddedilir. Sabit oturum sonu
uzatılmaz; yeni JWT exp kalan oturum süresini aşmaz, expiresIn tam saniye farkıdır.
JWT doğrulamasının mevcut 30 saniye clock skew toleransı ayrıca geçerlidir. Son bir
saniyeden kısa kalan süre yeni tam saniyelik bilet üretemiyorsa401 dönebilir.

Yalnız güncel özet vardır: token family/geçmiş/reuse tespiti, eşzamanlı tek kazanan,
güvenli otomatik retry yoktur. Commit sonrası yanıt kaybında istemci eski token'da
kalabilir ve yeniden giriş gerekebilir. Cookie logout veya oturum iptali, önceki access
token'ı iptal etmez; bearer hâlâ DB'ye bakmaz. Tüm cihazlardan çıkış/denylist yoktur.

[Öğrenme notu](docs/day28-refresh-sessions.md) · [ADR](docs/adr/0028-refresh-sessions.md).

## Gün 29 — Refresh ailesi ve hesap oturumları

Gün28'in yalnız güncel özet modeli artık `RefreshTokenRecords` geçmişine taşındı.
`RefreshSession` aileyi temsil eder; fiziksel cihaz kimliği/fingerprinting yoktur.
Migration eski güncel token'ı korur; daha önce atılmış token geçmişini geri getirmez.
Eski ve yeni uygulama yazmalarını durdurup Gün28'deki hedef kontrollü app migration
komutunu çalıştırın. Lab'a uygulamayın. Migration yalnız token geçmişi/ilişkileri ekler
ve artık gerekmeyen güncel özet sütununu kaldırır.

```powershell
dotnet ef migrations has-pending-model-changes
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode refresh-family
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
dotnet run --no-launch-profile --urls https://localhost:7080
```

Mevcut JWT anahtar kurulumu, Development opt-in, HTTPS ve cookie login sonrasında:

```powershell
$csrf=Invoke-RestMethod "$base/accounts/antiforgery" -WebSession $session
$a=Invoke-RestMethod "$base/accounts/dev-token" -Method Post -WebSession $session -Headers @{'X-CSRF-TOKEN'=$csrf.requestToken}
$b=Invoke-RestMethod "$base/accounts/dev-token" -Method Post -WebSession $session -Headers @{'X-CSRF-TOKEN'=$csrf.requestToken}
$families=Invoke-RestMethod "$base/accounts/sessions" -WebSession $session
$families | Select-Object sessionId,createdAtUtc,expiresAtUtc,revokedAtUtc
# Listeden iptal etmek istediğiniz kendi aile kimliğini seçin; token değerlerini yazdırmayın.
$id=Read-Host 'İptal edilecek sessionId'
Invoke-WebRequest "$base/accounts/sessions/$id/revoke" -Method Post -WebSession $session -Headers @{'X-CSRF-TOKEN'=$csrf.requestToken} | Select-Object StatusCode
Remove-Variable a,b,csrf,families,id
```

`GET /accounts/sessions` yalnız cookie hesabının ailelerini CreatedAtUtc/SessionId artan
sırada döndürür; alanlar sessionId/createdAtUtc/expiresAtUtc/revokedAtUtc, no-store.
`POST /accounts/sessions/{sessionId}/revoke` cookie+CSRF gerektirir. Kendi ailesinde204;
tekrar204, iptal zamanı değişmez. Olmayan/başka hesabın ailesi aynı404. Eksik/yanlış
CSRF400, yalnız bearer401. Her iki yönetim endpoint'i bugünkü Development üretici
opt-in'iyle birlikte açılır. Refresh sözleşmesi ve anonim JSON yetkisi değişmedi.

R1→R2→R3 geçmişi tutulur. R1 yeniden sunulursa401 ve ilgili aile kalıcı iptal edilir;
R3 artık yenileyemez, diğer aile etkilenmez. İki eşzamanlı aynı-token isteği normal
sona erdiğinde bir200/bir401 ve iptal aile beklenir. İki sekme veya kaybolan refresh
yanıtı da katı reuse'u tetikleyebilir; yeniden giriş gerekebilir. İstemci single-flight
sunucu koordinasyonunun yerine geçmez. Bilinmeyen token başka aileyi iptal etmez.

İptal eski JWT'yi veya cookie'yi silmez. Cookie logout anlamı değişmedi. JWT DB'ye
bakmadığından access token exp ve30s toleransına kadar kabul edilebilir. Ham token/
anahtar loglanmaz; token geçmişi otomatik silinmez. Erken geçmiş temizliği reuse
kapsamını azaltır. OAuth/OIDC, tolerans penceresi, otomatik retry ve tam teslim garantisi yok.

[Öğrenme notu](docs/day29-refresh-families.md) · [ADR](docs/adr/0029-refresh-family-coordination.md).

## Gün 30 — Parola sıfırlama

`POST /accounts/forgot-password` yalnız `{ "email": "student@example.test" }` alır.
Geçerli biçimdeki bilinmeyen, doğrulanmamış ve uygun hesaplar için aynı **202** mesajı
verilir. E-posta Trim edilir; Identity normalizasyonu kullanılır. Yalnız doğrulanmış,
SignInManager tarafından uygun bulunan hesaba yerel talimat hazırlanır. Bu yanıt
hesabın varlığını veya mesaj teslimini kanıtlamaz. Beklenen dosya kutusu arızası da
aynı yanıttır; EventId 3001 yalnız hata türünü kaydeder. DB arızaları gizlenmez.

`POST /accounts/reset-password` gövdesi `userId`, Base64Url taşıma biçimindeki `token`
ve `newPassword` içerir. Başarı **204**; bozuk/eksik girdi, yanlış kullanıcı/purpose,
geçersiz/süresi dolmuş token **400**; parola politikası hatası `newPassword` alanında
400; beklenmeyen DB hatası güvenli500. Cookie/access token gerekmez; hedefi reset
izni belirler. Yeni parola dönüştürülmez; mevcut 12–128 karakter politikası geçerlidir.
Başarı otomatik giriş, cookie veya token çifti üretmez.

Ayrı `UniversityPasswordReset` provider'ı varsayılan **15 dakika** sürer;
`Accounts:PasswordResetMinutes` 1..60 olabilir. E-posta doğrulamasının **2 saat** süresi
değişmez. Yeni reset talebi önceki bağlantıyı iptal etmez; başarılı reset Identity
security stamp'ini değiştirir ve önceki reset token'ları geçersiz olur. Bu davranış
gerçek sağlayıcıyla sınandı; salt süreli olmak tek kullanımlılık sağlamaz.

Yerel geliştirme (mevcut güvenilen HTTPS sertifikası ve DB ayarlarıyla):

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Accounts__LocalMailboxEnabled = 'true'
$env:Accounts__ConfirmationPageUrl = 'https://localhost:7080/confirm-email'
$env:Accounts__PasswordResetPageUrl = 'https://localhost:7080/reset-password'
$env:Accounts__PasswordResetMinutes = '15'
dotnet run --no-launch-profile --urls https://localhost:7080
```

Callback yalnız yapılandırmadan alınır; Host/forwarded Host kullanılmaz. HTTPS
beklenir; yalnız Development yerel HTTP istisnası vardır. URL userinfo/query/fragment
kabul etmez. Callback sayfası uygulanmış değildir: GET hiçbir şeyi değiştirmez.
Yerel kutu `.local/accounts/messages` içindedir, wwwroot/Git dışındadır. Production'da
etkinleştirme reddedilir. Kutunun veya reset callback'in yapılandırılmadığı ortamda
202 korunur, güvenli uyarı yazılır; teslim yapılmış sayılmaz. Gerçek sender yoktur.

Ayrı PowerShell terminalinde; mesajı/token'ı konsola yazmadan:

```powershell
$base = 'https://localhost:7080'
$email = Read-Host 'Test hesabı e-postası'
Invoke-RestMethod "$base/accounts/forgot-password" -Method Post -ContentType application/json -Body (@{email=$email} | ConvertTo-Json) | Out-Null
# Yerel kutudaki size ait yeni JSON dosyasının yolunu girin; içeriği paylaşmayın.
$message = Get-Content -LiteralPath (Read-Host 'Yerel mesaj dosyası yolu') -Raw | ConvertFrom-Json
$link = [uri]$message.link
$query = [System.Web.HttpUtility]::ParseQueryString($link.Query)
$credential = Get-Credential -UserName $email -Message 'Yeni parola'
$body = @{userId=$query['userId']; token=$query['token']; newPassword=$credential.GetNetworkCredential().Password} | ConvertTo-Json
Invoke-WebRequest "$base/accounts/reset-password" -Method Post -ContentType application/json -Body $body | Out-Null
Remove-Variable body,credential,message,link,query
# Ardından Gün 26 antiforgery → login akışında yeni parolayı kullanın.
```

Parola/stamp ve hesabın tüm henüz iptal edilmemiş refresh aileleri tek açık DB
transaction'ında kesinleşir. Reset/refresh/revoke/ilk token üretimi aynı hesap satırında
DB kilidi alır; sıra hesap → aile → token. İlk üretim kilit sonrası cookie stamp'ini
yeniden doğrular. Dosya gönderimi bu kilitlerde yapılmaz. Akademik veriler değişmez.

Refresh hemen reddedilir; cookie ancak stamp doğrulaması veya bilet süresi sonunda
reddedilir. Mevcut stamp aralığı30 dakika, sabit cookie bileti20 dakika; anlık global
çıkış garantisi yoktur. JWT ek DB sorgusu yapmaz ve kendi süresi/toleransı boyunca
kabul edilebilir. Rate limit/timing/enumeration savunması33. gün; ağ kaybı/kalıcı teslim
ve gerçek posta sağlayıcısı kapsam dışıdır. Data Protection anahtarları korunmalıdır.

```powershell
dotnet restore
dotnet build
dotnet ef migrations has-pending-model-changes
powershell -File scripts/verify-course-writes.ps1 -Mode password-reset
powershell -File scripts/verify-course-writes.ps1 -Mode unit
powershell -File scripts/verify-course-writes.ps1 -Mode core
powershell -File scripts/verify-course-writes.ps1 -Mode full
```

Gün30 model değiştirmez; yeni migration gerekmez. Kod:
[endpoint](Endpoints/PasswordResetEndpoints.cs), [transaction](Accounts/PasswordResetOperations.cs),
[provider](Accounts/PasswordResetTokenProvider.cs), [ortak hesap kilidi](Accounts/AccountWriteLock.cs).

## Gün 31 — Identity rolleri ve katalog yönetimi

Roller tek yerde `UniversityRoles`: **Student, Instructor, Advisor, StudentAffairs**.
`AddRoles<IdentityRole>` Identity'nin rol destekli principal factory/store servislerini
bağlar. Public register rol/permission kabul eden bir DTO değildir; ek JSON alanları
üyelik oluşturmaz. E-posta doğrulaması veya Student profili bağlantısı rol kazandırmaz.

Bilinçli HTTP değişikliği: aşağıdaki dört mevcut yazma route'u artık yalnız Identity
application cookie + `ManageAcademicCatalog` (StudentAffairs) + geçerli CSRF ister:

| Route | Anonymous / yalnız bearer | Cookie, gerekli rol yok | StudentAffairs + CSRF |
|---|---|---|---|
| POST /courses | 401 | 403 | Mevcut201/400/409 |
| PUT /courses/{id} | 401 | 403 | Mevcut200/400/404/409 |
| POST /academic-terms | 401 | 403 | Mevcut201/400 |
| POST /course-offerings | 401 | 403 | Mevcut201/400/409 |

Student, Instructor, Advisor ve rolsüz hesaplar yönetimde403 alır. StudentAffairs
yanında ek rol bulunması hakkı kaldırmaz. Yetkili cookie'de CSRF eksik/geçersizse400.
Login HTML/302 yoktur. GET katalog/detay okumaları public kalır. Bu kapsamda başka
katalog yazma route'u yoktur; yeni CRUD eklenmedi.

Açık Development bakım komutu (mevcut yapılandırılmış DB üzerinde, HTTP hostu açmaz):

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet run --no-launch-profile -- roles prepare
$accountId = Read-Host 'Mevcut hesabın Identity Id değeri'
dotnet run --no-launch-profile -- roles add $accountId StudentAffairs
dotnet run --no-launch-profile -- roles remove $accountId StudentAffairs
```

Prepare yalnız eksik dört rolü oluşturur; tekrar üyelik işlemi güvenlidir. Kullanıcı,
parola veya ilk-admin seed'i yoktur. İlgisiz üyelikler silinmez. Production, geçersiz
rol, olmayan hesap ve başarısız IdentityResult sıfır olmayan exit code verir. Komut
bir Production provisioning sistemi değildir; yalnız güvenilen yerel geliştirici
çalıştırmalıdır. Rol kaldırmak mevcut cookie/JWT'leri anında değiştirmez.

Yetkili HTTPS yazma örneği (güvenilen yerel sertifika, önceki HTTPS kurulumu):

```powershell
$base = 'https://localhost:7080'
$credentials = Get-Credential -Message 'Doğrulanmış StudentAffairs test hesabı'
$csrf = Invoke-RestMethod "$base/accounts/antiforgery" -SessionVariable browser
$login = @{email=$credentials.UserName; password=$credentials.GetNetworkCredential().Password} | ConvertTo-Json
Invoke-RestMethod "$base/accounts/login" -Method Post -WebSession $browser -ContentType application/json -Headers @{'X-CSRF-TOKEN'=$csrf.requestToken} -Body $login | Out-Null
Remove-Variable login,credentials
$csrf = Invoke-RestMethod "$base/accounts/antiforgery" -WebSession $browser
Invoke-RestMethod "$base/courses" -Method Post -WebSession $browser -ContentType application/json -Headers @{'X-CSRF-TOKEN'=$csrf.requestToken} -Body (@{code='DEMO31';name='Yetkili örnek';credits=3} | ConvertTo-Json)
```

Cookie rollerini Identity üretir. JWT üreticisi her ilk üretim ve refresh'te güncel
`UserManager.GetRolesAsync` sonucunu `role` claim'lerine dönüştürür; bearer RoleClaimType
aynı addır, MapInboundClaims=false kalır. Eski principal'dan rol kopyalanmaz.
Rol ekleme/kaldırma security stamp'i otomatik değiştirmedi (gerçek test). Mevcut
cookie kaldırılmış rolü19. dakikada hâlâ taşıdı; sabit20 dakikalık bilet21. dakikada401
oldu. Stamp doğrulama aralığı30 dakika kaldığı için bu akışta önce bilet sona erer.
Yeni login güncel rolü alır. Geçerli stamp doğrulaması principal'ı yeniden üretir;
ancak bunu her istekte rol sorgusu/anlık iptal olarak sunmuyoruz. JWT kendi süre/skew
sınırına kadar eski rolü taşır; yeni mint/refresh güncel üyeliği alır.

**32. güne kalanlar:** Öğrenci oluşturma/listeleme/detay, enrollment oluşturma/batch/
submit, approve/reject, eligibility ve dönem özeti için sahiplik/danışman ataması ve
IDOR kontrolleri tamamlanmış değildir. Instructor–açılış ilişkisi yoktur. Bugün bu
route'lara eksik modelle rol hakları dağıtılmadı; güvenli self-servis ilan edilmez.
Diğer üç rol, yalnız test derlemesinde sabit `allowed:true` döndüren policy route'larıyla
sınanır; Production/normal uygulama bu route'ları içermez.

```powershell
dotnet restore
dotnet build
dotnet ef migrations has-pending-model-changes
powershell -File scripts/verify-course-writes.ps1 -Mode roles
powershell -File scripts/verify-course-writes.ps1 -Mode unit
powershell -File scripts/verify-course-writes.ps1 -Mode core
powershell -File scripts/verify-course-writes.ps1 -Mode full
```

Rol tabloları zaten mevcuttu; Gün31 migration üretmez. Kod:
[roller/policy filtresi/bakım](Accounts/UniversityRoles.cs), [Identity kaydı](Accounts/AccountSetup.cs),
[JWT üyelikleri](Accounts/TokenSessions.cs), [test matrisi](scripts/RolesChecks.cs).

## Gün 32: öğrenci sahipliği ve atanmış danışman

Kişisel akademik route’lar artık public değildir. Aşağıdaki politikalar yalnız
Identity application cookie’sini kabul eder; bearer tek başına yeterli değildir.
Hesap kimliği cookie’den, sahiplik/atama ise her istekte DB projection’ından alınır.
Student.Id akademik kimliktir; ApplicationUser.Id yerine kullanılamaz.

| Route | İzin |
|---|---|
| `POST /students` | StudentAffairs + CSRF; profil provisioning, otomatik hesap bağlantısı yok |
| `GET /students/{id}` | Student + kendi UserId bağlantısı VEYA Advisor + güncel atama |
| `GET /students/{id}/enrollments` | Aynı okuma kararı |
| `GET /enrollments/{id}` | Gerçek enrollment.StudentId üzerinden aynı okuma kararı |
| `GET /students/{studentId}/terms/{termId}/registration-summary` | Aynı okuma kararı |
| `POST /enrollments` | Student + gövdedeki hedef Student’ın sahipliği + CSRF |
| `POST /students/{id}/enrollments/batch` | Student + route hedefinin sahipliği + CSRF |
| `POST /students/{id}/enrollments/eligibility` | Student + route hedefinin sahipliği + CSRF; yazma yok |
| `POST /enrollments/{id}/submit` | Student + gerçek enrollment öğrencisinin sahipliği + CSRF |
| `POST /enrollments/{id}/approve` ve `/reject` | Advisor + gerçek enrollment öğrencisinin güncel ataması + CSRF |

StudentAffairs/Instructor için örtülü bypass yoktur. Çok rollü hesap da gerekli
ilişkiyi sağlamalıdır. Danışmanın okuma izni öğrenci adına oluşturma veya submit
hakkı vermez. Katalog/ders/açılış okumaları public kalır; katalog yazmaları Gün 31
StudentAffairs politikasını korur. Kişisel akademik route envanteri yukarıdadır;
account/session/refresh/reset güvenlik sözleşmeleri değiştirilmedi.

Kimlik yoksa **401**, genel rol eksikse **403**, uygun rol fakat kaynak yok/ilişki
uyuşmuyorsa aynı **404** döner. Kimlik doğrulama/yetkilendirme middleware’i, kaynak
filtresi, CSRF filtresi, sonra mevcut uygulama işlemi çalışır. JSON bağlama hataları
400 olabilir. Yetkili kaynak için akademik 400/failures ve mükerrer/state 409 korunur.
Nonpositive/olmayan kişisel kaynak kimlikleri artık kaynak 404’üdür; var olan yetkili
öğrencide geçersiz dönem/akademik girdiler mevcut alan 400’ünü korur.

`Student.AdvisorUserId` nullable, nonunique, `AspNetUsers.Id` FK ve NO ACTION’dır.
Bir danışman çok öğrenciye atanabilir; tek mevcut atama tutulur, tarihçe/dönem yoktur.
FK hesap varlığını korur, Advisor rolünü kanıtlamaz. Atama komutu rolü ayrıca denetler.
`UserId` filtreli unique/NO ACTION ilişkisi değişmedi. HTTP öğrenci request DTO’su
UserId veya AdvisorUserId bağlamaz.

### Kontrollü Development hazırlığı

Mevcut hesapları kullanın; komutlar hesap/parola oluşturmaz, HTTP hostu açmaz.
`$accountId`, `$advisorAccountId` ve `$studentId` açıkça seçilmiş mevcut kimliklerdir.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --no-launch-profile -- roles prepare
dotnet run --no-launch-profile -- roles add $accountId Student
dotnet run --no-launch-profile -- roles add $advisorAccountId Advisor
dotnet run --no-launch-profile -- students link $studentId $accountId
dotnet run --no-launch-profile -- students advisor $studentId $advisorAccountId
# Atamayı kaldırmak için:
dotnet run --no-launch-profile -- students advisor $studentId none
```

Aynı bağlantı/atama tekrarı güvenlidir. Başka hesaba relink reddedilir; hesabın başka
profile bağlılığı denetlenir. Rol/hesap yoksa exit1; Production’da komutlar reddedilir.
Atama değişikliği yeni kaynak sorgusunda görülür; cookie’deki rolün güncellik sınırı
Gün 31’deki gibidir. Mevcut cookie yeni rol için yeniden login gerektirebilir.

Gün 26 HTTPS/cookie login akışından sonra **yeniden** antiforgery token alın:

```powershell
# $base HTTPS adresi; $session login olmuş WebRequestSession; değerleri paylaşmayın.
$csrf = Invoke-RestMethod "$base/accounts/antiforgery" -WebSession $session
$headers = @{ 'X-CSRF-TOKEN' = $csrf.requestToken }
Invoke-RestMethod "$base/students/$studentId/enrollments" -WebSession $session
$body = @{ studentId=$studentId; courseOfferingId=$offeringId } | ConvertTo-Json
Invoke-RestMethod "$base/enrollments" -Method Post -WebSession $session `
  -Headers $headers -ContentType 'application/json' -Body $body
```

Migration yalnız uygulama DB’sine uygulanır; bağlantı hedefini kontrol edin,
lab DB’sini kullanmayın. Startup otomatik migration yoktur.

```powershell
dotnet restore
dotnet build --no-restore
dotnet ef database update 20261105134931_AddStudentAdvisor
dotnet ef migrations has-pending-model-changes
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode resource-access
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
```

Kaynak kontrolü ile yazma arasında atama değişebilir (**TOCTOU**). Kontrol transaction’ı
akademik yazmayı kapsamaz; kesin eşzamanlı atama garantisi eklenmedi. HTTP dışından
EnrollmentOperations çağrısı güvenilen iç kullanımdır; işlem sınıfı tek başına erişim
sınırı değildir. Öğretim görevlisi–açılış ilişkisi ve geniş provisioning/atama paneli
bu kapsamda yoktur. Hesap ve rol sahibi olmak bütün öğrenci verilerine erişim vermez.


## Gün 33 — Login ve parola sıfırlama talebi sınırları

`Accounts/AuthenticationAbuse.cs` yerleşik fixed-window limiter ve bir kez hazırlanmış
Identity dummy hash kullanır. Login gerçek hesapta yine `SignInManager` üzerinden
çalışır; olmayan hesapta yapılandırılmış `IPasswordHasher<ApplicationUser>` ile bir
kontrol yapılır ve sonuç ne olursa olsun ortak 401 döner. Parola değiştirilmez.
Kilitli/doğrulanmamış hesap yollarında hash kontrolü atlanabilir: constant-time veya
tam enumeration koruması yoktur. Başarılı giriş, DB ve cookie üretimi de farklı maliyettedir.

| Ayar | Varsayılan | Geçerli aralık |
|---|---:|---:|
| `AuthenticationLimits:Login:PermitLimit` | 10 | 1–10000 |
| `AuthenticationLimits:Login:WindowSeconds` | 60 | 1–86400 |
| `AuthenticationLimits:ForgotPassword:PermitLimit` | 3 | 1–10000 |
| `AuthenticationLimits:ForgotPassword:WindowSeconds` | 900 | 1–86400 |

Dört ayar zorunludur; eksik/boş/uygunsuz değer başlangıcı durdurur. `appsettings.json`
varsayılanları sağlar; environment override için `:` yerine `__` kullanılır.
Her politika ayrı IP kotasıdır; e-posta değiştirmek kotayı yenilemez. Kuyruk yoktur.
Routing → authentication → rate limiter → authorization → endpoint/CSRF sırası
korunur. Login/forgot istekleri yapısal veya CSRF açısından hatalı olsa da kotayı
kullanabilir. Kota dolmuşsa endpoint, parola kontrolü ve bildirim çalışmadan 429 döner.
Authentication middleware'in mevcut cookie doğrulaması gerektiğinde ayrıca DB işi yapabilir.

- Yapısal hata veya login CSRF reddi: mevcut 400.
- Olmayan/yanlış parolalı/kilitli/doğrulanmamış/girişe uygun olmayan hesap: aynı 401.
- Kota aşımı: `authentication_rate_limited` kodlu küçük 429 Problem Details,
  `Cache-Control: no-store`. `Retry-After` yalnız limiter metadata sağlarsa yukarı
  yuvarlanmış saniyedir; sonraki isteğin kesin kabul sözü değildir.
- Beklenmeyen DB hatası: mevcut güvenli 500; 401'e çevrilmez.
- Forgot-password: uygun/bilinmeyen/doğrulanmamış hesap için önceki ortak 202;
  limit aşımında 429. Gönderim arızasında teslim edilmiş gibi raporlanmaz.

Identity lockout ayarları değiştirilmedi: 5 başarısız deneme, 5 dakika,
`AllowedForNewUsers=true`, `lockoutOnFailure=true`. Mevcut hesapların ayrı
`LockoutEnabled` alanı topluca değiştirilmez. Sayaç ve bitişi Identity yönetir;
başarılı giriş sayacı sıfırlar. Lockout cookie/JWT/refresh kayıtlarını kendiliğinden
iptal etmez. Başkasının hesabını kilitleme ve dağıtık password spraying sınırları sürer.

Partition yalnız `Connection.RemoteIpAddress` kullanır; IPv4-mapped IPv6 aynı IPv4
anahtarına çevrilir, IP yoksa ortak sabit fallback vardır. Forwarded Headers kurulmadı;
`X-Forwarded-For` doğrudan güven kaynağı değildir. Reverse proxy kurulursa güvenilen
proxy/ağlar ve middleware sırası ayrıca tasarlanmalıdır. Ortak NAT/IP kullanıcıları
kotayı paylaşır. Sayaçlar süreç belleğindedir; restart sıfırlayabilir, çoklu instance
ortak kota sağlamaz. Bu DDoS veya bütün timing saldırıları için çözüm değildir.

3301/3302 event kimlikleriyle yalnız Debug düzeyinde olay türü ve TraceId kaydedilir;
her reddi ayrıntılı yüksek maliyetli loga dönüştürmeyiz. Auth gövdeleri, başlıkları,
e-posta/IP, parola/hash, dummy hash ve token loglanmaz. Mevcut güvenli hata filtresi korunur.

```powershell
dotnet restore
dotnet build --no-restore
dotnet run --no-launch-profile --urls https://localhost:7080
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode auth-abuse
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
dotnet ef migrations has-pending-model-changes --no-build
```

Önceki HTTPS antiforgery → login ve forgot-password örnekleri aynen kullanılabilir.
İzole regresyon hostları 10000/60s kota kullanır; koruma kapatılmaz. `auth-abuse`
ayrı hostta 10/5s ve 3/5s ile gerçek 429/pencere yenilenmesini sınar. IP ayrımı test
hostunun Connection özelliğinde kontrollü yapılır; public bypass header yoktur.
Lockout sonrası kabul için yalnız test hesabının bitişi UserManager ile geçmişe
alınır; beş dakika beklenmiş gibi sunulmaz. Şema değişikliği/migration yoktur.
