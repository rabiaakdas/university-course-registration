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

## Gün 9

**Konu:** ACID, commit/rollback, varsayılan SaveChanges transaction'ı ve açık transaction.

**Başlangıç:** Yerel/uzak main e5ace9efcc0877069d52dbfc45d7bd96c4a5557f;
çalışma ağacı temiz, uygulanabilir AGENTS.md yok. Mevcut kayıt/hata/DTO düzeni ve
6. günün A/B SaveChanges deneyleri incelendi. App bağlantısında MARS=false;
UseSqlServer dışında retry yapılandırması yok. Bağlantı değerleri yazdırılmadı.
SDK 10.0.401 ve mevcut EF 10.0.14 paket/araçları korundu.

**Değişiklik:** POST /students/{id:int}/enrollments/batch. Önce liste yapısı
(1–50, pozitif, tekrarsız), sonra öğrenci, toplu açılış ve toplu mevcut kayıt
kontrolleri. 50 teknik sınırdır. Ortak sunucu UTC zamanı, AddRange ve tek
SaveChangesAsync. İstek sıralı EnrollmentResponse items zarfı; hayalî Location
yok. İlgili mevcut unique/FK eşleyicisi kullanılır. Üretimde BeginTransaction,
retry, yeni tablo veya migration yok. Önceki deney aynı dosyada C/D ile genişledi.

**Gerçek doğrulamalar:**

- Restore/build başarılı, 0 uyarı/0 hata. Model/snapshot farkı yok.
- Mevcut izole runner university_registration_tests_day6_e61cafc7644a4ec7b27a674fa6c4c895
  üzerinde bütün eski SQL, ders/katalog/dönem/açılış/öğrenci/tekli kayıt ve seed
  regresyonlarını çalıştırdı; geçti. Uygulama DB'sindeki üç GET kontrolü de geçti.
- Geçerli batch gerçek HTTP'de 201; doğru items alanları, istek sırası, ortak
  UTC zaman aralığı, çalışan detay GET'leri ve öğrenci listesi. Üst tablo sayıları
  değişmedi; restart sonrası DB ve HTTP'den kayıtlar görüldü. Location yok.
- **Ön kontrol kanıtı:** Null/boş/tekrar/pozitif olmayan/51 öğeli liste ve yanlış
  JSON türleri 400. Yapı bozuksa olmayan öğrenci için de önce 400; geçerli yapıda
  olmayan öğrenci 404. Eksik açılış 400; mevcut seçimlerden biri kayıtlıysa 409.
  Yeni context okumalarında önceki kayıtlar korundu, yeni/kısmi kayıt oluşmadı.
  Bu kontroller yazma atomikliği kanıtı olarak sunulmadı.
- 50 öğe sınırında production handler gerçek DB ile 201 verdi. Interceptor üç
  toplu ön kontrol SELECT'i gördü; satır başına sorgu yok. INSERT komutlarının
  toplam sayısı provider'a bağlı bırakıldı. Geçersiz yapıda DB sorgusu yoktu.
- **Gerçek yazma hatası kanıtı:** Test context'i üretim batch handler'ını çalıştırdı.
  SavingChanges interceptor'ı ön kontrollerden sonra başka context'te rakip çifti
  commit etti. Yalnız testte MaxBatchSize(1) ile ilk INSERT'in başarıyla yürüdüğü
  gözlendi; sonraki gerçek unique hatası 409 oldu. Yeni context'te batch'in ilk
  kaydı yoktu, rakibin kesinleşmiş kaydı kaldı. Failed tracker Added tutuyordu;
  yeniden SaveChanges çağrılmadı. Üretim batching ayarı/test parametresi eklenmedi.
- Öğrenci/açılış ön kontrolden sonra ayrı ayrı kaldırılarak gerçek save FK
  hataları çalıştırıldı; doğru alan 400 ve yeni context'te sıfır batch kaydı.

**Dört transaction deneyinin gerçek sonuçları (mevcut Course CHECK örneği):**

| Deney | Yeni context'te sonuç |
|---|---|
| A: İki yeni kayıt, tek SaveChanges, gerçek CHECK hatası | İkisi de yok |
| B: İki bağımsız SaveChanges, ikincisi CHECK hatası | İlk var, ikinci yok |
| C: Açık transaction, ilk save başarılı, ikinci CHECK hatası, tam rollback | İkisi de yok |
| D: Açık transaction, iki başarılı save, commit | İkisi de var |

C'de hata sonrası aynı transaction'da ilk kayıt vardı; savepoint ile tam rollback
ayrımı gözlendi. Rollback tracker'ı temizlemedi. Bağımsız okumalar commit/rollback
sonrasında yapıldı; başka bağlantının commit öncesi hemen boş döneceği varsayılmadı.
MARS=false, execution strategy RetriesOnFailure=false, SupportsSavepoints=true
runtime kontrolleri geçti. Cleanup CancellationToken.None kullanır; rollback hatası
ayrı kaydedilir ve asıl hata korunur. Transaction nesneleri await using ile dispose
edilir. Cleanup rollback hatası ayrıca enjekte edilmedi.

**Temizlik/sınırlar:** Finally yalnız test Enrollment'ları ve üst test kayıtlarını
FK sırasıyla temizledi; dış runner eski deneyleri ve süreçleri temizledi. Son DB
sayısı 2/2/3/0/0. Kullanıcı/app/lab verilerine deney yazması veya şema değişikliği
yok. Test DB seed ile tutuldu, sequence boşlukları doldurulmadı. Kontenjan/akademik
kurallar, yetkilendirme, isolation karşılaştırması ve genel retry/idempotency yok.
Commit sonrası yanıt kaybı commit'in olmadığını kanıtlamaz. Bugünkü kontrollerde
engel yok; rollback temizliğinin hata yolu ve disk/elektrik arızası test edilmedi.

**Sıradaki gün:** Unit/integration test ve davranış doğrulama.

**Commit/push:** Bu kaydı içeren kimlik
`git log --format=%H --grep="^feat: add atomic batch enrollment$"`
ile bulunur. Gerçek push sonucu son raporda bildirilir; hash için ek commit yoktur.

## Gün 10 — Unit/integration ve davranış doğrulama

**İnceleme:** Mevcut gerçek HTTP/LocalDB runner ve Course/Related/Catalog/
Enrollment/Batch kontrolleri okundu. Kısa envanter `docs/testing.md` içindedir.
Başarı/tekrar önceki geniş akışta vardı; ayrı senaryo seçimi, her istekte yeni
context ile tam kayıt korunması ve saf doğrulama seçimi eksikti.

