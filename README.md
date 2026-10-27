# Üniversite Ders Kayıt ve Kontenjan Yönetim Sistemi

106 öğrenme birimlik programın ilk üç gün uygulaması: HTTP istek akışını görmek,
ders/dönem/açılış ayrımını temsil etmek ve ayrı SQL laboratuvarında ilişkisel
şema ile kısıtları öğrenmek.
Proje tek bir ASP.NET Core Minimal API'dir.

## Ön koşullar ve çalıştırma

.NET 10 LTS kullanılır; doğrulanan SDK sürümü **10.0.401**, hedef framework
`net10.0`'dır. `global.json` aynı özellik bandındaki kararlı düzeltme sürümlerine
izin verir. Harici NuGet paketi yoktur.
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

API verileri sabit ve bellektedir; API'nin veritabanı bağlantısı veya kalıcılığı
yoktur. 3. gün SQL laboratuvarı aşağıda ayrı anlatılır.

## İstek akışının kod karşılığı

`Program.cs` içindeki `MapGet`, `GET /courses` isteğini karşılar.
Endpoint'in uygulama işlemi sabit ders listesini döndürmektir.
`Results.Ok`, 200 durum kodlu başarılı JSON yanıtını oluşturur.
Henüz ayrı iş kuralı veya veri erişim katmanı yoktur. Bellekteki `Course`
nesnelerinden anonim yanıt nesnelerine açık alan seçimi yapılır; `Credits`
modelde bulunur ama `/courses` yanıtına eklenmez.

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

**API hâlâ bellekteki verileri kullanır; DB bağlantısı, EF Core, provider paketi
ve migration eklenmedi.** API komutları yukarıdaki gibidir.

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
