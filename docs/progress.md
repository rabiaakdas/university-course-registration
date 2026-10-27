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
GitHub erişimi gerekir. 1. gün uygulama oturumunda uzak repo oluşturulmadı.

**Sıradaki gün:** Course, AcademicTerm ve CourseOffering ayrımı.
Bu günün uygulamasına bu modeller eklenmedi.

**Commit/push takibi:** Bu kaydı içeren commit'in kimliği Git geçmişinden
`git log -1 --format=%H -- docs/progress.md` ile bulunur.
Hedef commit mesajı: `feat: initialize course catalog API`.
Push sonucu son raporda verilir; burada gerçekleşmemiş bir push başarılı sayılmaz.

### 2. gün başında Git durumu güncellemesi

Kullanıcı 1. gün push işlemini tamamladığını bildirdi. Yerel `main` ve
`origin/main`, `46560620d235626c791f6480221ea82c9f4348ca` commit'ini gösteriyordu;
çalışma ağacı temizdi. `origin`,
`https://github.com/rabiaakdas/university-course-registration.git` adresidir.
`git ls-remote origin refs/heads/main` aynı commit'i doğrudan uzak depodan
doğruladı. Önceki GitHub erişim engeli bu kontrol sırasında aşılmıştır.

## Gün 2

**Konu:** Course, AcademicTerm ve CourseOffering ayrımı.

**Öğrenilen kavramlar:** Kimlik, kimlik üzerinden ilişkilendirme, ders/dönem/şube
ayrımı, açılışa ait kapasite, `DateOnly`, `get/init`, model ve API yanıtı ayrımı,
bellekte LINQ ile alan seçimi ve eşleştirme.

`init` sonradan atamayı sınırlar; geçerli değer garantisi sağlamaz. C# kimlik
alanları tek başına ilişki bütünlüğünü sağlamaz. Öğrenci ileride açılışa kaydolur;
bugün öğrenci veya kayıt modeli eklenmedi.

**Somut değişiklik:** `Models` altında üç küçük model eklendi. Dersler `Course`
nesnelerine dönüştürüldü; örnek krediler 4 ve 3 oldu. `/courses` anonim yanıt
nesnelerine açık alan seçimiyle eski üç alanını korur. İki örnek dönem ve üç
açılış eklendi: aynı dersin güz A/B şubeleri 40/30 kapasitelidir; bahar A şubesi
35 kapasitelidir. `/academic-terms` ve `/course-offerings` eklendi. Açılışın
ders/dönem adları `Single` ile kimliklerden bulunur; liste sırasına dayanılmaz,
adlar açılış modelinde tutulmaz. Bunlar bellekte çalışan LINQ işlemleridir.

**Gerçek doğrulama sonuçları:**

- SDK 10.0.401 ile restore başarılı; build başarılı, 0 uyarı ve 0 hata.
- `scripts/verify-http.ps1` çalıştırıldı; derlenen API'yi geçici süreçte başlattı.
- Üç endpoint gerçek HTTP üzerinden kimlik doğrulama bilgisi olmadan çağrıldı:
  hepsi 200 OK ve `application/json; charset=utf-8` döndürdü.
- JSON dizileri ve tam alan kümeleri doğrulandı. `/courses` aynı iki dersin
  id/code/name değerlerini korudu; Credits yanıta sızmadı.
- Tarihler `DateOnly.ParseExact` ile geçerli takvim tarihleri olarak çözümlendi;
  beklenen adlar, tarihler ve tarih sırası doğrulandı.
- Açılışların mevcut ders/dönem kimliklerine bağlandığı ve yanıt adlarının doğru
  eşleştiği doğrulandı. Ayrı açılış kimlikleri, A/B şubeleri, 40/30 kapasiteleri,
  aynı dersin ikinci dönemde açılması ve şube benzersizliği kontrol edildi.
- Bütün kontroller geçti; geçici API süreci `finally` ile kapatıldı.

**Açık noktalar:** Veriler hâlâ sabit/bellekte. DB, kalıcılık, kapsamlı iş kuralı
doğrulaması, kontenjan tüketimi ve concurrency çözümü yok; bugünün kapsamı dışında.