**Yeni/iyileştirilen:** `CoreBehaviorChecks` gerçek CourseInput.Validate üzerinde
pozitif olmayan kredi, null/boş/boşluk, normalizasyon/ad temizliği ve birlikte
alan hatalarını DB'siz kontrol eder. Üç bağımsız HTTP fixture'ı: başarı 201 +
Location/detail + DB FK/zaman/sayı farkı; gerçekten olmayan açılış 400 + ilgili
alan; tekrar 409 + tam bir çift. Retlerde ve başarıda başlangıç kayıtlarının
kimlik/zaman/FK değerleri yeni context üzerinden korunur. Parent sayıları değişmez.
Fixture'lar üretilmiş kimliklerle çalışır, sıraya veya seed'e dayanmaz.

**Yeniden kullanılan:** Mevcut HTTP istemcisi, bounded readiness polling, API süreç
yaşam döngüsü, geçici konsol projesi ve bütün geniş regresyonlar korundu. Fault,
concurrency, katalog SQL ve transaction deneyleri kopyalanmadı. PowerShell unit/
core/full seçer ve başarısız alt komut kodunu iletir. Framework/paket/ürün kodu/
model/migration değişikliği yok. Karar ADR 0010'da.

**Gerçek sonuçlar:** Restore/build başarılı (0 uyarı/0 hata). Birim modu geçti.
İlk test kaynağı derlemesindeki yerel değişken adı çakışması düzeltildi; başarısız
ilk deneme geçiş sayılmadı. Son hâliyle iki ardışık core koşusu başarılı:

- Normal sıra: `university_registration_tests_day10_445e27c47af846918fe523faee7a8cba`.
- Ters sıra: `university_registration_tests_day10_d64bca45b5b046438ab4de5e408cb92e`.

Her koşuda üç senaryo ve finally temizliği geçti; beş tablo boş, pending migration
ve model/snapshot farkı yok, API süreci kapandı. Önceki çekirdek denemeleri de
başarılıydı. `-Mode unit -ProbeFailure` beklenen assertion'ı üretti ve üst komut
exit 1 döndürdü; kalıcı başarısız test yok.

Full regresyon `university_registration_tests_day6_37f2e592a9de4273963c51f10a41e81d`
üzerinde geçti: seed tekrarı, 23 SQL kısıt deneyi, ders yazma/okuma, dönem/açılış,
katalog gerçek SQL/tracking, öğrenci/kayıt, batch ön kontrol ve gerçek save hatası,
dört transaction deneyi. Eski full takımı yeniden yazılmadı. Çalışan API süreci
kalıntısı görülmedi. SDK 10.0.401, LocalDB 17.0.4025.3; paketler değiştirilmedi.

**İzolasyon/sınırlar:** App/lab mutasyonu yapılmadı. Core yalnız bu koşunun
kimliklerini FK sırasıyla temizler; test DB şeması tutulur, sequence sıfırlanmaz.
Full DB seed ile kalır. Assertion, ortam/çalıştırma ve temizlik ayrı raporlanır;
cleanup hatası asıl hatayı gizlemez. Cleanup hata yoluna fault enjekte edilmedi;
Down çalıştırılmadı. İki geçiş tüm olası hataların yokluğu değildir. Birim testleri
SQL/HTTP kanıtı değildir. Kontenjan/akademik uygunluk/yetkilendirme uygulanmadı.

**Sıradaki gün:** SOLID'in pratik sınırları, composition ve gereksiz soyutlama;
kayıt sorumluluklarını değerlendirme.

**Commit/push:** Kaydı içeren commit `git log --format=%H --grep="^test: strengthen enrollment behavior checks$"`
ile bulunur. Push sonucu son raporda verilir; hash için ikinci commit yoktur.

## Gün 11 — SOLID sınırları ve kayıt sorumlulukları

**Önce inceleme/değerlendirme:** Repo/main temizdi; gerçek HEAD 5d372f7 idi.
Tekli/batch CreateAsync içinde HTTP, ön kontrol, UTC, Add/Save ve SQL hata yanıtı
birlikteydi. Gerçek tekrar save hata sınıflandırmasıydı; tekli iki alanı birlikte
400 değerlendirmesi ile batch'in öğrenci 404 → açılış 400 sırası aynı değildi.
Somut karar tablosu `docs/day11-responsibilities.md`, karar ADR 0011 içindedir.

**Refactor:** Scoped somut `EnrollmentOperations` iki ayrı işlem metodunu taşır.
Endpoint'lerde binding/istek biçimi, teknik 50 öğe sınırı ve HTTP eşlemesi kaldı.
İşlemde DB varlık/tekrar kontrolü, UTC, Enrollment üretimi, Add/AddRange ve tek
SaveChanges bulunur. Başarı/ret için ayrı tekli/batch sonuç türleri; nullable genel
sonuç torbası yok. Mevcut DTO tekrar kullanılır; işlemde HttpContext/IResult/
Results/status code yok. RegistrationWriteErrors ve güvenli merkezi handler
korundu. Program.cs somut scoped DI kaydı ekler. Interface/repository/UoW,
generic Result, yeni proje, Strategy ve paket eklenmedi.

**Korunan anlamlar:** Tekli Add+save, batch AddRange+tek save; batch tekli işlemi
döngüyle çağırmaz. Ön kontrol retleri ile save FK kaybı farklı sonuç olarak taşınır.
Route/alanlar/Location/201/400/404/409, batch sırası ve UTC korunur. Hata/iptal
“kayıt bulunamadı” sayılmaz; failed context yeniden kaydedilmez. Şema ve önceki
migration'lar değişmedi. DI kullanımı EF/SQL Server bağımsızlığı iddiası değildir.

**Gerçek kontroller:**

- Değişiklik öncesi core geçti: `university_registration_tests_day10_d345e779e803458c862af12fd9154bed`.
- Refactor sonrası restore/build: 0 uyarı/0 hata. Unit ve core geçti;
  core DB `university_registration_tests_day10_516cb1773f934003a658f5e8e8390c5f`.
  Başarı 201, olmayan açılış 400, tekrar 409 ve tek DB çifti; eski kayıtlar korundu.
  Model/snapshot farkı ve bekleyen migration yok; cleanup sonrası beş tablo boş.
