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

## Gün 4

**Konu/kavramlar:** EF Core, DbSet/IQueryable, scoped DbContext yaşam süresi ve
thread-safety, sıralı await, Fluent API mapping, migration/model snapshot,
ValueGeneratedNever, unique index/constraint ayrımı, SQL seed ve okuma projeksiyonu.

**Başlangıç:** Yerel/uzak main `847f1b47a299fd44d12681d752fdc3160c9269bb`;
çalışma ağacı temizdi. Laboratuvarın gerçek tablo, sütun, kısıt ve NO ACTION
metadata'sı okundu. Uygulama DB adı mevcut değildi. SDK 10.0.401 ve LocalDB
17.0.4025.3 doğrulandı; uygulanabilir AGENTS.md bulunmadı.

**Somut değişiklikler:** UniversityDbContext, üç DbSet ve OnModelCreating;
AddDbContext scoped kayıt; /courses için Id sıralı, id/code/name projeksiyonlu
ToListAsync(CancellationToken). Diğer endpoint'lerin bellek kaynağı açık ayrıldı.
EF SQL Server/Design ve repo içi dotnet-ef 10.0.14 eklendi. Gerçek NU1902/NU1903
uyarısını gidermek için dolaylı Pkcs bağımlılığı 10.0.14 ile sabitlendi.
Parolasız yerel uygulama ayarı; initial migration, kontrollü SQL seed ve şema
kontrolü eklendi. 3. gün kısıt deneyleri kopyalanmadan ortak SQL'e taşındı.
HTTP betiği DB kaynağı kanıtı, restart ve eşzamanlı isteklerle genişletildi.

**Gerçek doğrulamalar:**

- Up/Down, designer/snapshot ve ileri SQL uygulama öncesi incelendi. Yalnızca
  beklenen üç model tablosu, index/kısıtlar ve migration geçmişi vardı.
- `20261027110911_InitialUniversitySchema` ayrı university_registration_app
  DB'sine uygulandı. İkinci database update: bekleyen migration yok.
- has-pending-model-changes: model/snapshot farkı yok. Geçmiş kaydı EF tarafından
  10.0.14 ile yazıldı. EnsureCreated/EnsureDeleted kullanılmadı, Down uygulanmadı.
- Gerçek DB sütun türü/uzunluğu, NOT NULL, IDENTITY olmaması, default olmaması,
  PK, iki unique index, CHECK, NO ACTION ve migration geçmişi kontrolü geçti.
- SQL seed iki kez çalıştı; ikinci eklemelerde 0 satır, toplam 2 ders/2 dönem/3
  açılış. Ortak 23 numaralı davranış kontrolü ve son rollback/içerik kontrolü geçti.
- Restore/build başarılı: 0 hata, 0 uyarı. Yerel tool restore başarılı.
- Boş bağlantı ortam değişkeniyle başlatma deneyi açık yapılandırma hatasıyla
  reddedildi; belleğe fallback yapılmadı.
- Üç endpoint gerçek HTTP'de 200/application-json ve beklenen içerik döndürdü;
  /courses tam iki ders, Id sırası ve yalnızca id/code/name alanlarıyla doğrulandı.
- Ders 1'in beklenen adı kontrol edilip geçici ad SQL'de commit edildi; yeni ad
  HTTP'de görüldü. finally içinde eski değer geri yüklendi ve HTTP'de doğrulandı.
- API yeniden başlatıldı; DB okuması ve 8 eşzamanlı GET isteği geçti. Tüm geçici
  API süreçleri kapatıldı. Bu bir kontenjan/concurrency garantisi değildir.
- Son salt okunur kontrolde uygulama adı geri dönmüş, laboratuvar 2/2/3 kayıtla
  korunmuştu. Laboratuvara migration/seed veya deney yazması yapılmadı.

**Açık noktalar/sınırlar:** Bugünün doğrulamasında engel yok. Academic terms ve
course offerings bellekten çalışır; DB ders adı değişikliği açılış yanıtına henüz
yansımaz. DB erişimi için LocalDB sahibi Windows hesabı gerekir. Yeni CRUD,
SaveChanges yazma akışı veya kapsamlı validation eklenmedi.

