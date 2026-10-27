# Üniversite Ders Kayıt ve Kontenjan Yönetim Sistemi

106 öğrenme birimlik programın ilk iki gün uygulaması: HTTP istek akışını görmek
ve ders, akademik dönem, dönemlik ders açılışı/şube ayrımını temsil etmek.
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

Veriler sabit ve bellektedir; henüz veritabanı veya kalıcılık yoktur.

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
