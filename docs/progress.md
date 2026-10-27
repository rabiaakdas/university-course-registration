# Öğrenme ilerlemesi

## Gün 1

**Konu:** Backend istek akışı ve sorumluluklar.

**Öğrenilen kavramlar:** Endpoint, uygulama işlemi, iş kuralı, veri erişimi,
dependency, coupling/cohesion, interface, composition ve DI.

**Uygulanan kısım:** Basit HTTP endpoint'i ve sabit JSON ders listesi.
Bütün kavramlar için yapay sınıflar oluşturulmadı. `MapGet` isteği karşılar,
`Results.Ok` başarılı JSON yanıtını oluşturur. Ayrı iş kuralı, veri erişimi,
servis veya interface katmanı yoktur; özel DI kaydı yapılmadı.

**Somut değişiklik:** .NET 10 üzerinde tek `UniversityRegistration.csproj` ve
`Program.cs` oluşturuldu. `GET /courses`, CSE201 ve CSE203 derslerini yalnızca
`id`, `code`, `name` alanlarıyla döndürür. SDK seçimi `global.json` ile kaydedildi.
Şablon örnekleri kaldırıldı; `.gitignore`, README ve mimari karar kaydı eklendi.

**Gerçekte yapılan doğrulamalar:**

- SDK: `dotnet --version` → `10.0.401`.
- `dotnet restore .\UniversityRegistration.csproj` → başarılı.
- `dotnet build .\UniversityRegistration.csproj --no-restore` → başarılı,
  0 uyarı ve 0 hata.
- Derlenen DLL `http://127.0.0.1:5080` adresinde geçici süreçle başlatıldı.
- `Invoke-WebRequest` ile gerçek, kimlik doğrulamasız `GET /courses` isteği
  gönderildi; 200 OK ve `application/json; charset=utf-8` programatik doğrulandı.
- `JsonDocument` ile geçerli JSON dizisi, tam iki ders, yalnızca üç beklenen
  alan, alan tipleri, tam değerler ve çözümlenen Türkçe karakterler doğrulandı.
  Bütün kontroller geçti.
- Geçici API süreci `finally` bloğunda kapatıldı; kapanma doğrulandı.

**Açık noktalar:** 1. gün API kabul ölçütleri karşılandı. Veriler sabit/bellekte;
DB ve kalıcılık yoktur. GitHub CLI PATH'te veya kontrol edilen standart kurulum
konumlarında bulunmadı; oturum doğrulanamadı. Uzak repo oluşturma ve push için
GitHub CLI ile yetkili GitHub oturumu gerekir. Uzak repo oluşturulmadı.

**Sıradaki gün:** Course, AcademicTerm ve CourseOffering ayrımı.
Bu günün uygulamasına bu modeller eklenmedi.

**Commit/push takibi:** Bu kaydı içeren commit'in kimliği Git geçmişinden
`git log -1 --format=%H -- docs/progress.md` ile bulunur.
Hedef commit mesajı: `feat: initialize course catalog API`.
Push sonucu son raporda verilir; burada gerçekleşmemiş bir push başarılı sayılmaz.