**Sıradaki gün:** Girdi doğrulama, iş kuralı, DB kısıtı ve HTTP hata ayrımı;
ders oluşturma/düzenleme.

**Commit/push:** Bu kaydı içeren commit kimliği Git geçmişinde
`git log --format=%H --grep="^feat: persist course catalog with EF Core$"`
ile bulunur. Hedef origin/main; push sonucu gerçekleştiğinde son raporda verilir.

## Gün 5

**Konu/kavramlar:** Binding, girdi doğrulama, iş kuralı ve DB kısıtı ayrımı;
istek/entity/yanıt sözleşmeleri, Add/SaveChanges, izlenen nesnenin düzenlenmesi,
400/404/409/500 Problem Details, sequence ile kimlik üretimi ve yarış koşulları.

**Başlangıç:** Yerel/uzak main 5803ef5948597aa73086cb54f4d53576230c650c,
temiz çalışma ağacı. SDK 10.0.401, EF paketleri ve yerel araç 10.0.14 korundu.
LocalDB 17.0.4025.3; gerçek UX_Courses_Code ve SQL_Latin1_General_CP1_CI_AS
incelendi. Collation değiştirilmedi; uygulanabilir AGENTS.md bulunmadı.

**Somut değişiklik:** POST /courses, GET /courses/{id:int}, PUT /courses/{id:int};
küçük istek kayıtları, ortak normalizasyon/doğrulama, endpoint eşleme sınıfı.
Liste yanıtı üç alanla kaldı; detay/yazma yanıtı credits içerir. PUT kendi kaydını
çakışmadan dışlar, kontroller geçince izlenen nesneyi değiştirir. Dar kapsamlı
SQL 2601 + dbo.Courses/UX_Courses_Code tanıma ve merkezi güvenli hata yanıtı eklendi.
GenerateCourseIds migration'ı CourseIds sequence/default ve üretilen EF kimliği
getirdi; diğer model kimlikleri ve ilk migration değiştirilmedi.

**Gerçek doğrulamalar:**

- Restore/build başarılı, 0 hata ve 0 uyarı. Son integration çalışması güncel kodu
  yeniden derledi. Paket yükseltmesi yapılmadı.
- Up/Down, snapshot ve ileri SQL incelendi. Migration yalnızca uygulama DB'sine
  uygulandı; tekrar update bekleyen migration bulmadı, model/snapshot farkı yok.
  Mevcut kayıtlar korundu. Down incelendi ancak çalıştırılmadı.
- Uygulama DB metadata kontrolü ve iki seed çalıştırması geçti; seed kayıt
  çoğaltmadı. Lab'a migration veya deney yazması yapılmadı.
- Ayrı, her çalıştırmada yeni ve işaretli test DB'sinde eski şemaya Id=500 eklendi;
  migration kaydı korudu ve yeni kimlik 500'den büyük üretildi. Şema, iki seed
  çalıştırması ve mevcut ortak 23 SQL kısıt deneyi geçti.
- Üç eski GET sözleşmesi hem uygulama DB'sinde hem ek ders bulunan test DB'sinde
  geçti. Liste yalnızca id/code/name, sıralı; ek kayıtlar artık hata sayılmaz.
- Gerçek HTTP POST 201/Location/DB satırı/detay ve listede görünme; restart sonrası
  kalıcılık; PUT 200/aynı Id/sabit satır sayısı ve kendi koduyla düzenleme geçti.
- Çakışan POST/PUT 409 ve veri korunması, normalizasyon, null/boş/boşluk/uzun
  alanlar ve pozitif olmayan kredi için toplu 400, bozuk JSON ve string Credits
  için 400, olmayan kayıtlar için 404 geçti. Geçersiz POST'ta DB okuması yok.