- Full geçti: `university_registration_tests_day6_b21f25c75ebe4924bd09141d59f00145`.
  Seed/23 SQL kısıt deneyi, ders/dönem/açılış, katalog SQL/tracking, öğrenci/tekli
  kayıt, transaction A–D ve batch HTTP regresyonları yeniden kullanıldı.
- Aynı interceptor deneyleri yeni nesneye bağlandı; beklentiler gevşetilmedi.
  Tekli save unique ve iki FK dalı doğru HTTP status/alanı korudu. Save yarışında
  üç ön kontrol SELECT'i doğrulandı; önceki kaynak kodda da üç sorgu vardı.
  Batch 50 öğede üç SELECT kontrolü korundu, satır başına sorgu oluşmadı.
- Batch'te başarılı ilk INSERT sonrası gerçek unique hatası: kendi yazmaları yok,
  rakip commit var. Gerçek iki FK save hatasında kısmi batch yok. Ön kontrol retleri
  bu yazma atomikliği kanıtının yerine sunulmadı.
- Doğrudan işlem çağrısında iptal token'ı OperationCanceledException olarak yayıldı;
  yeni context'te kayıt sayısı değişmedi. Yeni write senaryosu kopyalanmadı.

**Temizlik/sınırlar:** Testler yalnız GUID adlı izole DB'leri kullandı; app/lab
hedeflenmedi. Core fixture'ları temizlendi ve süreç kapandı; full kendi kayıtlarını
FK sırasıyla temizleyip seed'i bıraktı. Test DB şemaları tutuldu. Down, cleanup
arızası ve aktif sorgunun ortasında iptal ayrıca test edilmedi; önceden iptal edilmiş
token sınandı. Beklenmeyen altyapı arızalarının tamamı denenmedi. SQL mesajındaki
nesne adı ayrıştırma sınırı sürüyor. Kontenjan/akademik uygunluk/yetkilendirme yok.

**Öğrenme:** SRP tek metot demek değildir; composition nesneleri birleştirir,
DI yaşam süresi/sağlama mekanizmasıdır, DIP otomatik oluşmaz. OCP yeni ihtiyaca kod
uyarlamayı yasaklamaz. LSP'de ret/başarı/iptal anlamı; ISP'de gerçek tüketici ihtiyacı
önemlidir. Bugün olmayan değişkenlik için soyutlama eklenmedi.

**Sıradaki gün:** Strategy; değiştirilebilir kayıt önceliği hesaplama.

**Commit/push:** Kaydı içeren kimlik `git log --format=%H --grep="^refactor: clarify enrollment responsibilities$"`
ile bulunur. Push sonucu son raporda bildirilir; hash için ikinci commit yoktur.

## Gün 12 — Strategy ile kayıt önceliği önizlemesi

**İnceleme:** Temiz repo/main, gerçek HEAD fcebf4b; endpoint/DTO ve unit/core/full
konsol düzeni incelendi. Paketler değiştirilmedi. EnrollmentOperations ve mevcut
kayıt akışları önizleme için değiştirilmedi.

**Somut katkı:** Get-only kontrollü PriorityInput; IRegistrationPriorityStrategy;
GraduationPriorityStrategy ve CompletedCreditsPriorityStrategy. Graduation kalan
<=20 için100, aksi0; completed puanı tamamlanan kredi. Kurucu parametre koşulları
ve her iki stratejide null sözleşmesi korunur. Endpoint küçük switch ile normalize
policy seçer; nullable istek alanlarının bağımsız hatalarını birlikte 400 döndürür.
`POST /registration-priority/preview` yalnız normalize policy/score döndürür.
DB bağımlılığı, yeni schema/Student alanı, migration veya kayıt kararı değişikliği
yok. Factory/DI/calculator zinciri eklenmedi; durumsuz stratejiler doğrudan seçilir.

**Gerçek birim/HTTP sonuçları:** Restore/build 0 uyarı/0 hata. Unit geçti:
219/220/221/240/250 graduation sınırları; completed 0/120/225; negatif completed,
sıfır/negatif required ve parametre adları; fazla tamamlanan kredi, iki stratejide
null, deterministik sonuç ve Int32 uçları. Yeni framework/paket yok.

Preview modu gerçek HTTP'de iki politika, trim/harf normalizasyonu, yalnız iki
yanıt alanı, 0 completed kabulü; bilinmeyen/null/boş politika, eksik/null krediler,
negatif/sıfır sınırlar ve birlikte üç alan hatasını doğruladı. Yanlış JSON türü,
bozuk JSON ve Int32 taşması 400 verdi. Formül eşiklerinin hepsi HTTP'de tekrar
kopyalanmadı; saf birim sınırında test edildi.

**DB bağımsızlığı kanıtı:** Geçerli bağlantı biçimiyle rezerve edilmiş ama
SQL dinlemeyen loopback portuna hedeflenen ayrı API süreci kullanıldı. Startup
sözleşmesi değiştirilmedi. Aynı süreçte /courses=500, preview=200; DB kontrol
isteğinden sonra preview yine200. LocalDB kapatılmadı; bu deney DB oluşturmadı veya
veri değiştirmedi. Bağlantı teşebbüsü sayan interceptor yok; sıfır teşebbüs
ölçüldüğü iddia edilmiyor. Kod incelemesinde hesaplama yolunda DbContext/sorgu/
SaveChanges veya dış servis yoktur. Preview kontrolleri ayrıca full içinde geçti.

**Regresyonlar:** Core DB `university_registration_tests_day10_20031f67aaf14377b1b84df19dace068`:
üç kayıt senaryosu geçti, beş tablo temiz, model/snapshot farkı ve pending migration
yok. Full DB `university_registration_tests_day6_7c7a08780fa74672bd47eee7a218c9d6`:
mevcut seed/SQL kısıtları, ders/katalog/dönem/açılış/öğrenci/tekli ve batch HTTP,
SQL/tracking/transaction, gerçek save unique/FK ve ilk INSERT sonrası batch
rollback/rakip commit koruma kontrolleri geçti. Mevcut testler yeniden kullanıldı.

**Sınırlar/temizlik:** App/lab hedeflenmedi. Preview process/socket/client ve
regresyon süreçleri kapandı; test verileri mevcut finally düzeniyle temizlendi.
Test DB şemaları tutulur, sequence sıfırlanmaz. Down ve cleanup arızası test edilmedi.
Gerçek transkript/yetki/yerleştirme/eşit puan sıralaması yok; puan politikalar
arasında ortak ölçek değildir. Engellenen kontrol yok. Testler yalnız sınanan
örnek davranışlara güvence verir.

