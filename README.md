# Üniversite Ders Kayıt ve Kontenjan Yönetim Sistemi

106 öğrenme birimlik programın ilk altı gün uygulaması: HTTP istek akışını görmek,
ders/dönem/açılış ayrımını temsil etmek ve ayrı SQL laboratuvarında ilişkisel
şema ile kısıtları öğrenmek; EF Core ile dersleri okumak, doğrulayarak oluşturmak ve düzenlemek.
Proje tek bir ASP.NET Core Minimal API'dir. Güncel durumda bütün listeleme
endpoint'leri DB kullanır; dönem ve açılış oluşturma da desteklenir.

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