- Sekiz eşzamanlı farklı POST ayrı kimlikler aldı; aynı normalize kodda bir 201
  ve yedi 409, tek kayıt oluştu. Ayrıca test interceptor'ı ön kontrolden sonra
  gerçek rakip kayıt ekleyerek SaveChanges hata sınıflandırma dalını doğruladı.
- Gerçek PK 2627 ve başka unique index 2601 hataları kod çakışması sayılmadı.
  Test DB'sine geçici CHECK ile oluşturulan beklenmeyen hata HTTP'de hassas
  ayrıntısız 500 döndü; CHECK kaldırıldı.
- Finally test kayıtlarını temizledi ve geçici API süreçlerini kapattı. Ayrı
  test DB'leri seed ile tutuldu; sequence boşlukları kapatılmadı. Son tam çalışma:
  university_registration_tests_day5_35d283ec87ad43f681bcf8aca820b5c2.

**Açık noktalar/sınırlar:** Bugünkü kontrollerde engel yok. SQL sağlayıcısı index
adını ayrı alan sunmadığından dar hata tanıma mesajdaki tırnaklı adların sırasına
bağımlıdır; bilinmeyen biçim 409 yapılmaz. Ön kontrol rezervasyon değildir.
Eşzamanlı düzenlemede lost update henüz çözülmez. Diğer iki liste bellekten çalışır.
Migration/seed sırasında yazıcılar durdurulmalıdır. Test DB'leri otomatik silinmez.

**Sıradaki gün:** İlişkili veri yazma, change tracking ve SaveChanges; dönem
oluşturma ve ders açılışı.

**Commit/push:** Bu kaydı içeren kimlik Git geçmişinde
`git log --format=%H --grep="^feat: add validated course creation and updates$"`
ile bulunur. Push sonucu gerçekleşmeden başarılı kabul edilmez; son raporda verilir.

## Gün 6

**Konu/kavramlar:** İlişkili veri yazma, FK ile mevcut kayda bağlanma, Add ve
SaveChanges sınırı, Detached/Added/Unchanged/Modified/Deleted, DetectChanges,
projection ve entity izleme ayrımı, tek/ayrı SaveChanges ve başarısız tracker.

**Başlangıç:** Yerel/uzak main 41e892f7804f224ee05c5ca46ff36308b0f1c0dc;
çalışma ağacı temizdi. Uygulanabilir AGENTS.md yoktu. SDK 10.0.401, EF SQL Server,
Design ve yerel araç 10.0.14 korundu; yeni paket/teknoloji eklenmedi. Gerçek
uygulama DB'sindeki iki FK ve birleşik unique index adları incelendi.

**Somut değişiklikler:** Dönem/açılış POST ve çalışan Location için detay GET;
ayrı istek türleri, toplu alan doğrulaması, varlık/benzersizlik ön kontrolleri.
Yalnızca yeni dönem veya açılış Add edilir; üst kayıtlar FK ile bağlanır. Dönem
ve açılış listeleri Id sıralı DB projection'a taşındı; açılış iki SQL JOIN kullanır.
Program'daki sabit listeler kaldırıldı. İlgili 2601 unique ve 547 FK hata dalları
dar kapsamlı eşlenir; CHECK 547 ve diğer hatalar genel 400/409 yapılmaz.
AcademicTermIds/CourseOfferingIds sequence/default migration'ı; seed, metadata,
HTTP ve mevcut izole test runner'ı güncellendi. Öğrenme notu ve ADR 0006 eklendi.

**Gerçek doğrulamalar ve sonuçlar:**

- Restore/build başarılı: 0 uyarı, 0 hata. Paket sürümleri değişmedi.
- Yeni Up/Down, designer/snapshot ve üretilen SQL incelendi. Mevcut tablo/satırlar
  korunur; Down önce default sonra sequence kaldırır. Down çalıştırılmadı.
- 20261027120003_GenerateTermAndOfferingIds yalnızca university_registration_app
  üzerinde, geçici API süreçleri kapalıyken uygulandı. Tekrar update: bekleyen
  migration yok. Model/snapshot farkı yok. Şema/default/NO ACTION kontrolü geçti.