**Öğrenme:** Sözleşme/somut strateji/kullanan kod ayrımı; hesaplama ile seçimin
ayrılması. Küçük switch korunabilir. Switch/Func alternatifinin maliyeti ve
Strategy'nin ek tür bakım maliyeti öğrenme notu/ADR 0012'de açıklanır.

**Sıradaki gün:** Simple Factory, Factory Method, Abstract Factory ve DI farkları.

**Commit/push:** Kaydı içeren kimlik `git log --format=%H --grep="^feat: add strategy-based priority preview$"`
ile bulunur. Push sonucu son raporda verilir; hash için ek commit yoktur.

## Gün 13 — Simple Factory, Factory Method, Abstract Factory ve DI

**İnceleme/karar:** Repo/main temizdi, gerçek HEAD 4dba0a4 idi. Önizlemede tek
switch, tek tüketici ve seçilen stratejinin null olmasına dayalı validation var;
bağımsız destek listesi yok. Hata mesajındaki adlar ikinci seçim algoritması
sayılmadı. Simple Factory'ye taşıma endpoint'i birkaç satır kısaltır, fakat bugün
ek tür bakım maliyetini karşılamaz. Üretim switch'i, normalizasyon ve API aynen
korundu. Somut A/B değerlendirmesi öğrenme notu ve ADR 0013'te.

**Deney katkısı:** Yalnız scripts/FactoryExperiments.cs içinde üç çalıştırılabilir
alternatif: normalize adla TryCreate/NotNullWhen(true), protected abstract
CreateStrategy kullanan ortak Preview creator akışı, strategy+explanation sağlayan
iki Abstract Factory ailesi. Formüller mevcut gerçek stratejilerden kullanılır;
açıklamalar eşikleri kopyalamaz. Null/bilinmeyen/non-normalize factory girdisi
false/null; başarılı çağrı yeni durumsuz nesne. Üretim DI/endpoint/web derlemesine
deney türleri eklenmedi. Paket/proje/migration ve üretim özellik değişikliği yok.

**Gerçek sonuçlar:** Restore/build 0 uyarı/0 hata. Factories ve unit DB olmadan
geçti. Simple Factory başarı/başarısızlık/yeni nesne kontrolleri; Factory Method
225/240 girdisinde 100/225; Abstract Factory 100 + kalan15 ve 225 + tamamlanan225
uyumu, kredi aşımında kalan0 kontrolü geçti. Web assembly'sinde deney türü yok.
Factories -ProbeFailure beklenen assertion üretti, üst PowerShell exit1 verdi;
normal çalıştırma başarılı. Kalıcı bozuk test eklenmedi.

Preview kontrolü erişilemeyen SQL hedefinde /courses500 ve preview200 davranışını
korudu; normalize politika, JSON/alan hataları ve puanlar geçti. Bağlantı teşebbüsü
sayacı kullanılmadı; yalnız erişilebilir DB gerekmemesi kanıtı ve kaynak kodundaki
DB bağımlılığı yokluğu raporlanır.

Core: `university_registration_tests_day10_5a9ee94fb0304ed1ad2fb3f06bdda5ad`.
Üç kayıt senaryosu, temiz beş tablo, pending migration/model-snapshot uyumu geçti.
Full: `university_registration_tests_day6_c6bfd12a3edb442bb35412e094ad57fd`.
Seed tekrarı/23SQL kısıt deneyi, ders/katalog/dönem/açılış/öğrenci/tekli-batch HTTP,
tracking/transaction ve gerçek save FK/unique dalları geçti. Batch ilk başarılı
INSERT sonrası hata deneyinde batch yazmaları geri alındı, rakip commit korundu.
Önceki deneyler yeniden kullanıldı; yeni framework'e taşınmadı.

**Temizlik/sınırlar:** App/lab hedeflenmedi; geçici süreçler kapatıldı. Core test
kayıtları temizlendi; full seed ile tutuldu, yalnız kendi verilerini temizledi.
Sequence sıfırlanmadı, DB şemaları inceleme için tutulur. Down ve cleanup arızası
enjekte edilmedi. Engellenen kontrol yok. Gerçek yerleştirme/yetki/akademik kredi
ve kontenjan hâlâ kapsam dışı. Açıklama yalnız deneye aittir, API alanı değildir.

**Öğrenme:** Simple Factory GoF Factory Method değildir. Seçim factory ile yok
olmaz; override noktası ile ürün ailesi farklı ihtiyaçlardır. DI bağımlılık
sağlama/kurulum/yaşam süresidir, otomatik politika kararı veya DIP değildir.
Kaynak tutan factory ürünlerinde sahiplik/dispose sözleşmesi gerekir; bugün bu
varsayımsal ihtiyaca altyapı eklenmedi. Yeni aile/yeni ürün türü maliyeti notta.

**Sıradaki gün:** Specification; ön koşul ve kredi sınırı kuralları.

**Commit/push:** Kaydı içeren kimlik `git log --format=%H --grep="^refactor: clarify priority creation and factory alternatives$"`
ile bulunur. Push sonucu son raporda verilir; hash için ek commit yoktur.

## Gün 14 — Specification: ön koşul ve dönemlik kredi sınırı

**İnceleme:** Repo/main temizdi, gerçek HEAD 7bde90e idi. EF/SDK araçları mevcut
10.0.14/10.0.401 olarak korundu. App DB'de dört migration ve beş uygulama tablosu
vardı. Tekli/batch varlık/mükerrer kontrolleri, sonuç türleri ve eski fault/test
verileri incelendi. Önceki 50 seçimli testin aynı dönemde 200 kredi istediği
belirlendi; sınırı kapatmak yerine test dönemleri ayrıldı.

**Modeller/migration:** CoursePrerequisite (CourseId,PrerequisiteCourseId) ve
StudentCompletedCourse (StudentId,CourseId) birleşik PK'li iki tablo eklendi.
Dört zorunlu FK NO ACTION; doğrudan self-prerequisite CHECK. Kimlik stratejileri,
önceki migration'lar ve varsayılan seed değişmedi. Yeni migration
`20261029103645_AddPrerequisitesAndCompletedCourses`: Up/Down, snapshot farkı ve
üretilen SQL incelendi; yalnız iki tablo, iki ilişki index'i, anahtar/kısıtlar ve
migration geçmişi ekleniyor. Down iki yeni tabloyu kaldırır; çalıştırılmadı.

