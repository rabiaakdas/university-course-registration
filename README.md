# Üniversite Ders Kayıt ve Kontenjan Yönetim Sistemi

106 öğrenme birimlik programın ilk on üç gün uygulaması: HTTP istek akışını görmek,
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