- Seed uygulama DB'sinde iki kez çalıştı; mevcut örneklerde 0 ek satır. İzole
  testte yeni kullanıcı benzeri kayıtlar varken de iki seed, değerleri ve sayıları
  korudu. Sequence'ler gerektiğinde yalnızca ileri hizalanır.
- Mevcut runner iki ayrı yeni day6 DB'sinde geçti. İlk migration'a Course=500,
  Term=600, Offering=700 ve FK'leri eklenip yeni migration'larla korundu; üretilen
  yeni kimlikler bunların üstündeydi. Ortak 23 SQL kısıt deneyi tekrar kullanıldı.
- Ders POST/PUT/detay/listesi ve önceki 400/404/409/500, normalizasyon, restart,
  kimlik/kod yarışları regresyonu geçti. Üç GET sözleşmesi uygulama DB'sinde ve
  ilave dönem/açılışların bulunduğu test DB'sinde geçti.
- Dönem 201/Location/detay/liste; aynı başlangıç-bitiş kabulü; eksik/null/bozuk
  tarih, boş/uzun ad ve birleşik alan 400'leri; başarısızlıkta sabit kayıt sayısı.
- Açılış 201 yalnızca açılış sayısını artırdı; ilişkili ders/dönem yeniden eklenmedi.
  Liste/detay alanları, doğru adlar, normalizasyon, kapasite/şube/FK alan hataları,
  olmayan detay 404 ve aynı birleşim 409 geçti. Farklı şube/dönem kabul edildi.
- Altı paralel dönem ve altı bağımsız açılış ayrı kimlikler aldı. Aynı açılış
  yarışında bir 201/beş 409 ve tek DB kaydı doğrulandı.
- Test context interceptor'ı ön kontrolden sonra gerçek unique rakibi ve ayrı
  ayrı ders/dönem silinmesi oluşturdu: save aşamasında 409 ve doğru alan 400'leri
  doğrulandı. Gerçek CHECK 547 yanlış FK sayılmadı. Temel geçersiz alanlarda DB
  okuması olmadı. Üretim API'sine test kancası eklenmedi.
- Restart sonrası dönem/açılışlar kaldı. Yalnızca testin ders/dönem adları DB'de
  değiştirildi; açılış listesi/detayı yeni adları gösterdi. ToQueryString iki
  INNER JOIN gösterdi; üç tablo belleğe alınmadı.
- Tracking: new Detached, Add Added, save öncesi ayrı context'te yok; save sonrası
  üretilmiş Id/Unchanged. Entity sorgusu Unchanged; değişiklik+DetectChanges Modified.
  Ayrı context DB'de save öncesi eski, sonrası yeni değer okudu. Remove Deleted
  gösterildi fakat bu silme kaydedilmedi. Temiz context'te alan/JOIN projection'ları
  tracker'a entity eklemedi.
- Tek SaveChanges'te gerçek kredi CHECK'i iki eklemeyi de geri aldı; ayrı iki
  SaveChanges'te ilk başarılı kayıt kaldı. Hata sonrası tracker Added durumlarını
  tuttu; bağımsız okumalar yeni context'lerden yapıldı, hatalı context kullanılmadı.
- Finally önce açılışlar, sonra test dönem/dersleri temizledi; API süreçleri kapandı.
  Son test DB'si university_registration_tests_day6_b2d00ffec4a241b692fe8950746bbac4
  salt okunur kontrolde 2/2/3 seed kaydıyla kaldı. DB otomatik silinmedi.
- Laboratuvara yazılmadı; öncesi/sonrası sayılar 2/2/3, veri checksum'ları aynıydı.

**Açık noktalar/sınırlar:** Bugünkü kontrollerde engel yok. Down uygulanmadı.
SQL mesajındaki tırnaklı kısıt adlarının sırasına bağımlılık sürüyor; bilinmeyen
biçim 500'e bırakılır. Lost update, kontenjan tüketimi ve ileri transaction/isolation
çözümü yok. Dönem ve açılış ayrı oluşturma işlemleri; başarısız açılış dönemi geri
almaz. Test DB'leri seed ile tutulur, sequence boşlukları doldurulmaz.