**Uygulama:** Saf PrerequisitesSatisfiedSpecification ve CreditLimitSpecification;
FrozenSet'e savunmacı kopyalanan pozitif kimlikler, null sözleşmesi, kontrollü long
kredi girdisi. Taşmasız limit-current karşılaştırması. EnrollmentPolicy ayarı
Program'da başlangıçta doğrulanır; appsettings açık örnek30 içerir, fallback yok.
EnrollmentOperations seçilmiş açılış/dersten metadata, gerekli ön koşul/başarı ve
ilgili öğrenci/dönem yükünü toplu hazırlar. SQL SUM girdisi bigint'tir. Önce
istek sırasındaki ön koşul, sonra ilk görünme sırasındaki dönem: ilk retle durur.
AcademicRejection HTTP'den bağımsızdır; endpoint400 Problem Details code ve ilgili
kimliği üretir. Eski409/404/alan400 ve FK/unique sınıflandırması korunur.

**Gerçek birim/başlangıç sonuçları:** Restore/build başarılı, 0 uyarı/0 hata.
Boş/tek/eksik/tam/fazla tamamlanan ön koşul, dış liste değişimi, null/kimlik
kuralları geçti. 24+5/6 kabul,24+7 ret; mevcut sınırüstü ret;0 ve long uçları
geçti. Eksik/boş/bozuk/sıfır/negatif/Int64 taşan ayarlar ayrı API süreçlerinde
anlaşılır başlangıç hatası verdi. Geçerli30 ayarı HTTP regresyonlarında çalıştı.

**Gerçek HTTP/DB/SQL sonuçları:**

- Specifications ilk koşusu: `university_registration_tests_day6_c88c89e5cd344b2da40d26188ce44d72`.
  Son ek fail-fast assertion'ıyla yeniden koşu:
  `university_registration_tests_day6_fbd3353eaa954fa19ef2803a64649f91`; ikisi geçti.
- Enrollment'ın tamamlanma sayılmaması, başka öğrencinin başarısının kullanılmaması,
  başarı kaydıyla kabul ve aynı batch ön koşul seçiminin reddi geçti. Eksik ön
  koşul400/code/açılış, kredi400/code/dönem kimlikleri doğru; ret sonrasında yeni
  context'te bütün eski kayıtlar korunuyor ve kısmi kayıt yok.
- Tam30 kabul,24+7 ret,24+4+4 toplu ret, başka dönem ayrımı, çok dönemli bir aşımda
  bütün batch reddi, aynı dersin iki şubesinin iki kez sayılması geçti. İstek
  sırası fail-fast kimliğini belirledi. İki koşul birlikte reddettiğinde prerequisite
  önce döndü. Başarılı tekli Location/detail ve batch yanıt sırası korundu.
- Gerçek SQL capture altı SELECT; filtreli JOIN/GROUP BY/SUM(bigint) doğrulandı.
  Int32'yi aşan mevcut yük de500/taşma yerine kredi reddi verdi. Satır/kural başına
  sorgu yok. Önceki üç sorguya üç toplu akademik sorgu maliyeti eklendi.
- Her iki ilişki PK tekrarı2627, dört FK547 ve selfCHECK547 doğru nesne adıyla
  reddedildi. İki derslik dolaylı döngü kabul edildi: CHECK'in sınırı açıkça gözlendi.
- Unit/core/full ve full içindeki preview/factory/SQL/tracking/transaction regresyonları
  geçti. Core: `university_registration_tests_day10_c16e6dfa58aa44a2830a0fa285ab88a9`.
  Full: `university_registration_tests_day6_cc72f60bca4541c792cacc1faca210e2`.
  50 öğe, 10 dönem × 5 açılış × 4 kredi ile başarılı; sınır30 kaldı. Gerçek single/batch
  unique/FK save dalları ve ilk INSERT sonrası batch rollback/rakip commit koruma
  aynı interceptor deneyleriyle geçti. Altı sorgu assertion'ı yeni ihtiyaca göre
  güncellendi, gerçek hata assertion'ları gevşetilmedi.
- Opsiyonel SQL full içinde ve ardından iki ardışık koşuda sentetik kimliklerini OUTPUT ile aldı, ilişkileri doğruladı ve
  rollback yaptı. Seed tekrarı/23 eski SQL kısıt kontrolü de full içinde geçti.

**Uygulama DB sonucu:** Yalnız `university_registration_app` açık hedefiyle
migration uygulandı; ikinci update no-op. has-pending-model-changes fark bulmadı.
verify.sql yeni kolon/birleşikPK/NO ACTION/CHECK metadata dahil geçti. App sayıları
2 ders/2 dönem/3 açılış/0 öğrenci/0 kayıt olarak korundu; iki yeni tablo 0 satırdır.
Örnek başarı verisi gerçek uygulama öğrencilerine yazılmadı; lab hedeflenmedi.

**Temizlik/sınırlar:** İzole test ilişkileri üst kayıtlardan önce yalnız fixture
kimlikleriyle temizlendi; core/specifications yedi tablo boş, full seed ile kaldı.
Test DB şemaları tutulur, sequence geri sarılmaz. Geçici süreçler kapandı. Down,
cleanup arızası ve eşzamanlı kredi aşımını engelleme testi yapılmadı; sonuncusu
ürün garantisi değildir. İki istek aynı yükü okuyabilir. Ön koşullar yalnız doğrudan,
kredi güncel Course.Credits üzerinden; değişirse eski yük değişir. Kontenjan,
not/transkript, dolaylı döngü, kilit/serializable/token/retry eklenmedi. Engel yok.

**Sıradaki gün:** Birleşik kurallar ve açıklanabilir ret nedenleri; saat çakışması
ve toplu uygunluk sonucu.

**Commit/push:** Kaydı içeren kimlik `git log --format=%H --grep="^feat: enforce prerequisite and credit specifications$"`
ile bulunur. Push sonucu son raporda bildirilir; hash için ek commit yoktur.

## Gün 15 — Birleşik kurallar, açıklanabilir retler ve saat çakışması

