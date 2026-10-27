# Üniversite Ders Kayıt ve Kontenjan Yönetim Sistemi

106 öğrenme birimlik programın 1. gün uygulaması: HTTP isteğinin bir endpoint
tarafından karşılanmasını ve örnek ders listesinin JSON olarak döndürülmesini görmek.
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
Henüz ayrı iş kuralı veya veri erişim katmanı yoktur; örnek liste anonim
nesnelerle endpoint içinde tanımlanır.

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