**Sıradaki gün:** LINQ → SQL, IQueryable/IEnumerable, projection, DTO ve
AsNoTracking; filtreli ve sayfalı katalog.

**Commit/push:** Bu kaydı içeren kimlik Git geçmişinde
`git log --format=%H --grep="^feat: create academic terms and course offerings$"`
ile bulunur. Gerçek push sonucu son raporda bildirilir; hash için ek commit yoktur.

## Gün 7

**Konu:** LINQ → SQL, IQueryable/IEnumerable, projection, DTO ve AsNoTracking.

**Başlangıç:** Yerel/uzak main 0da70385908bc67bedde4eae04fc6ade12983a34;
çalışma ağacı temiz, uygulanabilir AGENTS.md yok. SDK 10.0.401, EF SQL Server,
Design ve dotnet-ef 10.0.14 korundu. Mevcut SQL_Latin1_General_CP1_CI_AS collation
salt okunur sorguyla doğrulandı. Paket/model/şema değişikliği yok; migration eklenmedi.

**Somut değişiklik:** Ayrı GET /courses/catalog; search Trim + Code/Name Contains,
isteğe bağlı pozitif credits eşitliği; page=1/pageSize=20, 1–100 sınırı ve güvenli
long offset hesabı. Alan hataları birlikte 400, desteklenmeyen offset 400.
CourseListItemDto eski liste ve katalogda ortak; liste biçimi/üç alanı korundu.
CourseCatalogResponse items/page/pageSize/totalCount taşır. Filtreli IQueryable'dan
sırayla CountAsync ve OrderBy/Skip/Take/Select/ToListAsync; CancellationToken iletilir.
Entity taşımayan DTO için AsNoTracking gerekmedi, PUT tracking'i değiştirilmedi.

**Gerçek doğrulamalar:**

- Restore/build: başarılı, 0 uyarı/0 hata. has-pending-model-changes: fark yok.
- Mevcut runner ayrı university_registration_tests_day6_0fc6cd84b97545edb1ffe8c22ca57d91
  DB'sinde çalıştı (day6 öneki mevcut güvenlik işaretiyle bilinçli korundu).
  Önceki SQL kısıt, ders/dönem/açılış HTTP, yarış ve SaveChanges regresyonları geçti.
- Katalog için 53 sayfalanan + 5 literal/aksan örneği oluşturuldu. Varsayılanlar,
  yalnız kod/yalnız ad, kredi, AND, Trim/boş arama, eşleşmeme, Id sırası, altı
  sayfada sınırlar/toplam/eksiksiz ve tekrarsız gezinme, sayfa aşımı geçti.
- page/pageSize/credits aralıkları, birden çok alan hatası, skip taşması, yanlış
  sayı biçimleri ve Int32 üst/alt taşmaları gerçek HTTP'de 400 verdi. Büyük ama
  desteklenen offset boş 200 döndü. DTO alanları tam id/code/name; Credits yok.
- catalog rotası ve int detay rotası birlikte çalıştı; eski /courses sayfasız dizi
  olarak kaldı. Üç eski GET sözleşmesi test ve uygulama DB'sinde geçti.
- `%`, `_`, apostrof literal Contains; küçük/büyük harf eşleşmesi ve aksan ayrımı
  gerçek DB/HTTP'de doğrulandı. Collation değiştirilmedi.
- Production katalog handler'ı test context'iyle çağrıldı; ReaderExecutedAsync
  tam iki tamamlanan gerçek SQL komutu gözledi. İkisinde filtreli WHERE; ilkinde
  COUNT, ikincide ORDER BY Id/OFFSET/FETCH ve yalnız Id/Code/Name seçimi doğrulandı.
  Arama/kredi/sayfalama parametreleri incelendi; SQL'e girdi birleştirilmedi,
  satır başına ek sorgu oluşmadı. SQL/parametreler log dosyasına yazılmadı.
  Geçersiz parametrede DB sorgusu yoktu. ToQueryString tek kanıt olarak kullanılmadı.