**Kavramlar/değişiklik:** Haftalık yerel saat, yarı açık aralık, AND ile tüm
bağımsız retleri toplama, yeni–mevcut/yeni–yeni çift tekilleştirme, belirli sıra,
önizleme/kayıt ortaklığı. CourseOfferingTimeSlot (sequence, FK NO ACTION,
Day0..6, time(7), start<end, birebir UNIQUE) eklendi. WeeklyTimeSlot kontrollü
saf değer; AcademicEligibility eski ön koşul/kredi specification'larını yeniden
kullanır. EnrollmentOperations veriyi filtreli toplu sorgularla hazırlar.
AcademicEvaluation savunmacı kopya üzerinden IsEligible türetir; retler ayrık
code/message/kimlik tipleridir. Genel composite/rule engine eklenmedi.

**Bilinçli sözleşme değişikliği:** Akademik fail-fast kaldırıldı. Tekli/batch
akademik ret artık400 Problem Details, title='Kayıt koşulları sağlanmadı.' ve
bütün failures listesidir. Eski alan400/varlık404/mükerrer409, UTC ve başarılı
Location/yanıtlar korunur. Yeni POST /students/{id:int}/enrollments/eligibility
batch doğrulamasını paylaşır, geçerli değerlendirmede200/isEligible/failures
verir; hiçbir kayıt/rezervasyon yazmaz. Gerçek kayıt yeniden değerlendirir.

**Migration:** 20261029122117_AddCourseOfferingTimeSlots Up/Down/snapshot ve
üretilen SQL incelendi: yalnız yeni tablo, sequence/default, PK/FK/CHECK/unique
index ve EF history. Mevcut tablolar yeniden oluşturulmadı. Uygulama süreci
yokken yalnız university_registration_app hedefine açık `dotnet ef database
update` uygulandı. İkinci update no-op; has-pending-model-changes temiz. Gerçek
metadata verify.sql/verify-time-slots.sql geçti. Mevcut app sayıları2 ders,
2 dönem,3 açılış; diğer tablolar0, yeni saat tablosu0. Varsayılan seed saat atamaz.

**Gerçek çalıştırılanlar:**

- Restore/build başarılı (0 uyarı/0 hata); SDK10.0.401, EF10.0.14,
  LocalDB17.0.4025.3 aynı kaldı, paket/framework eklenmedi.
- unit: saf saat sınırı/simetri/geçersiz gün/eşit-ters saat, üç ret türü birlikte,
  belirli sıra/çift başına tek ret ve savunmacı koleksiyonlar; önceki saf kontroller.
- eligibility ilk çalışma `university_registration_tests_day6_d0e7cb7d323e4ca396193d7a8aacebfd`:
  gerçek HTTP üçlü ret, fresh-context yazmama, yeni–yeni ve yeni–mevcut,
  bitiş=başlangıç kabul, farklı gün/dönem ve boş program, preview sonrası
  program değişikliğiyle kayıt reddi. Gerçek PK/FK/CHECK/UNIQUE/NOT NULL ve
  time(7)100ns/min/max gidiş-dönüşü geçti.
- specifications `university_registration_tests_day6_67de9d6c15ae438d9fa62127ad990c59`:
  önceki başarı/ön koşul/Int64 kredi/term düzeni/ilişki kısıt kontrolleri
  yeni failures sözleşmesiyle geçti; geçersiz startup yapılandırmaları da geçti.
- core `university_registration_tests_day10_e9ff0fbcebfb4513bc8add08a290a3aa`:
  başarılı kayıt/olmayan açılış/mükerrer tek satır ve temizliği geçti.
- full `university_registration_tests_day6_0c43daf37c794b19a4d2b6d90760cfef`:
  önceki ders/katalog/dönem/açılış/öğrenci/kayıt, priority DB bağımsızlığı,
  factory deneyleri, tracking ve transaction A–D, gerçek FK/unique hata
  dalları, 50'li batch ve ilk INSERT sonrası gerçek hata rollback'i geçti.
  Rakip context'in kesinleşmiş kaydı korundu. Akademik/eligibility kontrolleri,
  seed tekrarı ve rollback saat örneğinin iki çalışması da geçti.
- Son eklenen öğrenci izolasyonu, eski–eski çakışmayı dışlama ve saat FK'sinin
  NO ACTION silme deneyleriyle eligibility tekrar çalıştı:
  `university_registration_tests_day6_c87deb48d3c64c18ab7f02fa49970c60`, başarılı.
- Interceptor: önizlemede yalnız8SELECT, izlenen entity/yazma yok; iki saat
  sorgusu WHERE ve öğrenci/dönem JOIN'leriyle daraltılmış. Tekli ve50'li batch
  yine8SELECT; N+1 yok. SUM(bigint) korundu. Bir çiftin birden fazla saati tek
  schedule_conflict üretti. Sabit INSERT komut sayısı iddiası yok.
- `unit -ProbeFailure` kontrollü assertion ile üst çalıştırıcıya exit1 taşıdı;
  bu bilerek başarısız kontrol, ürün testi başarısı sayılmadı; bozuk test kalmadı.

**Temizlik/koruma:** Yeni fixture saatleri ilişkilerden/üst kayıtlardan önce
kimlik bazlı finally temizlendi. Bağımsız modlarda sekiz tablo boş, full sonu
yalnız seed2/2/3, diğerleri0 olarak ayrıca SQL'den okundu. Test DB şemaları tutuldu,
sequence sıfırlanmadı. Geçici API süreç sayısı0. Lab'a mutasyon yapılmadı;
önce/sonra üç tablonun sayıları ve veri checksum'ları aynı, saat tablosu yok.
Bu checksum karşılaştırması kriptografik bütünlük kanıtı olarak sunulmaz.

**Sınırlar/eksikler:** Engel yok. Down uygulanmadı, geri alma doğrulandı denmez.
Cleanup arızası zorlanmadı. Eşzamanlı kredi/program değişikliğine kesin garanti
verilmez; transaction/lock/retry eklenmedi. Haftalık yerel aynı-gün aralıklar;
tatil/timezone/dolaylı ön koşul/kontenjan/yetkilendirme yok. Boş saatler eksiksiz
program anlamına gelmez. Course.Credits güncel değeridir. Önizleme rezervasyon
veya kabul garantisi değildir. Başarılı testler yalnız sınanan davranışları kapsar.

**Sıradaki gün:** State; enum/koşul alternatifi;
taslak/gönderilmiş/onaylanmış/reddedilmiş geçişler.

**Commit/push:** Bu kaydı içeren commit `git log --format=%H --grep="^feat: explain enrollment eligibility and schedule conflicts$"`
ile bulunur. Push sonucu son raporda bildirilir; hash için ikinci commit yoktur.