**Sıradaki gün:** SQL tabloları, PK/FK, ilişkiler, veri türleri, NULL, kısıtlar
ve normalizasyon.

**Commit/push takibi:** Bu 2. gün kaydını ekleyen commit kimliği
`git log --format=%H --grep="^feat: model courses terms and offerings$"` ile
Git geçmişinden bulunabilir. Hedef `origin/main`; gerçekleşen push sonucu son
raporda verilir.

## Gün 3

**Konu:** SQL tabloları, PK/FK, bire çok ilişkiler, veri türleri, NULL, kısıtlar
ve normalizasyon.

**Başlangıç:** Çalışma ağacı temiz; main ve uzak main doğrudan GitHub sorgusunda
`6c48e9391fcee2477f31950efc269d2caebc9b38` commit'indeydi. Önceki günler korundu.
Uygulanabilir AGENTS.md bulunmadı. API, modeller ve HTTP betiği değiştirilmedi.

**Somut değişiklik:** `sql/` altında laboratuvar oluşturma/koruma, üç tabloluk
şema, mevcut kimlik/değerlerle seed, tekrar çalıştırılabilir kısıt deneyleri,
JOIN ve isteğe bağlı dar kapsamlı tablo temizleme dosyaları eklendi. README,
öğrenme notu ve ADR 0003 yazıldı; DB dosyaları/dump için ignore kuralları eklendi.

**Seçim:** PostgreSQL istemcisi 18.6 bulundu; localhost:5432 yanıt vermedi.
SQL Server LocalDB `(localdb)\MSSQLLocalDB` 17.0.4025.3 erişilebildi. Yeni
`university_registration_lab` oluşturuldu; öncesinde bu ad yoktu. Collation
`SQL_Latin1_General_CP1_CI_AS`. Bağlantı Windows kimlik doğrulamasıyla yapıldı.
Mevcut kullanıcı veritabanlarına yazılmadı.

**Gerçek kontroller:**

- Şema ve seed gerçek LocalDB'de başarıyla çalıştı: 2 ders, 2 dönem, 3 açılış.
- `03-verify.sql` iki kez çalıştı; her defasında 23 numaralı kontrol ve son içerik
  koruma kontrolü geçti. İstenen 20 davranışın tamamı kapsandı: yinelenen PK/kod/
  birleşim, geçersiz FK, farklı şube/dönem kabulü, sıfır/negatif/NULL/eksik kapasite,
  sıfır/negatif kredi, NULL ad, ters tarih, bağlı üst kayıtların silinememesi ve
  korunması, açılışsız ders, ad değişikliğinin JOIN'e yansıması ve Türkçe değerler.
- Ek olarak mevcut CI collation ve üst kimlik güncellemelerinde NO ACTION sınandı.
  Hatalar 2627/547/515 ve ilgili kısıt/sütun adıyla değerlendirildi.
- Geçici deneylerin tümü rollback edildi; temel veriler korundu. JOIN üç beklenen
  satırı gösterdi. İsteğe bağlı temizlik betiği çalıştırılmadı.
- SDK 10.0.401 ile restore ve build başarılı: 0 uyarı, 0 hata.
- Mevcut HTTP betiği üç endpoint'te 200/application-json, alanlar ve içerik
  regresyonlarını doğruladı. Geçici API süreci kapatıldı.

**Açık noktalar:** Bugünün doğrulamasında engel yok. API hâlâ bellekte; DB'ye
bağlı değil. PK otomatik kimlik üretmez; değer üretimi sonraki gün değerlendirilecek.
NOT NULL boş metni engellemez; bugün ilave boşluk doğrulaması yapılmadı.
Laboratuvar temel seed ile yerinde bırakıldı. PostgreSQL üzerinde test yapılmadı.

**Sıradaki gün:** EF Core, DbContext yaşam süresi/thread-safety, mapping ve migrations.

**Commit/push takibi:** Bu kaydı ekleyen commit Git geçmişinde
`git log --format=%H --grep="^feat: add relational schema and constraint checks$"`
ile bulunabilir. Hedef origin/main; gerçekleşen push sonucu son raporda verilir.