- Temiz context'lerde normal Course entity sorgusu 1 entry, AsNoTracking 0,
  saf DTO 0, entity içeren anonim projection 1 entry verdi. AsNoTracking nesnesini
  değiştirip SaveChanges çağırmak 0 yazma yaptı; ayrı context DB adının korunduğunu
  doğruladı. Yalnızca testin kaydı kullanıldı; önceki tracking deneyi kopyalanmadı.
- Finally katalog kayıtlarını kendi Id'leriyle temizledi; dış runner ilişkili
  kayıtları temizleyip süreçleri kapattı. Son salt okunur test DB kontrolü 2/2/3
  seed kaydı gösterdi. Uygulama/laboratuvar DB'sine test veya migration yazması yok.

**Eksik/sınır:** Bugünün kontrollerinde engel yok. COUNT ve items ayrı sorgu;
eşzamanlı değişiklikte aynı anın görünümü garanti edilmez. Transaction eklenmedi.
Offset maliyeti/veri değişirken kayma ve Contains'in ölçek sınırları belgeli;
benchmark, index/execution plan/full-text/keyset çalışması yapılmadı.

**Sıradaki gün:** Çoktan çoğa ilişki ve ilişki entity'si; öğrenci, ders kaydı ve
temel kayıt işlemi.

**Commit/push:** Bu kaydı içeren kimlik
`git log --format=%H --grep="^feat: add filtered and paginated course catalog$"`
ile bulunur. Gerçek push sonucu son raporda verilir; hash için ek commit üretilmez.

## Gün 8

**Konu:** Çoktan çoğa ilişki ve açık ilişki entity'si.

**Başlangıç:** Yerel/uzak main 6614f1e149a1236d76c4eacf31d06babde5e76e6;
çalışma ağacı temiz, uygulanabilir AGENTS.md yok. SDK 10.0.401, EF SQL Server,
Design ve yerel dotnet-ef 10.0.14 korundu. Uygulama DB'sinde yeni tablo adları
yoktu; mevcut 2 ders/2 dönem/3 açılış ve veri checksum'ları kaydedildi.

**Somut değişiklik:** Student ve Enrollment modelleri/DbSet/mapping; iki sequence,
ayrı PK'ler, öğrenci numarası unique ve öğrenci-açılış birleşik unique, zorunlu
FK'ler/NO ACTION, CreatedAtUtc datetimeoffset. Student POST/detay, Enrollment
POST/detay ve öğrencinin JOIN/projection kayıt listesi. İstek/yanıt DTO'ları,
Trim/alan doğrulaması ve dar 2601/547 hata eşlemesi. Yalnız yeni entity Add edilir;
UTC zamanı sunucudan, Id'ler sequence'den gelir. Mevcut ders/açılış eşlemeleri
korundu. Yeni genel öğrenci listesi veya PUT/DELETE yoktur.

**Gerçek doğrulamalar:**

- Restore/build başarılı, 0 uyarı/0 hata. Yeni paket yok.
- Up/Down, designer/snapshot ve üretilen SQL incelendi. İleri SQL sadece iki
  yeni tablo, iki sequence/default, PK/FK/unique ve gereken ilişki index'ini ekler.
  Down yeni tabloları kaldırır; uygulanmadı, geri alma testi yapılmadı.
- 20261027123407_AddStudentsAndEnrollments açık komutla yalnızca
  university_registration_app üzerinde uygulandı. Tekrar update: bekleyen yok;
  model/snapshot farkı yok. Başlangıçtaki 2/2/3 kayıt ve checksum'lar korundu.
- Gerçek şema kontrolü: nvarchar uzunlukları/NOT NULL, datetimeoffset/NOT NULL,
  PK, FK hedef/sütunları, NO ACTION, unique, sequence/default ve IDENTITY olmaması
  geçti. Uygulama seed'i iki kez çalıştı, örnek satır çoğalmadı.