## Gün 16 — Kontrollü Enrollment yaşam döngüsü ve State alternatifi

**Kavram/değişiklik:** EnrollmentStatus Draft0/Submitted1/Approved2/Rejected3;
private setter, kontrollü TrySubmit/TryApprove/TryReject ve nullable gerekçe.
İzinli üç geçiş dışında aynı işlemin tekrarı dahil ret; geçersiz gerekçe/geçiş
alanları değiştirmez. Gerekçe Trim sonrası1–500. Entity yalnız yerel geçişi
korur; akademik uygunluk EnrollmentOperations, HTTP eşlemesi endpoint'tedir.
Üretim enum+metot olarak kaldı; dört private durum nesnesiyle State yalnız
scripts/EnrollmentStateExperiment.cs deneyidir. Ortak matris iki yaklaşımı
aynı assertion'larla sınar; DI/framework/paket eklenmedi.

**Sözleşme/anlam değişikliği:** Yeni tekli/batch kayıtlar Draft. Tekli, batch
items, detay, öğrenci kayıt listesi ve yeni submit/approve/reject yanıtlarına
string status ve nullable rejectionReason eklendi. Başarı oluşturma201/Location,
geçiş200; yok404, yasak/tekrar409, akademik400/failures. Reject önce gerekçeyi,
sonra varlığı/geçişi kontrol eder; akademik değerlendirme yapmaz. Öğrenci–açılış
unique tüm durumlarda, Rejected dahil geçerlidir; yeniden açma yoktur.

**Yük:** Sadece Submitted/Approved kredi ve saat yüküne katılır. EvaluateAsync
aynı filtrelenmiş IQueryable'ı SUM(bigint) ve saat JOIN'lerinde kullanır.
Submit/approve private yoldan yalnız hedef Enrollment.Id'yi dışlar, aday olarak
bir kez değerlendirir; diğer aktif kayıtlar kalır. Exclude kimliği HTTP girdisi
olmadı. Önizleme/yeni kayıt aynı politikayı kullanır. Batch adayları kendi
aralarında hâlâ birlikte değerlendirilir; tek SaveChanges atomikliği korunur.

**Migration:** 20261029131736_AddEnrollmentLifecycle Up/Down/SQL/snapshot
incelendi. İki kolon, required int Status default0, nullable nvarchar(500)
gerekçe, eski satırlara UPDATE Status=2, iki CHECK. Geçmiş migration değişmedi,
tablo/kimlik/FK/unique korunur. SQL NULL mantığı ve Unicode whitespace açık
kontrol edilir. Eski Approved değeri önceki doğrudan kayıt anlamını taşır;
geçmiş onay/audit üretmez. Yalnız university_registration_app açık komutla
yükseltildi; ikinci update no-op, model/snapshot ve pending kontrolleri temiz.
App başlangıcında Enrollment0 idi; gerçek eski satır dönüşümü izole DB'de
ayrıca sınandı, app üzerinde sahte kullanıcı verisi oluşturulmadı.

**Gerçek kontroller:**

- Restore/build başarılı,0 uyarı/0 hata. SDK10.0.401, EF/yerel araç10.0.14,
  LocalDB17.0.4025.3 korundu. İlk test derlemesindeki yerel değişken adı çakışması
  düzeltildi; sonraki derlemeler ve tüm ilgili çalışmalar başarılı.
- state: `university_registration_tests_day6_08d8acb70b4a4f4ab7b5ab96a7dcc7f1`.
  Önce15.gün şeması, eski kolonlarla gerçek Enrollment; sonra migration ile
  Approved. Aynı Id/FK/zaman korundu, private setter yeni context'te yüklendi;
  tekrar migration no-op. Yeni HTTP satırları Draft; submit/approve/reject,
  akademik retlerde değişmeme, yasak/tekrar409, gerekçe alan400, yok404, restart,
  SQL status/reason/NULL/uzunluk ve enum/State ortak matris geçti.
- full: `university_registration_tests_day6_94c3be5c853342039f9d9015ce98e3ea`.
  Yukarıdakiler ve ek Submitted kredi / Rejected yükten çıkarma kontrolleri,
  Draft yük hariç, kendi24kredi/saatini iki kez saymayan approve, başka aktif
  kayıtları koruyan approve reddi, submit+approve'da değişen ön koşulun yeniden
  değerlendirilmesi, Draft batch iç kredi/saat retleri geçti. Rejected çift409,
  tek satır kaldı. Gerekçesiz diğer durumlar ve geçersiz durumlar DB'de reddedildi;
  SQL CHECK'in geçiş geçmişini bilmediği doğrudan Draft→Approved ile gösterildi.
- Aynı full çalışmasında eski ders/katalog/dönem/açılış/öğrenci/kayıt,
  priority DB bağımsızlığı, factory, tracking, transaction A–D,23 SQL kısıt
  deneyleri ve seed tekrarı geçti. Gerçek FK/unique kayıt anı dalları çalıştı.
  Batch ilk INSERT sonrası gerçek unique hatası kendi yazılarını geri aldı,
  rakibin kesinleşmiş kaydı kaldı; FK hataları da tüm batch'i geri aldı.
- Bağımsız unit geçti. core:
  `university_registration_tests_day10_60a3dff0abae4331b15fc60f7776b69f`;
  specifications: `university_registration_tests_day6_531b2326549946b9a238a801aed38c20`;
  eligibility: `university_registration_tests_day6_604e93f3477045db99b8f3c10c2c4f66`;
  hepsi başarılı, sonunda sekiz tablo boş/model uyumlu. Eski yük fixture'ları
  HTTP submit veya yalnız fixture'da açık Approved ile hazırlandı; kurallar
  kapatılmadı, limit30 korundu.
- Gerçek SQL interceptor: hazırlık sekiz toplu SELECT, durum filtreleri yük/saat
  sorgularında; tekli/50'li batch sayısı satır başına büyümedi. INSERT batching
  komut sayısı sabit varsayılmadı. Güncel metadata verify.sql ve yeni lifecycle
  kontrolü app/test DB'de geçti. `unit -ProbeFailure` bilerek assertion üretti;
  üst çalıştırıcı exit1 verdi, bozuk test bırakılmadı.