- Mevcut runner ayrı university_registration_tests_day6_f5c3f7ef308846e8a16ee293ca84cf69
  DB'sinde tüm önceki ders/katalog/dönem/açılış, tracking, SQL kısıt ve yarış
  regresyonlarını çalıştırdı; geçti. Mevcut day6 DB öneki/işareti korundu.
- Öğrenci 201/Location/detay; Trim, baştaki sıfırlar ve harf biçimi korundu.
  Aynı adla farklı numaralar kabul edildi. CI collation'a göre aynı numara 409;
  null/boş/boşluk/uzun alanlar toplu 400; reddedilen isteklerde satır artmadı.
- Bir öğrenci aynı dersin A/B açılışlarına, iki öğrenci aynı açılışa kaydoldu.
  Student/CourseOffering sayıları değişmedi. 201/Location/detay alanları ve
  sunucu UTC zamanı istek öncesi/sonrası aralığında doğrulandı; istemciden gönderilen
  Id/zaman kullanılmadı. Restart sonrası öğrenci ve kayıt kalıcılığı geçti.
- Tekrar çift 409; geçersiz/olmayan FK'ler alan 400; eksik detaylar 404; var olan
  öğrencinin boş kayıt listesi 200/[]; olmayan öğrencinin listesi 404. JOIN listesi
  doğru ders/dönem/şube ve Id sırası verdi. Altı aynı çift POST: bir 201/beş 409,
  DB'de tek satır. Bu kontenjan veya genel idempotency garantisi değildir.
- Doğrudan gerçek tekrar çift ve hatalı FK eklemeleri reddedildi. Test context
  interceptor'ı ön kontrolden sonra rakip öğrenci/çift ekleyip ilgili üst kaydı
  silerek dört gerçek save hata dalını ayrıca çalıştırdı: numara/çift 409,
  Student/Offering FK doğru alan 400. Üretime test kancası eklenmedi.
- İlgisiz kredi CHECK 547, ders kodu unique 2601 ve PK 2627 yeni eşleyicide
  kayıt hatası sayılmadı. Mevcut güvenli 500 regresyonu geçti. Bağlı öğrenci ve
  açılış DELETE denemeleri gerçek FK 547 ile reddedildi; üç ilgili satır korundu.
- ReaderExecuted interceptor'ı kayıt listesi için sabit iki gerçek sorgu gördü:
  öğrenci varlığı + üç JOIN'li filtreli/sıralı projection. N+1 yok, DTO tracker boş.
  Geçersiz alanlı yeni POST'larda DB sorgusu yoktu. Seed kayıtlar varken tekrar
  çalıştırıldı, öğrenciler/kayıtlar korundu.
- Finally Enrollment → test öğrenci/açılış sırasıyla temizledi, dış runner diğer
  test verilerini ve geçici süreçleri kapattı. Son test DB sayıları 2/2/3/0/0.
  Uygulama DB'sine test öğrenci/kaydı yazılmadı. Laboratuvarın üç tablosu ve 2/2/3
  örnekleri ayrı kaldı; laboratuvara migration/seed/test yazması yapılmadı.

**Açık sınırlar:** Bugünkü kontrollerde engel yok; Down uygulanmadı. Kontenjan
kontrolü/tüketimi, ön koşul, kredi sınırı, saat çakışması, farklı şube yasağı,
danışman onayı, durum geçişi ve bırakma yok. Student iş profilidir, Identity hesabı
değildir. Kimlik doğrulama/yetkilendirme yok; StudentId kabulü güvenli self-servis
değildir. SQL mesajındaki nesne adı/sıra bağımlılığı belgeli; bilinmeyen hata
merkezi 500'e bırakılır. Test DB'leri seed ile tutulur, sequence boşlukları normaldir.

**Sıradaki gün:** ACID, commit/rollback, EF varsayılan/açık transaction; bağlı
kayıt değişikliklerini birlikte kaydetme.

**Commit/push:** Bu kaydı içeren kimlik
`git log --format=%H --grep="^feat: add students and basic course enrollment$"`
ile bulunur. Push sonucu son raporda verilir; hash için ek commit oluşturulmaz.