**Koruma/temizlik:** App ve full-test sonunda2 ders/2 dönem/3 açılış, kalan
beş tablo0. Fixture kayıtları finally içinde kimlikleriyle FK sırasına uygun
silindi. Test DB şemaları tutuldu; sequence sıfırlanmadı. Geçici API süreç0.
Lab'a yazılmadı; önce/sonra üç tablonun satır sayıları/checksum'ları aynı,
Enrollments tablosu yok. Checksum kriptografik veri koruma kanıtı değildir.
Sandbox içindeki LocalDB/süreç okuma izin sınırı uygun yetkili araç çağrısıyla
çözüldü; gerçek DB kontrolleri engellenmiş sayılmadı veya atlanmadı.

**Sınırlar/eksikler:** Engel yok. Down ve cleanup arızası deneyi çalıştırılmadı.
Yetkilendirme/danışman ataması yok; onay/ret geliştirme akışıdır. Eşzamanlı
geçişlerde son yazan kazanabilir; kredi/program garantisi ve kontenjan yok.
Rejected yeniden açma, audit/event/outbox/bildirim, genel state-machine ve
Decorator altyapısı eklenmedi. DB CHECK satır tutarlılığı sağlar, geçmiş geçişi
ve yetkiyi sağlamaz. Başarılı kontroller yalnız sınanan davranışları kanıtlar.

**Sıradaki gün:** Decorator ve davranış sırası; kayıt değerlendirmesine süre ölçümü.

**Commit/push:** Kaydı içeren kimlik `git log --format=%H --grep="^feat: add controlled enrollment state transitions$"`
ile bulunur. Push sonucu son raporda; hash için ikinci commit üretilmez.

## Gün 17 — Decorator ve davranış sırası

**Üretim:** `IAcademicEligibilityEvaluator` gerçek hazırlanmış giriş/sonuç
sözleşmesini sunar. AcademicEligibility statik yerine saf somut uygulama oldu;
kurallar değişmedi. TimingAcademicEligibilityEvaluator her çağrıda yerel Stopwatch,
tek iç çağrı ve finally ile Event1701/ElapsedMs/Outcome/nullable FailureCount
üretir. Program scoped somut + sarmalanmış interface kaydını açık kurar.
EnrollmentOperations yalnız DB hazırlığı sonrasında interface'i çağırır.

**Deney/test:** DecoratorChecks aynı sonuç referansı, tek çağrı, aynı exception ve
orijinal stack, log alanları ve gerçek evaluator ret sırasını sınar. Üretim dışı
async deney TCS ile bekleme/sonuç/hata/iptali; sıra deneyi iki iç içe düzenin başarı
ve finally hata yolunu gösterir. Yeni paket/framework yoktur. `decorator` modu
mevcut EligibilityChecks/StateChecks senaryolarını kopyalamadan kullanır;
EvaluationLogObserver gerçek API JSON console olaylarını bellekten okur.
Eski doğrudan işlem/fault kontrolleri de test yardımcı timing evaluator'ına bağlandı.

**Gerçek sonuçlar (30 Ekim 2026):**
- SDK10.0.401/net10.0; paketler değişmedi. Restore başarılı, build0 uyarı/0 hata.
  İlk test derlemesinde test logger sınıfı/metot adı çakışması düzeltildi;
  sonraki unit ve full derlemeleri/kontrolleri başarılıdır.
- `unit`: sonuç kimliği, eligible/ineligible/failed alanları, nullable failure
  count, tek kayıt, aynı exception/stack; async success/fault/cancel ve iki sıra
  başarılı. Önceki saf kurallar/pattern/durum kontrolleri de geçti.
- `decorator`: yeni izole LocalDB üzerinde gerçek DI + HTTP; preview, tekli/batch
  oluşturma, submit/approve yollarında50 hesaplama olayı doğrulandı. Uygun/uygunsuz
  outcome ve failure sayısı doğru; erken retlerde olay yok; HTTP'de elapsedMs yok.
  Eligibility ret içeriği/sırası ve state/restart davranışları korundu.
- `core`, `specifications`, `eligibility`, `state`, `full` ayrı çalıştırıldı ve
  geçti. Full gerçek unique/FK yarış dalları, ilk INSERT sonrası batch rollback
  ve başka context'in kesinleşmiş rakip satırının korunmasını yeniden doğruladı.
  Öncelik preview DB bağımsızlığı, katalog SQL/tracking, transaction A/B/C/D,
  seed tekrarı ve üç liste endpoint'i regresyonları da geçti.
- Gerçek SQL interceptor kontrolleri sarmalanmış evaluator kullanan işlemde
  sekiz toplu hazırlık SELECT'i/no write; sorgu sayısı satır başına artmadı.
- `dotnet ef migrations has-pending-model-changes`: fark yok. İzole testlerde
  pending migration yok. Yeni migration oluşturulmadı; app/lab'a yazılmadı.
- `unit -ProbeFailure`: beklenen kontrollü assertion, üst çalıştırıcı exit1;
  kalıcı bozuk test yok. Test fixture'ları finally ile FK sırasıyla temizlendi.
  Küçük modlarda sekiz tablo boş, full sonunda yalnız2 ders/2 dönem/3 açılış seed'i
  ve diğer tablolarda0 satır kaldı. Geçici API süreç sayısı0; DB şemaları tutuldu,
  sequence sıfırlanmadı. Log/SQL çıktıları repoya eklenmedi.

**Sınırlar/eksikler:** Engel yok. Ölçüm sadece hazırlanmış akademik hesaplama;
DB hazırlığı, SaveChanges, HTTP ve toplam istek süresi değil. SaveChanges daha
sonra başarısız olsa bile başarılı hesaplamanın outcome'u eligible kalabilir.
Bu benchmark veya SLA değildir. Log sağlayıcısı arızası ve temizlik arızası
simülasyonu yapılmadı; logger exception'ı sonucu/asıl hatayı gölgeleyebilir.
Üretime yapay hesaplama hatası endpoint'i eklenmedi; failed dalı saf testte gerçek
exception ile sınandı. Down çalıştırılmadı (bu gün migration yok).
Mevcut yetki, kontenjan ve eşzamanlı kredi/program/geçiş sınırları değişmedi.

**Sıradaki gün:** Adapter; iki sahte bildirim sağlayıcısını ortak sözleşmeye uyarlama.

**Commit/push:** Kaydı içeren commit Git geçmişinden
`git log --format=%H --grep="^feat: measure eligibility with a timing decorator$"`
ile bulunur. Push sonucu son raporda verilir; kendi hash'i için ek commit yoktur.
