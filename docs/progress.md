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

## Gün 18 — İki sahte bildirim sağlayıcısı için Adapter

**Başlangıç kanıtı:** Çalışma ağacı temiz, branch main, origin beklenen GitHub
reposuydu. İncelenen HEAD `4e0f4a9140b3a76fafb3a1c05bf563cdbfe697e5`;
17. gün decorator kodu ve belgeleri dosyalarda ve Git geçmişinde mevcut. Kullanıcının
raporladığı eski commit güncel varsayılmadı; önceki çalışma değiştirilmedi.

**Katkı:** `scripts/NotificationAdapterDemo.cs` kontrollü NotificationMessage,
üç durumlu NotificationSendResult, INotificationSender, iki Fake sağlayıcı ve
iki adapter içerir. Aynı NotificationDemoCaller yalnız ortak sözleşmeyi kullanır.
Mail ayrı alan/typed exception, Publish kendi request/error-code protokolüyle
çalışır. Alanlar/referans korunur, hata ve iptal anlamları ayrılır. Bilinmeyen veya
çelişkili Publish cevabı exception'dır. Sahte davranış delegate ile ayarlanır;
alıcı adresindeki sihirli kelimelere dayanmaz.

**Gerçek kontroller:** SDK10.0.401, mevcut paket sürümleri korundu. Restore/build
başarılı; build0 uyarı/0 hata. Adapter modu DB/ağ olmadan geçti; derlenmiş kontrol
programı restore olmadan tekrar çalıştırıldı ve geçti. Ortak harness iki adapter
üzerinde kabul/alıcı reddi/kesinti, kayıpsız alan/referans, aynı token, tek çağrı,
null mesaj, beklenmeyen aynı exception ve canceled Task anlamını doğruladı.
TCS ile sonradan tamamlanan bilinen Mail exception'ları await/catch'e ulaştı.
Publish bilinmeyen kod, accepted+hata, başarısız+kodsuz, başarısız+referans,
null cevap ve boş referans kombinasyonları reddedildi; referanssız kabul desteklendi.
Web assembly'sinde deney türlerinin bulunmadığı kontrol edildi.

`unit`, `core`, `full` çalıştırıldı ve başarılı. Önceki decorator/async/sıra,
Strategy/Factory/State, saf akademik kurallar ve gerçek LocalDB/HTTP regresyonları
korundu. Full gerçek unique/FK, batch ilk INSERT sonrası rollback ve kesinleşmiş
rakip satırın korunmasını, lifecycle/restart, katalog/SQL/tracking/transaction,
seed tekrarı ve mevcut liste sözleşmelerini yeniden doğruladı. Yeni ürün davranışı
ve migration yok. `dotnet ef migrations has-pending-model-changes`: fark yok.
`adapter -ProbeFailure` beklenen assertion üretti; üst çalıştırıcı exit1 verdi.

**İzolasyon:** Adapter deneyi veri/süreç/ağ oluşturmaz. Core/full mevcut yeni
izole LocalDB yaklaşımını kullandı; app/lab'a yazılmadı. Finally temizliği sonrası
core sekiz tablo boş; full2 ders/2 dönem/3 açılış seed'i, diğer beş tabloda0 satır.
Geçici API süreç sayısı0. Log/test çıktısı ve DB dosyası repoya eklenmedi.

**Sınırlar/eksikler:** Engel yok. Gerçek sağlayıcı uyumluluğu veya teslim testi
çalıştırılmadı ve iddia edilmiyor. Accepted teslim/okunma değildir. Kesinti güvenli
retry anlamına gelmez; timeout/yanıt kaybı ve DB/dış gönderim atomikliği çözülmez.
Onay/ret/kayıt bağlantısı, HTTP endpoint'i, kalıcı bildirim, retry/fallback,
queue/Outbox/Inbox, yeni paket veya Facade eklenmedi. Önceki günün ayrı decorator
HTTP-log modu bugün ayrıca çalıştırılmadı; saf decorator ve full regresyonları
çalıştırıldı. Temizlik arızası/Down deneyi yok (bugün migration da yok).

**Sıradaki gün:** Facade; dönem kayıt özetini birleştiren işlem.

**Commit/push:** Bu kaydı içeren kimlik Git geçmişindeki
`feat: adapt demo notification providers` commit'inden bulunur. Push sonucu son
raporda verilir; hash yazmak için ek commit oluşturulmaz.

## Gün 19 — Dönem kayıt özeti Facade

**Başlangıç:** main/origin doğru, çalışma ağacı temiz. Gerçek HEAD
`21a86cb0b77132254d2de71914a4333331b7f432`; önceki decorator ve adapter commit'leri
Git geçmişinde ve dosyalarda görüldü, korundu. Kullanıcının eski commit'i güncel
varsayılmadı;17–18. günlere ait yeni bir sonuç uydurulmadı.

**Katkı:** Scoped TermRegistrationSummaryFacade öğrenci→dönem varlık kontrolünü,
hedef kayıt projection'ını ve aktif saatleri tek HTTP-bağımsız işlemde toplar.
Found/StudentMissing/TermMissing küçük sonuç türleri; endpoint alan bazlı400,
sıralı404 veya200 üretir. DTO'lar bütün durumları, durum sayıları, checked long
aktif kredi, aktif program ve saatsiz aktif kimlikleri taşır. Submitted/Approved
aktif anlamı korunur. Her enrollment tek satır; saat JOIN'i toplamı çoğaltmaz.
Güncel kredi kullanılır, şubeler ayrı sayılır. Pazartesi başlangıçlı sıra ve
TimeOnly/time(7) hassasiyeti korunur. İç HTTP, Add/Update/SaveChanges, yeni kural,
interface zinciri, paket veya migration eklenmedi.

**Gerçek doğrulamalar:** SDK10.0.401; restore/build başarılı,0 uyarı/0 hata.
İlk summary denemeleri testin JsonNode.GetValue<TimeOnly> dönüşümünde durdu;
üretim serileştirmesi değiştirilmeden test Deserialize<TimeOnly> kullanacak
şekilde düzeltildi. Başarısız denemelerde fixture/süreç temizliği çalıştı.
Sonraki summary kontrolü ve full içindeki tekrar başarılı:
-3/4/5/6 kredili Draft/Submitted/Approved/Rejected fixture'ı: aktif kredi9,
 her durum1, dört doğru kayıt. Başka öğrenci/dönem hariç; çok saatli dersin kredisi
 bir kez. Pasif saatler hariç, saatsiz aktif kimlikler artan.
- Pazartesi→Pazar, başlangıç/bitiş/açılış kimliği sıra kontrolleri ve
 09:00:00.1234567 değerinin gerçek HTTP JSON tick dönüşü başarılı.
- Boş/pasif sonuç,200/400/404, öğrenci-önce yokluk, alan hataları, tekrarlı GET
 ve bütün fixture kayıtlarının yeni context'te değişmediği doğrulandı.
- Gerçek SQL interceptor4 ve24 kayıt için4 SELECT; boş ve yalnız pasif yolda3.
 Filtre/projection/SQL sıra mevcut, entity tracker boş. Aynı dersin farklı
 şubeleri ayrı sayıldı; Int32 üstü toplam doğru long olarak döndü.
- `unit`, `core`, `full` başarılı. Önceki adapter/decorator saf deneyleri,
 akademik/state/eligibility HTTP, gerçek unique/FK ve ilk INSERT sonrası batch
 rollback/rakip kayıt korunması, katalog/tracking/transaction ve seed tekrarı
 regresyonları geçti. Ürün sözleşmeleri yeni endpoint dışında değişmedi.
- `dotnet ef migrations has-pending-model-changes`: fark yok. İzole testlerde
 migration/model uyumu kontrol edildi; uygulama DB'sine migration uygulanmadı.
- `summary -ProbeFailure`: beklenen assertion sonrası finally temizliği ve
 süreç kapatması; üst çalıştırıcı exit1. Sonradan ayrı SQL bağlantısıyla probe
 DB'sinin sekiz tablosu boş doğrulandı. Full DB'sinde yalnız2 ders/2 dönem/3 açılış
 seed'i ve diğer beş tabloda0 satır kaldı. Geçici API süreç sayısı0.

**Sınırlar/eksikler:** Engel yok. Özet birkaç sorgudur; tüm yanıt için tek snapshot
 veya eşzamanlılık garantisi yoktur. Sayılar/kredi aynı projection'dan; program
 sonraki sorgudan gelir. Saat yokluğu eksiksiz program/uygunluk garantisi değildir.
 Yetkilendirme, rezervasyon, çakışma raporu ve otomatik düzeltme yoktur. App/lab'a
 dokunulmadı; test DB şemaları tutuldu. Down/temizlik arızası deneyi çalıştırılmadı.
 Ayrı decorator HTTP-log modu bugün yeniden çalıştırılmadı; unit/full deney ve
 regresyonları çalıştırıldı. Yeni Builder veya genel raporlama altyapısı yoktur.

**Sıradaki gün:** Builder; constructor/object initializer alternatifleri;
karmaşık program test verileri.

**Commit/push:** Bu kaydı içeren kimlik Git geçmişindeki
`feat: add term registration summary facade` commit'inden bulunur. Push sonucu
son raporda; hash yazmak için ek commit oluşturulmaz.

## Gün 20 — Okunabilir uygunluk senaryoları için Builder

**İnceleme:** main/origin doğru, çalışma ağacı temizdi. Başlangıç HEAD
`4c14a163df03281726a9daba2122adf5d5268e7e`; 17–19. gün commit/dosyaları mevcut
Git geçmişinden görüldü ve korundu. Önceki kullanıcı commit'i güncel varsayılmadı.

**Katkı:** Yalnız scripts içinde EligibilityScenarioBuilder ve bağımsız sonuç
kabı eklendi. Gerçek OfferingEligibility/OfferingSchedule/PrerequisiteInput
kullanılır; öğrenci entity'si veya paralel akademik model yok. Tek öğrencinin
hazırlanmış bağlamı, açık dönem/kimlik/kredi/status/saat/ön koşul/tamamlanan ders ve
zorunlu limit ile kurulur. Build aktif mevcut yükü hazırlar, uygunluk hesaplamaz;
Draft/Rejected durumunu değiştirmez, aktif girdiye dahil etmez.

**Temsilî refactor:** EligibilityChecks.Unit'in birleşik yeni–mevcut/yeni–yeni
çakışma, ön koşul+kredi+saat ve dönem ayrımı fixture'ı builder'a geçti. Beklenen
ret listesi, sıra ve kimlik assertion'ları aynen korundu. DecoratorChecks'in gerçek
saf/decorated eşdeğerlik girdileri de aynı hazırlık biçimini kullanır. Basit kredi
sınırı ve tek slot constructor kontrolleri, tüm DB fixture'ları korundu. Yeni kopya
akademik test algoritması yazılmadı. Kazanç ilişkilerin/önemli değerlerin görünmesi;
maliyet küçük hazırlık API'si ve kopyalama kontrolleri, daha az satır garantisi yok.

**Sözleşme:** Her Build bağımsız, reset yok. Dış koleksiyonlar ekleme anında
kopyalanır; nested schedule ve prerequisite değerleri her Build'de yeniden kurulur.
Salt okunur listeler/kredi sözlüğü, immutable slotlar ve FrozenSet kullanılır.
Sonraki builder/dış liste değişimleri önceki sonucu bozmaz. Yapısal pozitif
kimlik/kredi/limit, geçerli enum/aralık, tanımlı hedef ve tekrarsız tanım koşulları
vardır. Birebir saat tekrarı açık hata; farklı geçerli örtüşmeler, eksik ön koşul
ve kredi aşımı düzeltilmeden kalır. Rastgele kimlik/zaman, expected failure üretimi,
DB/HTTP/SaveChanges/temizlik veya thread-safety altyapısı yoktur.

**Gerçek sonuçlar (30 Ekim 2026):**
- SDK 10.0.401 ile restore/build başarılı, 0 uyarı/0 hata; paket değişmedi.
- `builder` ve refactor edilmiş saf uygunluk kontrolleri başarılı. İki Build,
  sonradan builder değişimi, dış koleksiyonun Build öncesi/sonrası değişimi,
  nested slot/küme/dictionary bağımsızlığı, readonly mutasyon reddi, ayrı builder,
  istek sırası, yapısal hatalar ve uygunsuz verinin korunması doğrulandı.
  Builder türünün web assembly'sinde bulunmadığı kontrol edildi.
- `unit`, `eligibility`, `specifications`, `core`, `full` çalıştırıldı ve geçti.
  Gerçek LocalDB/HTTP akademik ret sırası, status/load, summary, unique/FK,
  ilk INSERT sonrası batch rollback ve kesinleşmiş rakip satırının korunması,
  seed/katalog/tracking/transaction kontrolleri korundu. Önceki adapter ve
  decorator deneyleri unit/full içinde yeniden geçti.
- `dotnet ef migrations has-pending-model-changes`: fark yok. Migration yok.
  Git diff ile üretim source/project/model dosyalarının değişmediği doğrulandı.
- `builder -ProbeFailure` beklenen assertion üretti; üst çalıştırıcı exit1.
  Üretimde ve normal testlerde kalıcı bozuk kontrol bırakılmadı.
- Regresyonlar izole LocalDB kullandı; app/lab'a yazılmadı. Küçük DB modlarında
  sekiz tablo boş; full sonunda yalnız 2 ders/2 dönem/3 açılış seed'i, diğer beş
  tabloda 0 satır doğrulandı. Geçici API süreç sayısı 0; finally temizliği korundu.
  Test DB şemaları tutuldu, sequence sıfırlanmadı. Çıktı/log/DB dosyası commit edilmedi.

**Sınırlar/eksikler:** Engel yok. Bellek içi builder DB/SQL veya transaction
kanıtı değildir; bunlar ayrı gerçek regresyonlarla sınandı. Thread-safe/paylaşılan
builder yok, her test yeni hazırlık kullanır. Down/temizlik arızası simülasyonu
çalıştırılmadı (bu gün migration yok). Ayrı decorator HTTP-log modu yeniden
çalıştırılmadı; saf deneyleri ve full regresyonlar çalıştırıldı. Genel entity
builder/Director/clone, yeni ürün özelliği ve Observer eklenmedi.

**Sıradaki gün:** Observer, domain event ve integration event; kayıt onayına
uygulama içi tepki ve teslim garantisi sınırları.

**Commit/push:** Bu kaydı içeren kimlik Git geçmişindeki
`test: build readable eligibility scenarios` commit'inden bulunur. Push sonucu
son raporda verilir; hash için ikinci commit üretilmez.

## Gün 21 — Observer, domain event ve integration event

**Başlangıç:** main temizdi; gerçek HEAD `074c64b` (Gün20) idi. 17–20 dosyaları ve
Git geçmişi incelendi; eski kullanıcı commit'i güncel kabul edilmedi. Önceki kayıtlar
korundu, bugünkü değişikliklere ilgisiz dosya katılmadı.

**Katkı:** Immutable EnrollmentApproved, küçük observer sözleşmesi, scoped sıralı
publisher ve ApprovalLogObserver. Onay koordinasyonu bağımsız başarılı SaveChanges
sonrasında olay üretip dağıtımı await eder. Dış transaction kabul edilmez; submit,
reject, create ve retler olay üretmez. Observer arızası güvenli kimlik/tür alanlarıyla
loglanır ve sonraki gözlemci çalışır. Çağıran iptali kalan dağıtımı durdurur;
kesinleşmiş Approved sonucu korunur. Normal HTTP sözleşmesi/şema değişmedi.

**Gerçek kontroller (2026-10-30):**
- Restore başarılı; build 0 hata/0 uyarı. İlk derlemede eksik transaction extension
  namespace'i ve testte HttpResponseMessage için await using hatası düzeltildi.
- unit: mevcut saf kontroller ve yeni sıfır/tek/çoklu observer, TCS sıralı await,
  aynı olay/token, null, immutable veri, güvenli log, beklenmeyen hata ve
  çağıran/çağıran olmayan OCE ayrımı geçti.
- observer: gerçek Kestrel/endpoint/test DI, yeni context Approved okuması,
  kimlik/UTC zaman aralığı, 200/409/404/akademik400, gerçek SQL CHECK UPDATE500
  ve olay çıkmaması; observer hatasında200; commit sonrası kontrollü iptalde
  Approved korunması/kalan observer'ın başlamaması; dış transaction reddi geçti.
- Üretim log observer'ıyla onay SELECT sayısı önce/sonra7; create/submit/reject
  olay üretmedi. Son GET Approved; başarısız onaylar Submitted olarak kaldı.
- core: başarılı kayıt/olmayan açılış/mükerrer kayıt ve temizliği geçti.
- full: state/eligibility/specifications, katalog/tracking/transaction deneyleri,
  gerçek unique/FK kayıt anı dalları, batch ilk INSERT sonrası rollback ve rakip
  kaydı koruma, summary, önceki saf decorator/adapter/builder/factory kontrolleri
  geçti. Mevcut endpoint/seed regresyonları geçti. Yeni observer kontrolleri full'e
  de dahil edildi; önceki testler kopyalanmadı, yalnız constructor bağlantısı güncellendi.
- `-Mode observer -ProbeFailure`: kontrollü assertion üst runner'da exit1; kalıcı
  bozuk test yok. `dotnet ef migrations has-pending-model-changes`: fark yok.
- Son observer DB `university_registration_tests_day6_976c8d0aa9ce46c7bc69fe91c76df16f`:
  sekiz test tablosu boş, pending migration/model farkı yok; geçici API/host kapalı.
  Full DB `university_registration_tests_day6_bb8e681e92354d05aeb35d2687841195`:
  yalnız temel seed bırakıldı; test satırları temizlendi. Core DB de boş doğrulandı.
  Süreç envanterinde bu çalışmanın API/check süreçleri kalmadı. App/lab hedeflenmedi;
  migration üretilmedi. Log/test çıktısı veya bağlantı bilgisi repoya eklenmedi.

**Sınırlar/eksikler:** Engel yok. Kalıcı teslim, retry/dedup/exactly-once ve eşzamanlı
onay garantisi yok. Commit/yayın arası çökme ve kısmi dağıtım kaybı açık; çökme-kurtarma,
logger arızası ve temizlik arızası simülasyonu yapılmadı. Logger arızası akışı bozabilir.
Dış transaction koruması açık EF transaction ile sınandı; ambient/enlisted ayrı
deney yapılmadı. Commit sonrası iptal uygulama/integration sınırında kontrollü sınandı;
kopmuş HTTP bağlantısına yanıt teslimi iddia edilmez. Down uygulanmadı (yeni migration
zaten yok). Integration event/broker ve gerçek bildirim yok; DB transaction'ı dış
etkiyi atomik yapmaz. Sonraki Outbox/queue konularının garantileri önceden kurulmadı.

**Sıradaki gün:** Repository/UoW, DbContext ilişkisi ve generic repository maliyeti.

**Commit/push:** Bu kaydı içeren kimlik Git geçmişindeki
`feat: observe committed enrollment approvals` commit'inden bulunur. Push sonucu
son raporda verilir; kendi hash'i için ek commit oluşturulmaz.

## Gün 22 — Repository/UoW ve DbContext sınırları

**Başlangıç:** main temiz; gerçek HEAD `39cb331` (Gün21). Git geçmişi ve 17–21
çalışmaları incelendi, eski raporlanan commit güncel varsayılmadı. AGENTS.md bulunmadı.

**Üretim kararı:** Doğrudan scoped UniversityDbContext korundu. EnrollmentOperations
zaten toplu veri hazırlığını ortaklaştırıyor; tracked geçiş sorguları ile katalog/
summary projection'ları aynı anlamı taşımıyor. Dar Find/Add üretim tüketicisini
anlamlı sadeleştirmediğinden repository/IUnitOfWork/interface eklenmedi. Hiçbir üretim
kaynağı, HTTP sözleşmesi, şema, batch tek SaveChanges veya onay olayı sınırı değişmedi.

**Deney:** EnrollmentRepositoryExperiment.cs yalnız test derlemesinde. Tracked
Find/null, Added yapan Add, çağıranın save/commit/dispose sahipliği açık.
RepositoryChecks aynı harness ile direct/repository yollarını karşılaştırır;
CatalogSqlCapture ve gerçek FK hata sınıflandırması yeniden kullanılır. İki küçük
bileşenin ortak context'e hazırladığı Student/Enrollment tek SaveChanges'e katılır.
Mevcut RelatedWriteChecks.Tracking B deneyinin ikinci yazması ayrı context'e alındı;
transaction testleri kopyalanmadı. Generic maliyetler öğrenme notunda açıklamalı
örneklerle, IQueryable yaşam süresi ise çalıştırılan kontrollü hata ile gösterildi.

**Gerçek sonuçlar (2026-10-30):**
- SDK10.0.401 ile restore/build başarılı; 0 hata/0 uyarı, paket yükseltme yok.
- repository: direct ve dar repository için bulunan/yok kayıt, beklenen context'te
  tracking, Added, save öncesi yok/save sonrası var, gerçek FK hatasında iki yeni
  kaydın da kalıcılaşmaması, cancellation ve disposed context hatası geçti.
- Gerçek yürütülen Find SQL'i parametreli WHERE içeriyor; ilişki JOIN'i/bütün tablo
  yüklemesi yok. Dar scalar projection SQL'de, gereksiz CreatedAtUtc/Reason sütunu yok.
- Ortak context'te iki bileşenin değişikliklerini tek save kalıcılaştırdı. Ayrı
  context'lerde ikinci CHECK hatası ilk save'i geri almadı (mevcut full deneyi).
- unit/core/full başarılı. Full: gerçek batch ilk INSERT sonrası unique rollback,
  rakip kesinleşmiş kayıt korunması, FK dalları; akademik/state/eligibility/katalog,
  summary ve önceki decorator/adapter/builder deneyleri geçti. Observer yeni
  context'te Approved okudu; yazma arızasında olay yok, observer arızasında200;
  onay SELECT sayısı önce/sonra7. Mevcut SQL sayfalama/projection ve seed tekrarı geçti.
- `repository -ProbeFailure` kontrollü assertion exit1; finally temizliği ve süreç
  kapatma çalıştı. Ayrı SQL bağlantısıyla bu DB'nin sekiz tablosunda toplam0 satır
  doğrulandı: `university_registration_tests_day6_4e866b7accb844688276087e4c42dec9`.
- Normal repository DB `university_registration_tests_day6_0d8a12ff8278406099c98fdf33032a00`
  ve core DB boş doğrulandı. Full DB `university_registration_tests_day6_2ac53102d9214fa586103cc754ddd83e`
  yalnız seed2 ders/2 dönem/3 açılış; kalan5 tablo0. Test şemaları tutuldu, sequence
  sıfırlanmadı. API/check süreç kalıntısı yok; app/lab hedeflenmedi.
- EF `has-pending-model-changes`: fark yok; repository/core pending migration kontrolü
  temiz. Yeni migration yok. Secret/SQL/log/test çıktısı commit edilmedi.

**Sınırlar/eksikler:** Engel yok. Gerçek altyapı bağlantı kesintisi ve temizlik
arızası simülasyonu yapılmadı; disposed context'in null'a çevrilmediği, iptal ve
gerçek FK/CHECK hataları sınandı. Generic GetAll/bellekte filtreleme anti-pattern'i
çalıştırılmadı, açıklamalı örnektir. Repository atomiklik/teknoloji bağımsızlığı veya
performans garantisi sağlamaz. Down uygulanmadı (yeni migration yok). Onay sonrası
commit/yayın boşluğu, kalıcı teslim ve eşzamanlı onay sınırları aynen geçerli.

**Sıradaki gün:** CQRS ve Mediator ayrımı; kayıt komutu/katalog sorgusu.

**Commit/push:** Bu kaydı içeren kimlik Git geçmişindeki
`refactor: evaluate enrollment persistence boundaries` commit'inden bulunur.
Push sonucu son raporda; hash için ek commit oluşturulmaz.

## Gün 23 — CQRS ve Mediator ayrımı

**Başlangıç:** main temiz, gerçek HEAD `98d7ddd` (Gün22). 17–22 commit'leri ve
progress/dosyaları bulundu; eski raporlanan commit güncel varsayılmadı. Eksik önceki
gün uygulaması yapılmadı. Önceki kayıtlar ve davranışlar korundu.

**Üretim:** CreateEnrollmentCommand get-only/pozitif kimliklerle oluşturulur;
EnrollmentOperations.CreateAsync mevcut koordinasyonun tek sahibi olarak komutu
kabul eder. HTTP request ayrıdır. RegistrationInput aynı komut Validate fonksiyonunu
kullanır; alan bazlı400 önceliği korunur. Doğrudan yanlış oluşturma ilgili parametre
ArgumentOutOfRangeException, null işlem girdisi ArgumentNullException'dır.
CourseCatalogQuery ve scoped CourseCatalogReader SQL akışını HTTP'den ayırır;
endpoint bağlama/alan hatası/200 yanıtını yönetir. Query constructor ve HTTP aynı
Validate fonksiyonunu kullanır. Aynı DB/doğrudan çağrı; Mediator üretimde yoktur.

**Deney:** MediatorExperiment iki typed istek/sonuç ve iki somut işleyici içerir;
CqrsChecks doğrudan/aracı çağrıyı, doğru handler/tek çağrı/aynı token ve sonucu,
TCS ile bekleyen task, aynı exception ve cancelled task, eksik handler hatasını
sınar. İki sabit typed slot duplicate registration'ı temsil etmez. Kaynaklar web
derlemesinden dışlanır; paket/reflection/pipeline eklenmedi.

**Gerçek sonuçlar (2026-10-30):**
- Restore/build başarılı, 0 hata/0 uyarı. SDK10.0.401 ve mevcut paketler korundu.
- cqrs ve unit: doğrudan geçersiz komut/query sınırları, ortak HTTP alan hataları,
  Trim/default/boş arama/güvenli offset/null ve Mediator deneyleri DB olmadan geçti.
- core: gerçek HTTP başarılı kayıt, olmayan açılış400, mükerrer409 ve DB doğrulamaları
  geçti. Full: Draft oluşturma, akademik failures sırası, durum geçişleri, unique/FK
  kayıt anı dalları; batch ilk INSERT sonrası rollback ve rakip kaydın korunması geçti.
- Mevcut CatalogChecks yeni reader'ı doğrudan çalıştıracak şekilde uyarlandı; ikinci
  katalog algoritması/test seti yazılmadı. Gerçek SQL'de parametreli WHERE, ayrı COUNT,
  Id ORDER BY/OFFSET-FETCH ve yalnız Id/Code/Name; toplam2 komut/boş tracker geçti.
  HTTP filtreler/AND/default/6 sayfa/toplam/özel karakter/collation/400/route/eskiliste
  sözleşmeleri geçti. Yeni context'te fixture kimlik/kod/ad/kredileri değişmemişti.
- Full ayrıca observer commit sonrası yeni context'te Approved, SQL yazma hatasında
  olay yok, observer hatasında200 ve commit sonrası iptal; onayda7 SELECT önce/sonra
  kontrollerini geçti. Summary, repository karşılaştırması, tracking/transaction,
  seed tekrarı ve önceki saf pattern deneyleri de yeniden çalıştı.
- cqrs -ProbeFailure beklenen assertion ile exit1 üretti; DB oluşturmadı. EF
  has-pending-model-changes fark yok. Yeni migration/şema değişikliği yok.
- Core DB `university_registration_tests_day10_60becbad57a5437db81b2ce9a3f2f8e3`
  boş doğrulandı. Full DB `university_registration_tests_day6_451e08273f2b4525ace98d4494efe9f2`
  sonunda yalnız2 ders/2 dönem/3 açılış seed'i, diğer5 tablo0 ayrıca SQL ile doğrulandı.
  API/check süreçleri kalmadı; test şemaları tutuldu, sequence sıfırlanmadı. App/lab
  hedeflenmedi. Secret/log/test çıktısı commit edilmedi.

**Sınırlar/eksikler:** Engel yok. Count/items tek snapshot değildir. Mevcut yetki,
kontenjan, eşzamanlı geçiş/kredi/program ve commit/yayın teslim sınırları değişmedi.
MediatR, genel bus, otomatik retry/transaction veya ayrı read DB eklenmedi.
Logger/temizlik arızası ve bağlantı kopması simülasyonu yapılmadı; Down uygulanmadı
(bugün migration yok). Önceki günlerin özel ProbeFailure ve decorator HTTP-log modu
bugün ayrıca tekrarlanmadı; aşağıdaki tablo bunları geçmiş kanıt olarak ayırır.

### 17–23 toplu değerlendirme kanıt haritası

Commit'ler gerçek Git geçmişinden; değişiklikler ilgili günün progress kaydındandır.
Bugün sütunu yalnız bu çalışmada gerçekten yürütülen kontrolleri gösterir.

| Gün | Commit | Kayıtlı değişiklik | Bugün yeniden çalıştırılan | Geçmişten aktarılan, bugün tekrarlanmayan | Açık sınır |
|---|---|---|---|---|---|
|17|4e0f4a9|Akademik timing decorator|Saf sonuç/log/async/sıra; full akademik akış|Decorator modundaki50 HTTP logu (G17)|Logger arızası; benchmark değil|
|18|21a86cb|İki Fake bildirim adapter'ı|Ortak adapter sözleşmesi/hata/iptal|Adapter ProbeFailure exit1 (G18)|Gerçek teslim/sağlayıcı uyumu yok|
|19|4c14a16|Dönem summary facade|Full SQL/HTTP, kredi/saat/no-write|Summary ProbeFailure temizliği (G19)|Tek snapshot/yetki garantisi yok|
|20|074c64b|Test senaryosu builder|Saf kopyalama/yapı; full uygunluk|Builder ProbeFailure exit1 (G20)|Bellek içi; DB fixture değil|
|21|39cb331|Commit sonrası observer|Full HTTP/SQL, hata/iptal,7 SELECT|Observer ProbeFailure exit1 (G21)|Kalıcı teslim/eşzamanlı onay yok|
|22|98d7ddd|Direct EF/repository deneyi|Full iki yol/tracking/SQL/UoW|Repository ProbeFailure temizliği (G22)|Teknoloji bağımsızlığı iddiası yok|
|23|Bu kaydı içeren commit; aşağıdaki mesajla Git'ten bulunur|Komut/query sınırı ve Mediator deneyi|cqrs/unit/core/full; model; exit1|Yok|Count/items iki sorgu; üretimde doğrudan çağrı|

**Sıradaki gün:** Kimlik verisi ile iş verisi ayrımı; ASP.NET Core Identity.

**Commit/push:** Bu kaydı içeren kimlik Git geçmişindeki
`refactor: separate enrollment command and catalog query` commit'inden bulunur.
Push sonucu son raporda; kendi hash'i için ikinci commit üretilmez.

## Gün 24 — Kimlik verisi ile iş verisi ayrımı (2026-11-01)

**Başlangıç:** main, HEAD `48c239e6dbc9471c67a3ed76f1d53b1c1a2fd91f`, temiz
çalışma ağacı. Önceki günlerin dosyaları ve kayıtları korundu. SDK10.0.401,
EF/araç10.0.14, LocalDB17.0.4025.3; paketler yükseltilmedi.

**Somut değişiklik:** Identity EF10.0.14, ApplicationUser, mevcut context'te
IdentityDbContext tabanı/base mapping ve minimal IdentityCore/store DI. Student
UserId nullable/private setter; TryLinkAccount boş veya tekrar bağlantıyı reddeder,
kimliği aynen korur. Filtered unique + NO ACTION ilişki. Yeni migration
`20261101104350_AddIdentityAccounts`, yedi Identity tablosu ve Student bağlantısı;
akademik tablolar/kimlik stratejileri değişmedi. Rol tabloları vardır, rol
servisi/atama yoktur. HTTP endpoint/response veya kayıt koordinasyonu değiştirilmedi.

**Yeni kontroller:** IdentityModelChecks.Unit ve identity-model modu. Gerçek EF
UserManager parolasız hesap oluşturma/IdentityResult, yeni scope'ta var/yok okuma,
çoklu NULL profil, başarılı bağlantı, UX_Students_UserId/2601, eksik hesap FK/547,
öğrencisiz hesap, bağlı hesap silme FK/547 ve akademik verinin korunması. Ayrı
CreateAsync başarısından sonra aynı context'teki profil SaveChanges'i gerçek
öğrenci numarası unique hatasıyla başarısız; yeni scope hesabın kaldığını ve profilin
oluşmadığını gösterdi. Başarısız context yeniden kullanılmadı.

**Migration kanıtı:** Identity modu DB
`university_registration_tests_day6_c75c1953d60741c7839d4bdcda49f2fc` önceki
migration seviyesinde sentetik öğrenci, ders/dönem/açılış, Approved enrollment ve
başarı ilişkisi hazırladı. İleri migration sonrası kimlik/değer/durum/ilişkiler
korundu; UserId NULL. Update tekrarı ve model uyumu geçti. Fixture temizlendi.
Up/Down/snapshot ve üretilen SQL incelendi. App DB'ye yalnız açık update uygulandı;
ikinci update no-op. App'in sekiz akademik tablosundaki eski sütun değerleri
önce/sonra bellekte karşılaştırıldı, aynı kaldı. App'te kullanıcı hesabı üretilmedi;
lab hiçbir migration/test yazmasının hedefi olmadı.

**Yeniden kullanılan regresyonlar:** restore/build (0 uyarı/hata), unit ve core
exit0. Core DB `university_registration_tests_day10_566c79e0b94943df9ed351ea8c335e92`
sonunda boş. İlk full koşusu eski rollback-only academic-example.sql dosyasında
SQL1934/QUOTED_IDENTIFIER nedeniyle başarısız oldu; başarı olarak sayılmadı.
Filtreli index gereksinimi için yalnız örneğe SET QUOTED_IDENTIFIER ON eklendi.
Eski StateChecks migration fixture'ı, o eski şemada bulunmayan UserId'yi yazmamak
üzere Student'ın açık sütunlarını kullanacak şekilde uyarlandı.

Düzeltmeden sonra full exit0: SQL şema/kısıt/seed tekrarı; ders/dönem/açılış/öğrenci/
tekli ve batch kayıt; katalog gerçek COUNT + filtre/projection/OFFSET SQL'i;
ön koşul/kredi/saat/state; summary; tracking/transaction/repository; saf
Strategy/factory/decorator/adapter/builder/Mediator ve observer kontrolleri geçti.
Gerçek ilk INSERT sonrası batch hatası kendi yazmalarını geri aldı ve kesinleşmiş
rakip kaydı korudu. Observer yeni context'te Approved okudu; DB yazma hatasında
olay yoktu; kritik olmayan hata/commit sonrası iptal sözleşmeleri korundu.
Identity kontrolleri full içinde de geçti. Full DB
`university_registration_tests_day6_0d88d5fdc92848e1a44c787ebda32b4e` sonunda SQL ile
2 ders/2 dönem/3 açılış seed'i, diğer beş akademik tabloda0 ve AspNetUsers0 doğrulandı.
Geçici API/check süreçleri kalmadı. Test şemaları tutuldu; sequence sıfırlanmadı.
`identity-model -ProbeFailure` DB oluşturmadan beklenen exit1'i üst betiğe iletti.

**Sınırlar/eksikler:** Authentication/authorization, public kayıt/giriş/bağlama,
cookie/JWT, SignInManager, parola ile giriş ve provisioning akışı yok. Parolasız
test hesabı güvenli self-servis değildir. Tek context iki SaveChanges'i atomik
kılmaz. Down incelendi ama uygulanmadı. Logger/temizlik arızası ayrıca simüle
edilmedi; önceki özel modların her biri ayrı komutla tekrarlanmadı, full içindeki
ortak kontroller yeniden kullanıldı. Yeni hesap–profil yarış garantisi yok.

**Sıradaki gün:** Hesap oluşturma, parola hash'i, normalizasyon ve e-posta doğrulama.

**Commit/push:** Bu kaydın kimliği Git geçmişindeki
`feat: separate identity accounts from student profiles` commit'inden bulunur.
Push sonucu son raporda; kendi hash'i için ek commit üretilmez.

## Gün 25 — Identity hesap oluşturma ve yerel e-posta doğrulama (2026-11-04)

Başlangıç main HEAD'i `9fdc375fc82f48adcf69f75c002793ee9e9291e3` idi.
24. gün ApplicationUser, IdentityDbContext, UserManager store ve nullable Student.UserId
ilişkisi gerçek dosyalarda/migration geçmişinde bulundu; yeniden uygulanmadı.
Çalışma ağacı başlangıçta temizdi. EF/Identity 10.0.14 ve SDK 10.0.401 korundu.

**Üretim katkısı:** POST /accounts/register ve /accounts/confirm-email; ayrı request/
response türleri, Identity parola doğrulama/hash/normalizasyonu, RequireUniqueEmail
ve gerçek UserNameIndex hatasının dar eşlemesi. E-posta Trim edilir, parola değişmez.
12–128 karakter sınırı; diğer bileşim zorunlulukları kapalıdır. Token sağlayıcısının
süresi iki saat; UTF8/Base64Url taşıma ve yapılandırılmış güvenilir bağlantı kullanılır.
Development dışında açılması reddedilen, açıkça etkinleştirilen yerel dosya kutusu
ve Data Protection anahtarları Git dışında tutulur. Gerçek dış gönderim yoktur.
Hesap commit'inden sonraki dispatch hatası hesabı silmez: 201/failed; çağıran iptali
cancelled olarak ayrılır. Oturum veya otomatik Student bağlantısı oluşturulmaz.

**Yeni kontroller:** AccountsChecks ve accounts modu. Gerçek Kestrel/LocalDB üzerinde
201/400/503, parola doğrulama ve farklı salt'lı hash, normalizasyon, eşzamanlı tekrar,
UserNameIndex kayıt anı yarışı ve ilgisiz PK hatasının 500 kalması geçti. Token yanlış
hesap/değiştirme/bozuk kodlama, kalıcı doğrulama, geçerli tekrar kullanım, yeni token'ın
eskiyi geçersiz kılmaması ve security stamp değişiminin eskiyi reddetmesi gözlendi.
Süre testi gerçek sağlayıcıda kontrollü pozitif bir tick ayarıyla geçti; iki saat
beklenmedi. Ayrı gerçek uygulama süreçlerinde aynı izole anahtar alanı ve fiziksel
mesaj kutusuyla restart sonrası doğrulama geçti. Sender hatası/iptalinde hesap kaldı;
Production kutu yasağı ve gözlenen log/yanıtlarda parola/hash/token yokluğu doğrulandı.
İlk test derleme ve scope hazırlığı hataları düzeltildi; başarısız koşular başarı
sayılmadı ve kendi verileri temizlendi.

**Yeniden kullanılan kontroller:** restore/build 0 uyarı/0 hata; unit, core ve full
exit0. Full; Identity model, katalog SQL/projection, akademik uygunluk/state,
summary, eski deneyler ve tekli/batch gerçek unique/FK kontrollerini çalıştırdı.
İlk INSERT sonrası batch rollback ve kesinleşmiş rakip kaydının korunması geçti.
Observer yeni context ile Approved gördü; yazma hatasında olay yoktu; observer
hata/iptal davranışları korundu. Accounts kontrolleri full içinde tekrar geçti.
accounts -ProbeFailure beklenen exit1'i üst çalıştırıcıya iletti.

Full test DB'si `university_registration_tests_day6_10a7e79d6fed4cd5ab9640a6114be668`
sonunda bağımsız SQL kontrolünde 2 ders/2 dönem/3 açılış seed'i; Students, Enrollments,
CoursePrerequisites, StudentCompletedCourses, CourseOfferingTimeSlots ve AspNetUsers
sıfırdı. Geçici uygulama/check süreçleri ve accounts-checks çalışma dizinleri kalmadı.
Test DB şeması korundu; sequence sıfırlanmadı. App DB yalnız mevcut verify.sql ile
okundu; lab hedef alınmadı. Model/snapshot farkı yok, yeni migration üretilmedi.

**Sınırlar:** Giriş/çıkış, authentication/authorization, resend/recovery, rate limit,
tam enumeration koruması ve gerçek posta teslimi yoktur. Accepted/queued-locally
posta kutusuna erişimi kanıtlamaz. Token tek kullanımlık değildir. UserManager'ın
bu API'leri request CancellationToken almaz; sınır kontrolleri ve sender token'ı
ayrı yönetilir. Commit sonrası yanıt kaybı mümkündür. Logger arızası veya bütün dış
proxy logları için garanti verilmez. İki saatlik duvar saati testi ve Down yapılmadı.
Özel regresyon modları ayrıca tekrarlanmak yerine full içindeki ortak kontroller
kullanıldı. Test geçen davranışlar üretim güvenliği/teslim garantisi değildir.

**Sıradaki gün:** Authentication/authorization, cookie session ve bearer; giriş/çıkış
ve korunan endpoint.

**Commit/push:** Bu kaydın commit'i Git geçmişindeki
`feat: register accounts and confirm email locally` mesajıyla bulunur; push sonucu
son rapordadır. Hash yazmak için ek commit oluşturulmaz.

## Gün 26 — Identity cookie giriş/çıkış ve CSRF (2026-11-04)

Başlangıç main HEAD'i `3bd05f3573bcffafecc3d6c76ef7cf2c28ed700c`, çalışma ağacı
temizdi. 24. gün `9fdc375` ve 25. gün `3bd05f3` gerçek dosyalar/geçmiş üzerinden
doğrulandı; önceki kapsam yeniden uygulanmadı. AGENTS.md bulunmadı. SDK 10.0.401,
net10.0 ve Identity/EF 10.0.14 korundu; paket eklenmedi/yükseltilmedi.

**Değişiklik:** AddSignInManager + Identity cookie şemaları, açık authentication/
authorization middleware; RequireConfirmedEmail, 5 hata/5 dakika lockout. Login
email/password; e-posta Trim, parola aynen. Başarı200; olmayan/yanlış/doğrulanmamış/
kilitli hesap ortak401. Me kimlik gerektirir ve yalnız id/email döndürür; profil
bağlantısı gerektirmez. Logout SignOutAsync +204. GET antiforgery token sağlar;
login/logout açık ValidateRequestAsync kullanır. Kimlik değişiminden sonra yeni
token gerekir. HttpOnly/Secure/Strict uygulama cookie'si, 20 dakika sabit bilet,
isPersistent:false, SlidingExpiration:false. Mevcut akademik endpoint'ler değişmedi.

**Yeni gerçek HTTPS/LocalDB kontrolleri:** CookieSessionChecks ve cookie modu; her
koşunun sertifikasına pinning (global bypass/trust-store değişimi yok). İlk iki TLS
koşusu Windows Schannel'ın üretilmiş anahtar kullanımında bağlantı hatası verdi;
başarı sayılmadı. Sertifika bellek içinde PFX yeniden yüklemesiyle düzeltildi.
Bu koşuların kendi verileri/dizinleri temizlendi.
Başarılı cookie koşusunda anonim/farklı istemci401, doğrulanmış profil olmayan
hesap200, küçük me yanıtı, ortak giriş retleri, lockout sayacı/kilidi, bozuk JSON/
alan400, bozuk cookie ve gerekli claim'i eksik bilet401 geçti. Login/logout eksik/
yanlış CSRF400; anonim token'ın kimlik sonrası reddi ve oturumun korunması geçti.
Geçerli logout204 ardından cookie jar ile me401. HttpOnly/Secure/Strict ve kalıcı
Expires/Max-Age yokluğu doğrulandı. Cookie options kontrollü TimeProvider ile
11.dakikada me200 ve yenileme Set-Cookie yokluğu; 21.dakikada ret gözlendi. Çıkış
sonrası eski kopya bilet süre dolana kadar çalıştı; anında revocation iddiası yoktur.
İlgili akademik kayıt/statuslar ve test Student.UserId bağlantısı değişmedi;
gözlenen uygulama log/yanıtları secret taşımadı.

**Yeniden kullanılan doğrulamalar:** restore/build 0 uyarı/0 hata; unit/core/full
exit0. Full önceki Identity/account parola/token/dispatch/restart kontrollerini,
SQL şema/seed, katalog, akademik uygunluk/state/summary ve deneyleri tekrar kullandı.
Gerçek unique/FK yazma dalları ve ilk INSERT sonrası batch rollback/rakip kayıt
korunması geçti. Observer yeni context'te Approved okudu; yazma hatasında olay yok,
kritik olmayan hata/iptal davranışı korundu. Cookie kontrolleri full içinde de geçti.
Model/snapshot farkı yok; migration oluşturulmadı. cookie -ProbeFailure beklenen
sıfır olmayan exit code'u üst çalıştırıcıya iletti.

Full DB `university_registration_tests_day6_044f0be636bd4b49a089037a5cc2b62f` bağımsız
SQL kontrolünde yalnız 2 ders/2 dönem/3 açılış seed'i içerdi; öğrenciler, enrollment,
ön koşul/başarı/saat ilişkileri ve AspNetUsers sıfırdı. Geçici host/süreçler ve
cookie-checks çalışma dizinleri temizlendi. İzole test DB şemaları tutuldu;
sequence değerleri sıfırlanmadı. App/lab test hedefi olmadı.

**Açık sınırlar:** Rol403 senaryosu, 2FA kurulum/akışı ve tüm cihazlarda çıkış yok.
Yalnız Succeeded kabul edilir; 2FA sonucu başarıya çevrilmez, ayrı gerçek 2FA fixture'ı
kurulmadı. Browser SameSite davranışı gerçek tarayıcıyla sınanmadı; HTTP cookie
öznitelikleri ve sunucu CSRF denetimi doğrulandı. Süre 20 dakika beklenerek değil
sağlayıcının kontrollü saatiyle test edildi. CSRF koruması bugün login/logout'a
uygulanır; kalan akademik endpoint'ler halen yetkisizdir. Yetkilendirilmiş öğrenci
self-servisi, tam enumeration/rate limit, çalıntı biletin anında iptali veya dış proxy
log güvenliği garantisi yoktur. Down uygulanmadı; şema değişmedi. Özel eski modlar
ayrı ayrı tekrarlanmak yerine full içindeki ortak kontroller kullanıldı.

**Sıradaki gün:** JWT doğrulaması.

**Commit/push:** Bu kaydın kimliği Git geçmişindeki
`feat: add cookie login logout and protected account endpoint` mesajından bulunur.
Push sonucu son raporda; hash yazmak için ikinci commit üretilmez.

## Gün 27 — JWT doğrulaması ve cookie/bearer ayrımı (2026-11-04)

Başlangıç main HEAD'i `4c90ddd2388356dc6ca055c2ecbf1723c7f0ba67`; çalışma ağacı
temizdi. 24–26. gün Identity/profile, register/confirmation ve cookie/CSRF dosyaları,
ilerleme kaydı ve gerçek Git geçmişinden doğrulandı; yeniden uygulanmadı.
AGENTS.md bulunmadı. net10.0/SDK 10.0.401, mevcut Identity/EF sürümleri korundu.
Resmi Microsoft/NuGet kaynaklarıyla uyumlu kararlı JwtBearer 10.0.14 eklenmiştir.

**Değişiklik:** UniversityJwtSetup/Keys, ayrı UniversityBearer/UniversityBearerOnly,
cookie default'unu değiştirmeyen kayıt. Enabled varsayılan kapalı. Bearer-me küçük
accountId yanıtı verir, StudentId varsaymaz ve DB sorgusu yapmaz. Development+opt-in
dev-token cookie-only policy, antiforgery ve güncel hesap uygunluğundan sonra RS256
üretir. Sunucudan sub/jti/iss/aud/iat/nbf/exp, 600 saniye, no-store. İstek gövdesindeki
kimlik/rol/süre kullanılmaz. Public doğrulama anahtarı private imza nesnesinden ayrıdır;
DI singleton sahibi host kapanınca dispose eder. Repo dışı, kullanıcı ACL'li açık
anahtar kurulum betiği eklenmiştir; startup anahtar oluşturmaz/anahtarı ezmez.

Doğrulama imza, yalnız RS256, issuer, audience, lifetime, exp ve 30 saniye skew
koşullarını açık belirtir. MapInboundClaims=false; validated event tek dolu string
sub'u, array ve duplicate JSON alanı dahil denetler. Query token alınmaz; uzaktan
key URL'si güvenilmez. Handler hata ayrıntısı ve JWT/request URL logları bastırılır.
Akademik model/transaction ve mevcut HTTP sözleşmeleri değiştirilmedi.

**Yeni gerçek HTTPS kontrolleri:** JwtChecks, 26. gün CookieSessionChecks hazırlığını
kullanır; sertifika sadece o koşuya pinlenir, RSA anahtarları repo dışı geçicidir.
İlk koşu sağlayıcının PrivateKeyStatus=Unknown metadatasını yanlış yorumlayan test
assertion'ında durdu; başarılı sayılmadı. Kontrol gerçek private export reddi ve ayrı
RSA nesneleriyle düzeltildi; test verileri/anahtarları temizlendi.

Başarılı jwt koşusu ve full içindeki tekrarında: cookie+CSRF üretim, doğru sunucu
sub/claims/süre, no-store, profilsiz hesap, tüm cookie/bearer ayrımları ve bearer
kimliği için sıfır ek DB okuması geçti. SQL interceptor önce login okumasını gördü.
Dev-token eksik/yanlış/eski kimlik CSRF'sini reddetti; client userId/role/süre etkisiz.
Production dev opt-in guard, eksik/yanlış ayarlar/anahtarlar, dev route404 ve yalnız
public anahtarlı Production host'ta bearer200 doğrulandı.

**HTTP ret matrisi (hepsi gerçek istekle401):** Değiştirilmiş payload/imzası korunmuş;
yanlış RSA anahtarı; yanlış iss; yanlış aud; skew ötesi geçmiş exp; gelecekte nbf;
exp yok; unsigned; RS512; sub yok; boş sub; sub array; yinelenen JSON sub; bozuk metin.
Fixture oluşturma reddi API kanıtı sayılmadı. Skew içi exp=-2s/nbf=+10s kabul,
dışı ±120s ret; uzun bekleme yapılmadı, tam sınır anı iddiası yoktur. Aynı JWT iki
kez ve cookie logout sonrası çalıştı; jti replay veya çıkış revocation garantisi yok.

**Regresyonlar:** restore/build0 uyarı/hata; unit/core/cookie/full exit0. Full önceki
Identity/account parola/confirmation/dispatch/restart, gerçek katalog SQL, akademik
uygunluk/state/summary, batch ilk INSERT sonrası rollback ve rakip kayıt korunması,
gerçek FK/unique ve onay sonrası Observer kontrollerini yeniden çalıştırdı. Model/
snapshot farkı yok; migration üretilmedi. jwt -ProbeFailure üst çalıştırıcıya sıfır
olmayan exit code verdi. Anahtar kurulum betiği repo dışı geçici dizinde eşleşen
3072-bit çift üretti, mevcut dizine ikinci çağrıyı reddetti; dosyalar temizlendi.

Full DB `university_registration_tests_day6_b46459fd568d4f9298e5f1ea5004c71a` bağımsız
SQL kontrolünde yalnız 2 ders/2 dönem/3 açılış seed'i; Student/Enrollment/Identity
hesapları ve ön koşul/başarı/saat ilişkileri sıfırdı. Süreç ve geçici JWT anahtar
dizini kalmadı. App/lab test hedefi olmadı; izole DB şemaları/sequence boşlukları
korundu. Test token/private key/log çıktısı commit kapsamına alınmadı.

**Sınırlar/eksikler:** OAuth/OIDC, refresh/rotation, JWKS/anahtar rotasyonu, denylist,
oturum/hesap revocation ve tam öğrenci yetkilendirmesi yoktur. Cookie logout/parola/
hesap değişikliği mevcut JWT'yi otomatik iptal etmez; bearer DB'ye bakmaz. Gerçek
browser SameSite ve dış proxy logları bu HTTP testinin kapsamı değildir. Tam 30s
sınırı, çok instance anahtar yönetimi ve Down denenmedi. Kaynak/setup guard'ları ve
public-only Production HTTPS host'u kullanıldı; ayrı deployment yapılmadı. Önceki
modların tamamı ayrıca çağrılmak yerine full içindeki ortak kontroller kullanıldı.

**Sıradaki gün:** Access/refresh token ve rotation.

**Commit/push:** Bu kaydın kimliği Git geçmişindeki
`feat: validate JWT bearer tokens alongside cookie authentication` mesajından
bulunur; push sonucu son rapordadır. Hash yazmak için ek commit oluşturulmaz.

Son test izolasyonu incelemesinde, önceki account/core süreçlerinin kullanıcı JWT
ortam değişkenlerini devralmaması için Enabled/DevTokenEnabled testte açıkça kapatıldı;
JWT fixture kendi ayarlarını bunun üzerine kurar. Bu değişiklikten sonra full yeniden
exit0 oldu. Son DB `university_registration_tests_day6_d0c168d65c5b493fb40740d6c669de6d`
bağımsız SQL kontrolünde yine 2/2/3 seed, diğer akademik/Identity test tabloları0;
JWT geçici anahtarları, cookie çalışma dizinleri ve süreçler kalmadı.

## Gün 28 — Access/refresh, kalıcı oturum, sıralı rotation

**İncelenen başlangıç:** main HEAD `31244599d817d7e0c00eab3d8e8d63a8b2e8e220`, temiz
çalışma ağacı. Identity/account/cookie/JWT dosyaları ve önceki ilerleme kayıtları gerçekten
mevcuttu; önceki günler yeniden uygulanmadı. Paket/sürüm eklenmedi veya yükseltilmedi.

**Değişiklik:** RefreshSession (Guid, Identity UserId, binary(32) güncel SHA-256 özeti,
UTC tarihler, nullable revoked), UNIQUE/NO ACTION/tarih kısıtları. TokenSessions JWT
üretimini kopyalamadan topladı; 32 güvenli rastgele bayttan kanonik Base64Url43 üretir.
İlk dev-token cookie+CSRF kontrolünden sonra ayrı oturum kaydeder, dört alanlı çift döner.
Refresh HTTPS JSON/AllowAnonymous; cookie/access token yetkisine dayanmaz. Biçim400,
bilinmeyen/eski/expired/revoked/uygunsuz hesap ortak401; beklenmeyen yazma500. Tek güncel
hash değişir, sabit7gün sonu uzamaz. Access600s ve oturum7gün açık ayarlardır; kalan süre
JWT exp/expiresIn'i sınırlar. Bugünkü refresh de açık Development üreticisine bağlıdır;
Production'ın yalnız public-key doğrulama davranışı korunur.

**Migration:** `20261105101325_AddRefreshSessions` Up/Down/üretilen SQL incelendi;
yalnız yeni tablo/index/kısıtlar. Açık hedef doğrulamasından sonra app DB'sine uygulandı;
ikinci update no-op, model/snapshot farkı yok. App önce/sonra Courses2/Terms2/Offerings3,
Students0/Enrollments0/Accounts0; yeni RefreshSessions0. İzole önceki Identity migration
seviyesinden yükseltmede hesap, bağlı Student ve Approved Enrollment kimlikleri/alanları/
ilişkileri korundu; replay no-op. Lab'a dokunulmadı. Down çalıştırılmadı.

**Gerçek kontroller:** restore/build0 uyarı/hata; unit/core/refresh/full exit0. Refresh
izole DB `university_registration_tests_day6_8f267c74c87c4d16b0bdf9644c5c7d4a` yükseltme
ve HTTPS kontrollerini geçti. R1→R2→R3, eski token401, aynı satır/id/user/son tarih,
hash eşleşmesi, her açık ilk üretimde yeni oturum, Student'sız hesap, expired access401
ama refresh200, önceki access hâlâ200, account lockout401, expired/revoked401, son tarihe
yakın exp üst sınırı/no-store doğrulandı. Gerçek UPDATE UNIQUE hatasında500/no pair,
eski hash korunumu, eski token'ın yeniden çalışması ve kaydedilmeyen yeni hash'in yokluğu
yeni context ile kanıtlandı. FK/NO ACTION/UNIQUE/CHECK/NOT NULL gerçek SQL ile sınandı.

Full `university_registration_tests_day6_c2e188916f2e47aca42ddd8c9d1cbc23` katalog gerçek
SQL/projection, tekli/batch/state/eligibility, ilk INSERT sonrası rollback/rakip korunumu,
onay commit sonrası Observer ve hata/iptal, Identity/account/confirmation/restart,
cookie HTTPS/CSRF/süre ve JWT ret matrisini yeniden çalıştırdı. Bağımsız SQL temizlik
kontrolü yalnız2/2/3 seed; Student/Enrollment/Accounts/RefreshSessions/önkoşul/başarı/saat0.
JWT geçici anahtar dizinleri, cookie çalışma dizinleri ve geçici uygulama süreçleri kalmadı.
İzole test DB şemaları mevcut yaklaşım gereği korundu; sequence sıfırlanmadı.
`refresh -ProbeFailure` beklenen kontrollü assertion ile exit1 verdi ve süreç kapandı;
bu bir ürün başarısı olarak sayılmadı. Son başarılı full'den sonra ürün kodu değişmedi.

**Çözülen test sorunları:** İlk refresh host'unda bozuk JSON Development exception ayarı
üretimden farklıydı; RouteHandlerOptions eşitlendi, ilgili koşu tekrar exit0 oldu.
İlk full eski16tablo beklentisinde durdu;17tablo ve yeni refresh metadata kontrolü eklendi,
full yeniden exit0 oldu. App hedef guard'ı genel connection builder alias'ını tanımadığı
ilk denemeyi mutasyonsuz durdurdu; SqlConnectionStringBuilder ile hedef doğrulandı.
Eski System.Data.SqlClient read-only bağlantı denemesi handshake hatası verdi; aynı
korunum kontrolü sqlcmd ile başarıyla yapıldı. Bu başarısız adımlar başarı sayılmadı.

**Sınırlar/eksikler:** Yalnız sıralı rotation kanıtı; concurrency tek başarı, history,
family/reuse tespiti, retry/denylist yok. DB commit/HTTP teslimi ayrı; gerçek bağlantı
kopması simüle edilmedi. Hatalı yazmanın ham yeni token'ı dışarı verilmediği için onu
HTTP ile tekrar gönderme testi yapılmadı; kaydedilmeyen özetin DB'de olmadığı doğrulandı.
Cookie logout/refresh iptali eski access token'ı iptal etmez. JWT30s skew korunur;
refresh son tarihi uzamaz. Browser saklama, production refresh işletimi ve genel OAuth
sunucusu kapsam dışıdır. Anahtar/token/SQL logları commit'e alınmadı.

**Sıradaki gün:** Token family, reuse, eşzamanlı refresh ve çoklu cihaz.
**Commit/push:** `feat: add persistent refresh sessions and token rotation` Git kaydından
bulunur; son rapor gerçek push sonucunu verir. Hash için ikinci commit üretilmez.

## Gün 29 — Token ailesi, reuse, eşzamanlı tüketim ve sahiplik

**Başlangıç:** main `8b6528b3f9070c86c745ab9bb7f48df04661e649`, temiz çalışma ağacı.
Cookie/CSRF, ayrı bearer, TokenSessions ve gün28 migration/testleri gerçekten mevcut.
Önceki günler yeniden uygulanmadı. Paket/framework eklenmedi veya yükseltilmedi.

**Değişiklik:** RefreshSession aile olarak kaldı; current hash sütunu kaldırılıp her
özet için RefreshTokenRecord eklendi. Kullanım/ardıl bağlantısı ve binary32 unique özet;
(SessionId,ReplacedById) birleşik FK aynı aileyi korur. UsedAt/created ve self-replacement
CHECK'leri var. Yeni token geçmişi dışında akademik/Identity şeması değişmedi.

TokenSessions refresh ve revoke, açık transaction içinde aynı Session satırında
parametreli UPDLOCK/HOLDLOCK kullanır. Kilit öncesi yalnız immutable aileId okunur;
durum sonra tekrar okunur. Koşullu UsedAtUtc IS NULL tüketimi satır sayısıyla doğrulanır;
ExecuteUpdate, ardıl SaveChanges, replacement ve commit aynı transaction'dadır.
Kullanılmış token aile iptalini COMMIT edip ortak401 döndürür. Bilinmeyen token aile
iptal etmez. No-tracking okumalar bulk update ile eski tracker yazmasını önler.

GET /accounts/sessions ve POST /accounts/sessions/{id}/revoke açık cookie-only policy
kullanır; POST CSRF doğrular. Liste kullanıcı filtresi ve deterministik projection;
no-store, dört güvenli alan. Sahipsiz/olmayan aynı404, kendi iptal tekrarında204 ve aynı
timestamp. Cookie logout ve JWT kabulü değişmedi. Yönetim de mevcut Development
üretici opt-in sınırındadır; genel üretim OAuth/cihaz yönetimi değildir.

**Migration:** `20261105103704_AddRefreshTokenHistory` SQL'i incelendi: tablo/index/FK/
CHECK oluştur → eski her güncel özeti INSERT SELECT ile taşı → eski index/sütunu kaldır.
Up/Down incelendi; Down çalıştırılmadı. App hedefi açık kontrol edilip uygulandı; ikinci
update no-op, model/snapshot farkı yok. App önce/sonra2ders/2dönem/3açılış; öğrenci,
enrollment,hesap,session/token0. Lab'a dokunulmadı. İzole Day28 şeması yükseltmesinde
akademik kimlik/Approved kayıt/hesap-profil bağlantısı, mevcut aileId/tarihler ve özet
korundu; taşınan token gerçek HTTPS refresh200 verdi. Gün28'in atılmış geçmişi yoktan
üretilmedi; migration öncesi kayıp özetler için reuse tespiti iddiası yoktur.

**Gerçek doğrulama:** restore/build0 uyarı/hata; unit/core/full exit0. Refresh-family
HTTPS/LocalDB testi sıralı R1/R2/R3 ve kullanım/ardıl linklerini, aynı sabit aile sonunu,
R1 reuse401'in kalıcı iptalini ve R3 retini doğruladı. Aynı hesabın başka ailesi çalıştı,
bilinmeyen token onu değiştirmedi. İki ayrı connection/context kontrollü bariyerde
aynı token ile yarıştı: tam bir200/bir401, iki token kaydı (bir ardıl), bir tüketim ve
iptal aile. Kazananın yeni refresh token'ı da401 oldu.

Ordered test interceptor'ı ilk SQL aile kilidini aldıktan sonra ikinci HTTP işlemini
başlattı. Revoke-first → refresh401/tek token; refresh-first →200, ardından revoke204
ve ardıl401. İki sırada da kalıcı aile iptal. Rastgele Task.Delay kullanılmadı.
Gerçek ardıl INSERT UNIQUE ihlali koşullu tüketimden sonra oluşturuldu: HTTP500,
eski UsedAtUtc/Replacement null, ardıl yok ve eski token yeniden kullanılabilir.
Yeni context kalıcı durumu doğruladı. Reuse401 rollback değil commit kanıtıdır.

HesapA listesi sadeceA/sıralı; B listesi A'yı içermedi; B'nin bilinenA-id ve olmayan-id
revoke'u404/hasarsız. CSRF eksik/geçersiz400, bearer-only yönetim401, kendi revoke204/
tekrar204/timestampkorunumu doğrulandı. Eski access JWT aile iptalinden sonra200 kaldı.
Gerçek UNIQUE/CHECK/self/cross-family-FK/NO ACTION deneyleri beklenen SQL kısıtlarıyla
başarılı. Cookie/JWT ortak HTTPS host'u sertifikayı sabitler ve log secret sızıntısını
kontrol eder; eski parolasız/hesap confirmation/CSRF/süre testleri full içinde çalıştı.

Full DB `university_registration_tests_day6_76ea16d367c249988a82a469cf6be3ef` bağımsız
SQL kontrolünde yalnız2/2/3seed, Student/Enrollment/Accounts/Session/Token/önkoşul/
başarı/saat0. Full ayrıca katalog SQL/projection, gerçek batch ilk INSERT sonrası
rollback/rakip korunumu ve onay sonrası observer hata/iptal davranışlarını yeniden geçti.

**Düzeltilen kontrol:** İlk family koşusunda ürün/HTTP testleri geçti fakat migration
fixture temizliğine token→session sırası eklenmemişti; exit1 doğru korundu. Temizlik
sırası düzeltildi. İlk manuel telafi SQL'i QUOTED_IDENTIFIER ayarında reddedildi ve
transaction kesinleşmedi; sqlcmd -I ile yalnız o GUID test DB'sindeki migration29
fixture satırları temizlendi. Sonraki family koşusu exit0 ve boş test tabloları verdi.
Bu başarısız koşu başarılı sayılmadı; ürün endpoint'ine test anahtarı eklenmedi.

**Sınırlar:** Strict reuse masum iki sekme/yanıt kaybını saldırıdan ayırmaz; yeniden
giriş gerekebilir. Sunucu DB kilidi, istemci single-flight'tan farklıdır. Kalıcı teslim,
çökme/ağ kopması, çok node deployment, tolerans penceresi veya otomatik retry denenmedi/
eklenmedi. Token geçmişi otomatik temizlenmez; saklama politikası ileride gerekir.
Cookie ve mevcut access JWT aile iptaliyle otomatik iptal olmaz. Key/token/log/build
çıktıları commit dışındadır. Test DB şemaları korunur; kullanıcı verisi silinmez.

**Sıradaki gün:** Parola sıfırlama ve tek kullanımlık süreli token.
**Commit/push:** `feat: detect refresh token reuse and manage session families` Git
mesajından bulunur; gerçek push sonucu son raporda. Hash için ek commit oluşturulmaz.

Son ek doğrulama: `refresh-family` yeniden exit0 oldu
(`university_registration_tests_day6_4c867fa142b64f6ba074eeb436a9661f`); aktif aileye ek
olarak eski revoked/expired ailelerin migration'da korunumu kontrol edildi. Ardından
`refresh-family -ProbeFailure` beklenen exit1 verdi; üst çalıştırıcı hata yayılımı
korundu, API kapandı. Son değişiklikler test/belge kapsamındadır; üretim kodunun full
ile doğrulanan davranışı değiştirilmedi.

## Gün 30 — Süreli parola sıfırlama ve atomik aile iptali

**Başlangıç:** main HEAD `8c38a415f8759e7faf9cb441da382309e205156b`, temiz çalışma
ağacı. Gün29 refresh aileleri/koordinasyonu ve24–28 hesap altyapısı gerçek koddan
incelendi; önceki günler yeniden uygulanmadı. Paketler10.0.14 kaldı.

**Değişiklik:** Forgot/reset endpoint'leri; ayrı adlandırılmış15 dakikalık reset
provider'ı (email2 saat değişmedi); güvenilen callback/yerel mevcut sender; UserManager
reset + aktif ailelerin iptali tek açık transaction. Reset, refresh, revoke ve ilk
üretim ortak hesap→aile→token kilit sırasını kullanır. İlk üretim kilit altında cookie
stamp'ini yeniden doğrular. Yeni model/migration yok; akademik davranışlar korunur.

**Gerçek doğrulama:** restore/build exit0 (0 warning/error),
`has-pending-model-changes` fark yok; unit/core/full exit0. Full; mevcut hesap,
Identity model, katalog, akademik uygunluk/durum, gerçek unique/FK, batch ilk INSERT
sonrası rollback ve observer regresyonlarını yeniden çalıştırdı. Password-reset
ayrı koşusu da exit0: pinned HTTPS, güvenilen Host'tan bağımsız callback, ortak202,
yerel dosya ve kontrollü sender arızası, amaç/kullanıcı/token/süre retleri, yeni ve eski
parola, normalizasyon yapılmaması, yeni scope kalıcılığı ve log secret taraması.

Gerçek CHECK hatası Identity SaveChanges sonrasındaki aile iptalinde üretildi:
500, yeni context'te parola/stamp/aileler aynı. Başarı204: iki aile iptal, başka hesap
korundu; otomatik giriş yok. Yeni reset talebi eskisini iptal etmedi; başarılı reset
eski ve önceden üretilmiş ikinci token'ı geçersiz kıldı. İki eşzamanlı reset bir204/
bir400, yalnız kazanan parola kalıcı. Reset–refresh ve reset–ilk üretim iki lock
sırasıyla sınandı; reset sonrası eski cookie aktif yeni aile üretemedi. Eski JWT200
kaldı; cookie testte stamp kontrolü tetiklenince401 oldu.

Full DB `university_registration_tests_day6_e2e12d56c53842e58d51458f048fefc3`
bağımsız SQL kontrolünde yalnız2ders/2dönem/3açılış seed; Student/Enrollment/Account/
Session/Token0. Son reset DB `university_registration_tests_day6_df2281444b0741e2b38b873dcf6e9330`
tüm bu tablolarda0. Son reset koşusu süre testini yalnız teste ait10ms provider süresi/
30ms beklemeyle tekrar geçti; üretim süresini değiştirmedi. İlk başarılı koşuda süre
sınaması eski token'ın negatif test lifespan'ıyla yapılıyordu; son koşu gerçek yeni
üretilen token + pozitif kısa süreyle daha açık hale getirildi. `password-reset
-ProbeFailure` beklenen exit1 üretti; üst komut bunu doğruladı. Probe DB
`university_registration_tests_day6_97797ffc5f034c4eb546aefbfc8fb395` tabloları da boş.
Geçici HTTPS host/anahtar/kutu temizliği tamamlandı; DB şemaları yaklaşım gereği kaldı.

**Sınırlar:** Stamp validator varsayılan30 dakika, sabit cookie20 dakika; anlık global
logout yok. JWT kendi expiry/skew sınırına kadar kabul edilebilir. Aynı hesap aileleri
DB kilidinde sıraya girer; generic lock/retry yok. Enumeration/timing/rate limit33.
gün; gerçek e-posta, network loss/crash recovery, çok sunuculu deployment ve reset
cancellation için ayrı fault testi yapılmadı. Kod cancellation'ı401'e çevirmiyor.
Day30 şema değiştirmediğinden migration Up/Down uygulanmadı; app/lab verisine dokunulmadı.

**Sıradaki gün:** Claims, roles ve policy.
**Commit/push:** `feat: reset passwords and revoke refresh sessions atomically`;
gerçek hash/push son raporda, hash yazmak için ek commit yok.

## Gün 31 — Identity rolleri, claim aktarımı ve katalog policy'si

**Başlangıç:** main `9d217038102fdafd1559911f4ce7db29bdb7cf8f`; temiz çalışma ağacı.
Gün28–30 refresh/koordinasyon/reset kodu ve ilerleme kayıtları gerçekten mevcuttu;
eski27. gün hash'i HEAD varsayılmadı. Paket sürümleri10.0.14 korundu.

**Üretim:** UniversityRoles sabitleri Student/Instructor/Advisor/StudentAffairs;
AddRoles ile Identity principal factory/store; açık Development `roles prepare`,
`roles add/remove <accountId> <role>` komutu. Startup/public register üyelik atamaz.
JWT ilk üretim/refresh güncel manager rollerini role claim'ine taşır;
RoleClaimType=role, MapInboundClaims=false. Hesap→aile→token koordinasyonu, reset
transaction'ı ve akademik batch SaveChanges/observer noktaları korundu.

**Bilinçli HTTP değişikliği:** POST courses, PUT courses/{id}, POST academic-terms ve
POST course-offerings artık cookie-only ManageAcademicCatalog + StudentAffairs +
gerçek antiforgery ister. Public GET'ler değişmez. Başka katalog yazma route'u yoktur.
Öğrenci/kayıt/onay/özete rol tabanlı sahte sahiplik garantisi eklenmedi.

**Gerçek kontroller:** restore/build exit0,0warning/error; model/snapshot fark yok.
Unit/core/full exit0. Full; yeni yetkili HTTPS/cookie/CSRF fixture ile eski katalog
validation/race/SQL/projection, gerçek unique/FK, batch ilk INSERT sonrası rollback,
state/eligibility, committed observer, accounts/confirmation, cookie/JWT/refresh
family/reset regresyonlarını yeniden çalıştırdı. Aynı testler kopyalanmadı; testler
bypass header veya global authorization kapatma kullanmıyor.

Roles modu gerçek HTTPS/LocalDB'de dört yazma için anonymous401, rolü olmayan ve
Student/Instructor/Advisor403, StudentAffairs201/200; ek rol200; CSRF eksik/yanlış400;
yetkili negatif kredi400, duplicate409, missing404 verdi. Retlerden sonra yeni context
ile katalog snapshot'ı korundu. Profil bağlantısı/onaylı e-posta yetki sağlamadı.
Public register'a role/roles/permission verilmesine rağmen üyelik yoktu. Cookie ve
bearer rol politikaları, tampered role401, OR/AND ve Production deney route404 geçti.

Rol kaldırma stamp'i değiştirmedi. Eski cookie/JWT yetkisi devam etti; yeni login,
yeni mint ve refresh token'ında kaldırılmış rol403 oldu. Mevcut fixed20dk cookie,
kontrollü saatte19dk eski rol200 ve21dk401; stamp aralığı30dk değişmedi. Anlık rol
revocation veya her istekte üyelik sorgusu yoktur. Bearer-me ek DB sorgusu yapmıyor.

**Komut/hata yayılımı:** Manager tabanlı prepare iki kez4rol, add/repeat/remove;
olmayana/izin dışına exit1 ve Production reddi test edildi. Gerçek CLI süreçleri de
geçersiz rol ve Production'da HTTP hostu açmadan exit1 verdi. Son `roles -ProbeFailure`
beklenen assertion/exit1 üretti; başarılı test sayılmadı.

**Ara başarısızlıklar:** İlk genişletilmiş cookie test hostu katalog reader DI kaydı
eksik olduğu için başlayamadı; kayıt eklenip koşu geçti. Son fixture ayrımında kalan
çift Advisor ataması `manager membership` assertion'ını doğru biçimde başarısız yaptı;
yinelenen atama çıkarıldı. Son roles koşusu exit0. Bu iki koşunun verileri de temizlendi.

**Temizlik kanıtı:** Full DB `university_registration_tests_day6_d17e9bad19d3411fb5f0817707c71a8f`
bağımsız SQL sorgusunda Accounts/Roles/UserRoles/Session/Token/Student/Enrollment0,
yalnız2ders/2dönem/3açılış seed. Core
`university_registration_tests_day10_5fdf1def4b9243778215442f1341dd22` ve son roles
`university_registration_tests_day6_9a8a8cb5a6c2483ab4f1a3f4fd9ddb2e` aynı tablolarda
ve katalogda0. Probe ve iki başarısız koşunun DB'leri de boş. Geçici catalog/cookie
fixture klasörü ve UniversityRegistration süreç sayısı0. Test DB şemaları kalır;
app/lab test hedefi olmadı. Yeni migration/Down yoktur.

**Sınırlar:** Yerel bakım Production provisioning değildir. Role değişimi/yayınlanmış
JWT için anlık iptal ve eşzamanlı rol provisioning garantisi kurulmadı. Rate limit,
network loss/crash, gerçek posta ve saldırı kapsamlı timing ölçümü yapılmadı. Son full
sonrasında yalnız tek-rol/ek-rol fixture açıklığı ve temizleme hedef koruması iyileştirildi;
son roles koşusu bunlarla yeniden geçti. Ürün kodu full sonrasında değiştirilmedi.

**Sıradaki gün:** Resource-based authorization, ownership ve IDOR; Student sahipliği,
danışman ataması, Instructor–açılış ilişkisi ve kişisel akademik okuma/yazma korumaları.
**Commit/push:** `feat: authorize academic catalog management with Identity roles`;
gerçek hash/push son raporda, hash için ek commit yok.

## Gün 32 — kaynak bazlı yetkilendirme / öğrenci sahipliği / danışman ataması

**Başlangıç kanıtı:** main, temiz çalışma ağacı, HEAD
`f2ee3ae5093054a1f7f8433871b382f9b6cc9620` (Gün31). Mevcut Identity/cookie/JWT,
refresh aileleri, reset ve rol kodları incelendi; önceki günler yeniden uygulanmadı.
Bu koşuda bunların mevcut regresyonları da yeniden çalıştı.

**Ürün:** `StudentAccess` cookie-only Read/Manage/Decide policy’leri, küçük resource
projection/requirement/handler ve IAuthorizationService kullanır. Student+UserId
sahipliği create/batch/eligibility/submit için; Advisor+AdvisorUserId ataması karar
vermek için zorunludur. Profil/liste/enrollment detay/dönem özeti owner veya atanmış
advisor tarafından okunabilir. StudentAffairs/Instructor bypass yoktur. Profil
provisioning POST artık StudentAffairs+CSRF. Genel kimlik401/rol403, olmayan veya
ilişkisiz kaynak ortak404; kaynak kontrolü akademik ayrıntılardan önce. Tüm kişisel
route matrisi README’de; katalog okumaları public, account akışları değişmedi.

**Model/komut:** nullable, nonunique, NO ACTION AdvisorUserId; private setter ve
kontrollü AssignAdvisor. Development `students link/advisor` komutları mevcut
hesabı/rolü denetler, public self-assignment yoktur. Bir mevcut danışman, atama
tarihçesi yok. UserId unique bağlantısı ayrı kalır.

**Migration:** `20261105134931_AddStudentAdvisor` Up/Down ve üretilen SQL incelendi;
yalnız sütun/index/FK ekler. Açık hedef kontrolüyle yalnız app DB’ye uygulandı;
öğrenci/enrollment sayıları korundu, mevcut AdvisorUserId değerleri NULL. İkinci
update no-op, has-pending-model-changes temiz. İzole yükseltmede Day28’den Day31’in
son şemasına geçilip mevcut account/profile link kontrol edildi; sonra Day32 ve
tekrar update yapıldı. Id, UserId, StudentNumber, enrollment ilişkileri/durumu ve
refresh oturumu korundu, yeni atama null. Down çalıştırılmadı. Lab’a dokunulmadı.

**Gerçek doğrulamalar (exit0):**
- Restore/build: 0 uyarı, 0 hata. unit ve core geçti.
- resource-access: saf handler doğru/eksik/boş/çoklu kimlik, rol ve ilişki; gerçek
  pinlenmiş HTTPS/LocalDB farklı öğrenci/danışman, profil yok/rol yok/çoklu rol,
  IDOR/body-route hilesi, 401/403/404, cookie-only, CSRF, staff provisioning DTO’sunun
  UserId/AdvisorUserId’yi almaması. Instructor/Staff onay bypass’ı yok. Yetkisiz
  denemeler yeni context’te değişiklik/event üretmedi; reassignment eski danışmanı
  reddetti. Yetkili create/batch/preview/submit/approve/reject çalıştı; ret gerekçesi
  ve state409 korundu. Preview yazmadı. Liste SELECT sayısı daha çok kayıtta aynı.
- Gerçek advisor FK547/NO ACTION547, nullable atama ve aynı danışmana çok öğrenci;
  kontrollü komut yanlış/rolsüz hesap ve Production’ı reddetti. UserId unique/FK
  regresyonları full içinde geçti.
- Full: katalog SQL/projection, tekli unique/FK yarışları, batch ilk INSERT sonrası
  gerçek yazma hatasında rollback, akademik failures/sıra/kredi/saat, state ve restart,
  summary alan/sıra/time(7)/yazmama/N+1, repository/Identity/accounts ve önceki deneyler.
- Observer ayrıca ayrı modda geçti: yeni context Approved okudu, DB yazma hatasında
  olay yok; observer hatasında200, postcommit cancellation ve aynı sorgu sayısı.
- Cookie/JWT/refresh-family/reset/roles mevcut HTTPS paketi full ve resource-access
  içinde geçti: gerçek refresh yarışları/rollback/reuse, reset atomikliği ve yarışları,
  cookie/stamp/JWT sınırları, rol güncelliği. Ayrı mod isimlerinin her biri ayrıca
  çalıştırılmış gibi sayılmıyor; aynı kontroller ortak paketten çağrıldı.
- resource-access ProbeFailure: beklenen assertion metni ve üst exit1 birlikte
  doğrulandı; bu probe DB oluşturmaz.

**Ara hatalar ve düzeltmeler:** Eski şema assertion’ı sütun sayısını10 bekliyordu;
11 ve advisor metadata kontrolleriyle güncellendi. Eski reject testi olmayan kaynağa
400 bekliyordu; yeni güvenlik önceliği404, var olan yetkili kaynakta alan400 testleri
korundu. Summary fixture’ın gecikmeli hesap bağlantısı baseline’ı değiştiriyordu;
kimlik hazırlığı baseline öncesine alındı, snapshot assertion zayıflatılmadı. İlk
probe, full ile aynı executable yeniden derlendiği için MSB3021 dosya kilidine takıldı;
sonradan sıralı çalıştırılıp gerçekten assertion/exit1 görüldü. App migration öncesi
eski PowerShell System.Data sağlayıcısı handshake hatası verdi; hiçbir değişiklik
yapmadı. Çalışan sqlcmd+EF ile hedef kontrolü/migration/tekrar doğrulandı.

**Temizlik:** Bağımsız SQL sorgusunda son resource DB
`university_registration_tests_day6_8e36a6ab72e54780ba86bd27e1c690fc`, core
`university_registration_tests_day10_1c2a351aedee46feb09217ae4603b002` ve observer
`university_registration_tests_day6_8b7ac3d08a5849978b8ae224b1381f19` hesap/rol/üyelik/
oturum/token/öğrenci/kayıt/katalog tabloları0. Son full DB
`university_registration_tests_day6_92bc02e5c7d14d8bac7496e620a074d1` ilk7 grupta0,
yalnız2ders/2dönem/3açılış seed. Üç başarısız full koşusu da aynı temiz seed durumunda.
Geçici catalog/cookie/JWT klasörleri ve UniversityRegistration süreç sayısı0.
İzole DB şemaları mevcut güvenli yaklaşım gereği bırakıldı. App/lab test hedefi olmadı.

**Sınırlar:** Yetki SELECT’i ile SaveChanges arasında atama değişebilir; TOCTOU
çözülmedi/yarışı test edilmiş gibi sunulmadı. HTTP dışı EnrollmentOperations çağrıları
güvenilen iç kullanımdır. Cookie rol güncelliği önceki sınırı korur. Instructor–açılış
ilişkisi, atama geçmişi, Production provisioning paneli ve kapsamlı güvenlik çözümü
kurulmadı. Full sonrasında yalnız ek açık rol/provisioning assertion’ları eklendi;
son resource-access bunlarla geçti, üretim davranışı değiştirilmedi.

**Sıradaki gün:** Enumeration, lockout ve rate limiting (Gün33).
**Commit/push:** `feat: authorize student ownership and assigned advisor access`;
gerçek commit ve push sonucu son raporda; kendi hash’i için ikinci commit yok.


## Gün 33 — Hesap keşfini azaltma, lockout ve rate limiting

Başlangıç: main `6f0524497862d91fb24a00648247de62984f1608` ve temiz çalışma ağacı;
32. gün kaynak erişimi kod/ilerleme kaydında mevcuttu. Önceki günler yeniden uygulanmadı.

**Değişiklik:** `AuthenticationAbuse` ile login10/60s, forgot3/900s ayrı IP fixed
window, QueueLimit0, metadata tabanlı Retry-After ve güvenli429. Dört ayar zorunlu,
aralıkları kontrollü. IPv4-mapped adres normalizasyonu ve sabit eksik-IP fallback;
Forwarded Headers güveni eklenmedi. Routing/authentication/limiter/authorization
sırası açık. Login'de Identity normalizer ile lookup, gerçek hesapta mevcut
SignInManager, olmayan hesapta tek startup dummy hash ile yapılandırılmış hasher
kontrolü. Ortak401 ve mevcut400/500/CSRF korunur. Debug event3301/3302 yalnız tür/TraceId;
secret veya kişisel girdi loglanmaz. Lockout5/5m/AllowedForNewUsers ve failure sayımı
korundu; mevcut LockoutEnabled değerleri değiştirilmedi. Forgot ortak202 korunur,
yeni bilinçli ret429'dur. Paket/model/migration değişmedi.

**Gerçek doğrulamalar (6 Kasım 2026):**
- Restore ve build başarılı, 0 uyarı/0 hata; unit, core, auth-abuse ve full exit0.
- İlk auth-abuse derlemesindeki span/await test ifadesi düzeltildi; sonraki koşu geçti.
- HTTPS yalnız testin tam sertifikasına güvenir. Ortak401 gövde/content-type,
  auth-cookie yokluğu, CSRF400 ve gerçek invalid-table SQL500 kontrol edildi.
- Gerçek hasher test decorator'ı: dummy hash startup'ta1; olmayan hesapta1 kontrol;
  gerçek uygun parola yolunda1 kontrol; dummy DB'de yok. Rate-limited istekte
  hash/lookup/token/yerel bildirim ve failed-count değişimi yok.
- Beş yanlış girişte gerçek Identity sayaç/bitiş alanları yeni context ile doğrulandı;
  kilitte doğru parola reddedildi, başka hesap korunuyor. Bitiş yalnız fixture'da
  UserManager ile geçmişe alındı; sonraki doğru giriş ve sayaç reseti geçti.
  Beş dakika duvar saati beklenmedi. Eski cookie/JWT anında iptal olmadı.
- Ayrı login/forgot kotaları, farklı e-postayla kota yenilenmemesi, sahte XFF'nin
  etkisizliği, mapped IPv4 aynı kota, kontrollü Connection IP'si farklı kota geçti.
  Gerçek farklı ağ kaynak IP'leri denenmedi; public test bypass header eklenmedi.
- Aynı sunucuda 5s test pencereleri yenilendi; kuyruktan sonradan iş çalışmadı.
  Retry-After biçim/değer ve no-store geçti. Eksik/boş/negatif/sıfır/bozuk/taşan
  ayarlar gerçek registration üzerinden reddedildi. Eski regresyonlar10000/60s,
  düşük kota testi10/5s ve3/5s; üretim limitleri kapatılmadı.
- Forgot uygun/bilinmeyen/doğrulanmamış ortak202; yalnız uygun hesapta token/dosya;
  sender arızası aynı dış cevap ve değişmeyen parola/stamp/aileler. Kontrollü
  loglarda parola/hash/dummy/cookie/token bulunmadı.
- Isınma sonrası full koşusunda beşer HTTPS örneği: mevcut doğru giriş
  min/medyan/max87,6/93,7/107,3ms; olmayan hesap85,6/91,1/110,3ms.
  Önceki bağımsız abuse koşusunda medyanlar94,3/103,1ms. Bunlar süre garantisi
  veya timing açığı yok kanıtı değildir; başarılı ve başarısız yolların DB/cookie
  işleri farklıdır. Hasher çağrı kanıtı bu gözlemden ayrıdır.
- Full içinde accounts/Identity/cookie/JWT/refresh-family/password-reset/roles/
  resource-access geçti; gerçek iki bağlantı refresh yarışı, reset atomik rollback,
  rol/ownership/CSRF, katalog ve akademik kurallar korundu. Gerçek batch ilk INSERT
  sonrası unique/FK rollback geçti. Observer commit sonrası Approved okuyabildi,
  SQL hatasında olay yok, observer arızasında başarı korundu; üretim observer'ı
  öncesi/sonrası7 SELECT. Önceki deneyler de full/unit üzerinden geçti.
- `auth-abuse -ProbeFailure` beklenen assertion ve exit1 üretti; üst script korudu.
- `dotnet ef migrations has-pending-model-changes --no-build`: fark yok. Yeni
  migration veya app/lab update yok; Down gerekmedi/çalıştırılmadı.

**Temizlik:** Full DB `university_registration_tests_day6_93bc4655a70246d4bb6adf4ce937e905`
yeni SQL bağlantısıyla kontrol edildi: hesap/rol/üyelik/refresh-token/refresh-session/
öğrenci/enrollment0; yalnız2ders/2dönem/3açılış seed. Core ve abuse kendi izole
DB'lerinde boş test tablolarını doğruladı. Geçici catalog/cookie/JWT klasörleri ve
UniversityRegistration süreç sayısı0. DB şemaları mevcut yaklaşım gereği bırakıldı;
app/lab test hedefi olmadı. Çıktılar obj altında Git dışı.

**Sınırlar:** Constant-time, tam enumeration/DDoS, dağıtık kota veya gerçek çok-IP
network deneyi yok. Kilitli/doğrulanmamış ön kontroller ve bildirim/DB zamanları
farklı kalır. Ortak NAT kotası, password spraying, başkasını kilitleme, restart ve
çoklu instance sınırları sürer. Proxy kurulursa güvenilen adresler/sıra ayrıca
ayarlanmalı. Lockout mevcut oturumları otomatik iptal etmez; rol/ownership TOCTOU ve
JWT iptal sınırları değişmez. Crash/ağ kaybı/timing saldırısı kanıtı üretilmedi.

**Sıradaki gün:** Gün34 XSS/CSRF/cookie flags/token storage/CORS/BFF ve küçük web istemcisi.
**Commit/push:** `feat: limit authentication abuse and reduce account enumeration`;
gerçek hash/push son raporda, kendi hash'i için ek commit yok.


## Gün 34 — Aynı origin web istemcisi ve tarayıcı güvenliği

Başlangıç: main `c13c6c294c1146fa5ed03b6d53faa1c95c717ec9`, temiz çalışma ağacı.
Cookie/antiforgery, roller/sahiplik, refresh/reset ve abuse kodları mevcut repo ve
ilerleme kaydında incelendi; eski günler yeniden uygulanmadı.

**Değişiklik:** wwwroot altında HTML/CSS/JS: e-posta/parola giriş, me durumu,
CSRF yenilemeli logout, public ders listesi; label/klavye/live region ve dar ekran.
Göreli, same-origin/no-store fetch; tek eşzamanlı giriş; hata/bekleme/Retry-After.
401 anonim durum, ağ/500 belirsiz durum olarak ayrılır. Güvenilmeyen metin yalnız
textContent/createElement ile gösterilir. Auth cookie okunmaz; CSRF request token
bellekte. JWT/refresh/storage/BFF/frontend framework eklenmedi.

`WebClientSetup` yalnız web-root static provider/default index kullanır. HTML'e
self CSP, object/frame/base kısıtları; statik no-store/nosniff/no-referrer. API'ye
CORS/global CSP eklenmedi; cookie/authorization/CSRF/backend sözleşmeleri korunur.
Test-only playwright-core1.65.0 kilitlendi; kurulu Edge kullanılır, browser indirme
ve production paket büyümesi yok. scripts/browser publish content'inden dışlandı.

**Gerçek browser doğrulamaları (6 Kasım 2026):**
- İlk koşu WebRoot'un builder sonrası değiştirilmesi nedeniyle NotSupportedException
  ile durdu; başlangıç WebApplicationOptions'a taşındı. Son web-client exit0.
- Gerçek izole LocalDB + HTTPS + Edge: anonim401, doğru giriş ve hesap e-postası,
  yenilemede cookie kalıcılığı, yanlış parola ortak ret/no-cookie, logout204 sonrası
  kişisel ekran temizliği/me401. Parola alanı her denemede temizlendi.
- Login sırasında iki submit tek POST üretti. Kimlik değişimi sonrası farklı CSRF
  request token alındı; eksik/bozuk logout token400 ve me200 ile oturum korundu.
- Auth ve CSRF cookie'leri gerçek browser context'inde mevcut; Secure/HttpOnly/
  Strict/Path=/ ve host kapsamı doğrulandı. document.cookie ikisini de okuyamadı,
  fetch me200 ile taşıdı. Aynı-origin Strict davranışı geçti.
- DB'deki HTML/event-handler benzeri ders adı literal göründü; img/script oluşmadı,
  işaret çalışmadı. Hata metni payload'ı da literal; CSP execution engeli tek kanıt
  sayılmadı. CSP header'ı, masaüstü ekran görüntüsü ve390px taşmasız düzen incelendi.
- localStorage/sessionStorage/IndexedDB boş; uygulama dev-token/refresh çağırmadı;
  console/URL'de parola/cookie/token yok. Host loglarında fixture parola/hash yok.
- Doğrudan browser HTTP çağrıları katalog yazma ve öğrenci kaydı için403 döndürdü;
  butonlara güvenilmedi. Yetki matrisi ayrıntıları full backend regresyonunda geçti.
- Gerçek login kotası10/60s dolduruldu; sonraki UI login429 ve bekleme mesajı,
  auth-cookie yokluğu doğrulandı. Kontrollü UI403/500/ağ ve literal400 hata sunumu
  ayrıca browser intercept ile test edildi; bunlar DB arızası simülasyonu sayılmadı.
- Gerçek Production executable ayrı izole config/DB ile HTTPS'te açıldı: root200,
  lab/dev-token/outbox/key/docs canary yolları404. Browser sertifika istisnası yalnız
  test anahtarının SPKI'sine; HTTP kontrolü exact cert'e bağlı, global bypass yok.

**CORS ayrı kanıt:** web-client içinde ve bağımsız cors-lab exit0. Ana app yanıtı
farklı origin'den okunamadı. Test API'sinde izin olmayan basit GET sunucuya1 kez
ulaştı ama JS okuyamadı. Reddedilen preflight OPTIONS1 ve ek GET0. Tek izinli origin
X-Lab preflight ardından GET okuyabildi; diğer origin'in basit GET'i ulaştı ama
okunamadı (open GET toplam2, OPTIONS1). Yalnız geçici test hostlarında izin var.
Portlar cross-origin fakat cross-site değildir. Ayrı site adlarıyla SameSite deneyi,
Firefox/Safari ve kapsamlı erişilebilirlik denetimi yapılmadı.

**HTTP/regresyon:** restore/build0 uyarı0 hata; unit/core/full exit0. Full içinde
accounts/cookie/JWT/refresh-family/reset/roles/resource-access/auth-abuse çalıştı.
Gerçek batch INSERT sonrası rollback, refresh yarışları/reset atomikliği, commit
sonrası observer ve akademik kurallar korundu. Full'den sonra yalnız browser
fixture/profil/kota/Production sınırı testleri genişletildi, üretim davranışı değişmedi;
son web-client bunlarla geçti. Kontrollü web-client ProbeFailure exit1, üst script
koruyor. Publish başarılı; wwwroot3 dosya ve sıkıştırılmış karşılıkları var,
scripts/browser publish'e girmedi. EF has-pending-model-changes fark yok; migration
ve app/lab update yapılmadı; Down gerekmedi/denenmedi.

**Temizlik:** Son browser DB `university_registration_tests_day6_c225f53051a6451e98cc1c580677312e`
yeni SQL bağlantısında Users/Students/Enrollments/Courses0. Full DB
`university_registration_tests_day6_32d3f8deb95347aba5d7d021cf527678` hesap/rol/oturum/
token/öğrenci/enrollment0; yalnız2ders/2dönem/3açılış seed. Cookie/catalog fixture
dizinleri, owned browser süreçleri, UniversityRegistration süreçleri ve geçici JWT
anahtar dizinleri0. İzole DB şemaları mevcut güvenli yaklaşım gereği bırakıldı.
Obj test çıktıları/anonim görsel Git dışıdır; sırlar, profiller ve local mesajlar
commit kapsamına alınmadı. App/lab browser test hedefi olmadı.

**Sınırlar:** CSP/HttpOnly tam XSS/CSRF çözümü değildir; parola alanı temizliği güvenli
bellek silme değildir. Cookie bileti/security stamp, kopyalanmış cookie ve JWT iptal,
TOCTOU, ortak IP ve dağıtık limiter sınırları önceki gibi. Ayrı BFF/OAuth sunucusu yok.
**Sıradaki gün:** Gün35 OAuth2/OIDC Authorization Code + PKCE.
**Commit/push:** `feat: add a secure same-origin web client`; gerçek hash ve push
son raporda; kendi hash'i için ek commit yok.

## Gün 35 — OIDC Authorization Code + PKCE

**Başlangıç kanıtı:** main HEAD `0874ee3d2d587f38939c3fbec8a4beb3c5bd99ef` ve temiz
çalışma ağacı incelendi. Gün24–34 kodları/ilerleme kaydı vardı; önceki kapsam yeniden
uygulanmadı. Identity.External ve AspNetUserLogins zaten mevcuttu.

**Değişiklik:** Uyumlu OpenIdConnect10.0.14 paketi, tek açık Development sağlayıcısı,
code/form_post + PKCE S256, HTTPS discovery/JWKS, issuer/audience/RS256/lifetime,
state/correlation/nonce; openid ve SaveTokens=false. Default cookie ve ayrı bearer
şemaları korunuyor. `/accounts/external/options`, CSRF korumalı login POST,
framework `/signin-oidc` ve yerel callback eklendi. Önceden bağlanmış issuer/sub
Identity login ilişkisi gerekir; email merge/otomatik hesap/Student/rol yoktur.
`external link` manager komutu Development'ta çalışıp host açmadan çıkar. UI yalnız
etkin sağlayıcı için form navigasyonu sunar; CSP sadece form hedefinde dar genişler.

**Gerçek sağlayıcı:** MIT/OpenID Certified oidc-provider9.13.3, izole Node süreci;
Node24/Edge, gerçek LocalDB ve HTTPS. jose6.2.15 yalnız negatif protokol fixture'ı;
playwright-core mevcut test bağımlılığı. Uygulama sağlayıcı veya OAuth server içermez.
Başarı token'ı kütüphanenin kendi işlemlerinden gelir; bozuk ID token deneyleri
ayrı kontrollü fixture'dır. TLS yalnız teste ait sertifika/SPKI ile doğrulandı.

**Gerçek sonuçlar:** `oidc` exit0. Gerçek sağlayıcı ekranı → code/S256 → backend
exchange → önceden bağlı yerel hesap/me200; yerel Instructor korunurken sağlayıcının
fiilen gönderdiği StudentAffairs yetkisi alınmadı. Aynı e-posta/farklı bağlanmamış sub,
doğrulanmamış, kilitli ve2FA hesaplar reddedildi. Otomatik hesap/Student yok;
AccessFailedCount artmadı. Aynı link tekrarında0, başka hesaba linkte1. Issuer'a bağlı
provider anahtarı farklı Authority'de farklı; yanlış issuer imzalı fixture reddedildi.
İkinci ayrı gerçek IdP kurulmadı.

State/correlation retleri token kabulünden önce; yanlış/eksik nonce signature/issuer/
audience geçtikten sonra gerçek InvalidNonceException ile reddedildi. Yanlış issuer,
audience, imza ve geçmiş exp kendi exception türlerine ulaştı. Boş sub ve array sub
reddedildi; array fixture JSON/claim ayrıştırmasında kesildi, handler'ın çoklu claim
koluna ulaştığı iddia edilmiyor. Yanlış verifier ve yeniden kullanılan code gerçek
provider token endpoint'inde400; ilk replay exchange200, ikinci400 gözlendi.
Token endpoint503, kullanıcı iptali, dış/protocol-relative returnUrl, kayıtsız provider
redirect URI ve eksik/geçersiz başlangıç CSRF reddedildi. Her başarısız akışta yeni
application cookie yok; external cookie temiz. Mevcut girişte start409 ve oturum
korunuyor. Yerel logout sonrası provider session ile yeniden kimlik girişi mümkün.
Uygulama loglarında fixture parola/hash/code/verifier/token/client secret yok.

**Regresyon:** restore/build0uyarı0hata; unit/core/full exit0. Full mevcut hesap,
cookie/JWT, refresh aile/yariş/rollback, reset atomikliği, roles/resource-access,
auth-abuse, akademik kurallar, gerçek unique/FK/batch rollback ve commit sonrası
Observer kontrollerini içerdi. Web-client exit0: gerçek Edge local login/reload/logout,
CSRF, XSS literal DOM, storage/cookie/static güvenliği, UI429/403/500/network ve
izole CORS/preflight geçti. Özellik kapalı seçenek gizli, yerel login çalışıyor;
Production gerçek executable'da external/lab yolları yok. Etkin Production veya
eksik/HTTP Authority/client/secret yapılandırmaları kayıt aşamasında reddedildi.
Kontrollü oidc ProbeFailure exit1; üst script hata yayılımını koruyor.

**Model/temizlik:** EF has-pending-model-changes fark yok. Migration/DB update/Down
uygulanmadı. App/lab test hedefi olmadı. Test kullanıcı external login ilişkileri,
roller, öğrenci ve diğer owned kayıtlar temizlendi; izole DB şemaları önceki düzen
gereği bırakıldı. Geliştirme sırasında başarısız üç test DB'sinde kalan yalnız bu
çalışmanın Instructor rolleri, users/profiles/logins0 doğrulandıktan sonra temizlendi.
Provider/browser/HTTPS süreçleri kapatıldı; key/secret/token yalnız pipe/bellekte,
DP/test dosyaları owned fixture temizliğinde kaldırıldı. Obj çıktıları Git dışıdır.

**Sınırlar/yapılmayanlar:** Ayrı cross-site host testi, Firefox/Safari, gerçek dış IdP,
federated logout, Production provisioning, crash/network-loss ve reset/external-login
atomik yarış garantisi yok. Token endpoint503 kontrollü gerçekHTTP deneyidir; fiziksel
bağlantı kopması denenmedi. Yetki kontrolü-cookie yazımı arası TOCTOU ve mevcut
security-stamp doğrulama aralığı sürer. Harici hata mesajı yerel hesabın varlığını
ayırmaz; tam timing/enumeration garantisi yok. Elle `--serve` terminal kurulumu için
adımlar belgelendi; otomatik provider/browser akışından ayrı manuel kullanıcı
oturumu yapılmadı. CSP/None protokol cookie ayarları cross-site test kanıtı değildir.

**Sıradaki gün:** Gün36 check-update yarışı, lost update ve son kontenjan deneyi.
**Commit/push:** Tek çalışma commit mesajı `feat: add OIDC code flow with PKCE and explicit account mapping`.
Gerçek hash/push sonucu son raporda; kendi hash'i için ek commit üretilmez.

### Gün 36 — Check-update yarışı ve lost update
- Öğrenilenler: Yarış durumu, eski veriye dayalı karar, kayıp güncelleme.
- Değişiklik: İzole DB için concurrency-lab modu eklendi; üretim davranışı değişmedi.
- Doğrulama: Build ve concurrency-lab geçti, exit 0.
- Sonuç: Sıralı kontrolde 1 kayıt; yarışta kapasite 1 için 2 kayıt; iki artışta sayaç 1.
- Temizlik: Deney verileri/tablosu ve bağlantılar temizlendi.
- Git: Commit/push bekliyor.
- Sıradaki gün: 37 — Atomik koşullu güncelleme ve kontenjan koruması.

### Gün 37 — Atomik kontenjan yönetimi

- İhtiyaç/kavramlar: check-update yarışı, koşullu UPDATE, etkilenen satır sayısı,
  açık transaction, açılış→kayıt kilit sırası, denormalize sayaç ve postcommit olay.
- Önceden hazırlanmış Gün37 diff'i korundu ve tamamlandı; Gün36 laboratuvarı yeniden
  yazılmadı. CourseOffering.OccupiedSeats private setter/default0 ve aralık CHECK'i;
  submit +1, reject yalnız kazanan geçiş için -1, approve sayaçsız. AsNoTracking
  okumalar bulk yazma sonrası eski tracker ile kaydedilmez. Kapasite409 kodu
  `capacity_unavailable`; sahiplik, CSRF, akademik retler ve UNIQUE/FK korundu.
- EF aracı tek `20261110110730_AddOccupiedSeats` migration'ını, Designer ve snapshot'ı
  üretti. Up sütun→guarded backfill→CHECK. SQL51037 aşımı açıkça durdurur.
- Restore başarılı; build 0 uyarı/0 hata. `seat-capacity` exit0: ayrı bağlantı/SPID,
  kontrollü buluşma; son yere iki submit, aynı Draft'a iki submit, çift reject,
  approve–reject tek kazanan, gerçek +1 sonrası CHECK hatasında tam rollback,
  yeni context sayaç=aktif kayıt sayısı ve postcommit observer okuması geçti.
  İzole eski şema yükseltmesi aşımda tamamen rollback, geçerli dört durumlu veride
  backfill2/kimlik-alan koruma, ikinci update ve model uyumu doğrulandı.
- `state` exit0: gerçek HTTP kapasite409/Draft korunması/yer bırakma ve yeniden
  kullanım, mevcut ret sırası/state/gerekçe/UNIQUE kontrolleri geçti.
- `observer` exit0: commit sonrası Approved okuma, SQL500'de olay yok, tekrar409,
  observer hata/iptal politikası ve dış transaction reddi geçti. Lock/recheck ile
  toplam9 SELECT; log observer ek sorgu üretmiyor.
- Full ilk koşu eski 13-sütun şema beklentisinde; ikinci koşu negatif kapasitenin
  yeni CHECK'i önce ihlal etmesinde durdu. `sql/app/verify.sql` yeni sütun/default/
  CHECK/sayaç eşitliğini doğrulayacak şekilde uyarlandı. Shared constraint testi
  yalnız negatif kapasitede iki bilinen CHECK'ten 547 kabul eder; diğer exact hata
  beklentileri korunur. Kontrollü SQL alt testi geçti. Bu hatalar nedeniyle yeniden
  çalıştırılan son full exit0: HTTP/katalog/akademik/state/summary, gerçek UNIQUE/FK,
  ilk INSERT sonrası batch rollback, observer, Identity/accounts/cookie/JWT,
  refresh-family/reset/roles/resource-access/auth-abuse ve mevcut saf deneyler geçti.
  Başarılı full sonrasında gereksiz tekrar yapılmadı.
- Uygulama DB'si appsettings ve override kontrolünden `university_registration_app`
  olarak doğrulandı. Uygulama süreci/başka DB session bulunmadığı kontrol edildi.
  İlk PowerShell System.Data sürücüsü şifreleme hatası verdi, yazma yapmadı.
  Geçici bakım yardımcı kodu uygulamanın Microsoft.Data.SqlClient/EF sürücüsüyle
  mevcut yapılandırmayı kullandı: migration uygulandı; offering eski alanları,
  enrollment, student ve tamamlanan derslerin önce/sonra SHA256 JSON özetleri eşleşti;
  sayaçlar aktif kayıt sayısıyla eşit. İkinci update no-op, EF model/snapshot farkı yok.
  Lab DB'sine bağlanılmadı; app'e test verisi yazılmadı. Down çalıştırılmadı.
- Temizlik: tüm hedefli testler owned fixture/constraint temizliğini tamamladı.
  Full koşuları süreç/owned kayıt temizliği yaptı; mevcut düzen gereği izole DB
  şemaları (full için seed ile) bırakıldı. Geçici bakım kodu/SQL dosyaları kaldırıldı;
  build çıktıları Git dışında. Son süreç kontrolünde uygulama/test hostu kalmadı.
- Sınırlar: farklı açılış kredi/saat ve danışman/sahiplik TOCTOU yarışları çözülmedi.
  Genel lost-update/otomatik retry, Outbox, crash/yanıt kaybı testi yok. Ayrı browser,
  CORS ve OIDC modları bu çalışmada tekrar çalıştırılmadı. Kapasite kilidi aynı
  açılıştaki yazmaları serileştirir; benchmark veya tüm concurrency sorunları için
  garanti değildir.
- Belgeler: README, docs/day37-seat-capacity.md ve ADR0037.
- Commit/push: tek çalışma commit'i `feat: enforce seat capacity with atomic enrollment transitions`;
  gerçek hash ve push sonucu son raporda, hash için ek commit yok.
- Sıradaki çalışma: iyimser eşzamanlılık ve diğer lost-update sınırlarının değerlendirilmesi.


### Gün 38 — Course rowversion ve optimistic concurrency

- Kavramlar: stale client, DB üretimli sürüm, EF OriginalValue, koşullu UPDATE,
  DbUpdateConcurrencyException/409 ve kör retry'nin lost update riski.
- Değişiklik: Course.RowVersion private setter/IsRowVersion. Detay ve başarılı PUT
  Base64 sürüm döndürür; PUT kanonik Base64/8 bayt sürüm ister. Eksik/bozuk400,
  başlangıçta olmayan404, eski sürüm409 `course_version_conflict`. İstemci sürümü
  OriginalValue yapılır; aynı içerikte de Name IsModified ile koşullu UPDATE vardır.
  Diğer DB hataları concurrency diye sınıflandırılmaz. Liste/katalog/POST yanıtları,
  StudentAffairs cookie/CSRF, normalizasyon, kredi ve kod benzersizliği korunur.
- EF aracı `20261110112155_AddCourseRowVersion` migration/Designer/snapshot üretti.
  İncelenen gerçek SQL yalnız Courses'a rowversion NOT NULL ekliyor; tablo/ilişki
  yeniden oluşturma yok. Restore başarılı, build0 uyarı/0 hata; model/snapshot farkı yok.
- `course-version` hedefli koşu ilk kez fixture'ın küçük harfli rastgele kodunu
  normalizasyon sonrası aynı beklemesi nedeniyle durdu. Fixture beklenen büyük harf
  politikasıyla düzeltildi; yeniden koşu exit0. İki bağımsız HTTPS/cookie istemcisi
  aynı sürümü okudu: ilk200, ikinci409; yeni context ilk değişikliği korudu. Güncel
  sürümle yeni200 ve yeni sürüm; stale aynı içerik409; eksik/bozuk/7-9 bayt400,
  yok404, anonymous401/Student403, CSRF400, kredi400, ayrı kod UNIQUE409 geçti.
  Public liste alanları değişmedi. İzole eski şema yükseltmesinde ders kimliği ve
  alanları korundu, 8 bayt üretildi; ikinci update/model uyumu doğrulandı.
- Hedefli başarıdan sonra full yalnız bir kez çalıştırıldı ve exit0 verdi. Mevcut
  PUT fixture'ları detaydan sürüm alır; dedicated stale testi bu yardımcıyı kullanmaz.
  Full gerçek SQL şeması/UNIQUE/FK, batch ilk INSERT sonrası rollback, akademik
  kurallar/state/observer, katalog, hesap/cookie/JWT, refresh-family/reset, roller,
  ownership ve auth-abuse kontrolleri geçti. Ayrı browser/OIDC modları çalıştırılmadı.
- Eski migration fixture'ları yeni modelin RowVersion OUTPUT sütununu eski şemada
  istememesi için açık sütunlu tarihsel Course INSERT ile uyarlandı. Etkilenen
  seat-capacity modu ayrıca exit0: aşımda migration rollback, geçerli backfill,
  çift submit/reject/approve-reject, gerçek ara hata rollback ve sayaç eşitliği geçti.
- App hedefi mevcut appsettings/environment üzerinden doğrulanarak
  `university_registration_app` seçildi. Yükseltme öncesi uygulama süreci ve başka
  DB session olmadığı kontrol edildi. Uygulamanın EF/SqlClient sürücüsüyle migration
  uygulandı; course eski alanları, offering/enrollment/student JSON SHA256 özetleri
  önce/sonra aynı, mevcut sürümler8 bayt. İkinci update no-op/model uyumlu.
  Lab DB'sine dokunulmadı; app DB'ye test verisi yazılmadı. Down çalıştırılmadı.
- Temizlik: hedefli/full/seat fixture'ları ve HTTPS süreçleri kapatıldı. İlk başarısız
  hedefli DB'de course/account/role kalmadığı ayrıca doğrulandı. Test DB şemaları
  mevcut yaklaşım gereği kalır (full seed ile); geçici bakım kodu/SQL kaldırıldı.
  Sertifika/anahtar/parola ve build çıktıları Git'e alınmadı.
- Belgeler: README, day38-optimistic-concurrency.md, ADR0038; kod konumları ve
  alternatif/maliyet açıklandı. Otomatik retry/merge, ETag veya yeni katman yok.
  Silinme ile PUT arasındaki ayrı yarış deneyi yapılmadı; farklı açılış kredi/saat
  ve danışman atama TOCTOU sınırları çözülmüş değildir.
- Commit/push: tek commit `feat: prevent stale course updates with rowversion`;
  gerçek hash/push sonucu son raporda. Kendi hash'i için ek commit üretilmez.


## Gün 39 — SQL Server transaction isolation (11 Kasım 2026)

- Yalnız test kodu: `scripts/IsolationLabChecks.cs`; mevcut çalıştırıcıya tek
  `isolation-lab` modu. Üretim modeli/endpoint/paket/migration değişmedi.
- Her koşu yeni `university_registration_tests_day39_<guid>` DB ve küçük indeksli
  tablo oluşturur. Ayrı unpooled SqlClient bağlantıları, parametreli scalar
  sorgular, süre sınırlı iptal/rollback/await ve finally DROP kullanılır.
- RCSI/ALLOW_SNAPSHOT_ISOLATION OFF/OFF, ON/OFF ve OFF/ON ayrı doğrulandı;
  ayarlar yalnız deney bağlantıları kapalıyken yönetildi. App/lab DB'ye dokunulmadı.
- Gerçek sonuçlar: RU 99 dirty read → rollback kalıcı10; RC dirty read LCK_M_S
  bekledi ve rollback sonrası10; RC iki okuma10/20; RR UPDATE LCK_M_X bekledi,
  kalıcı20; ayrı RR phantom sayısı1/2; SERIALIZABLE INSERT LCK_M_RIn_NL bekledi,
  okuyucu commit sonrası kalıcı sayı2. Bekleme bilgisi DMV'den gerçek session
  çiftiyle gözlendi; kısa süre tamamlanmamayı kilit kanıtı saymadık.
- RCSI açık yazara rağmen commit edilmiş10 okudu; ayrı deneyde10/20.
  SNAPSHOT aynı transaction'da10/10, yeni transaction'da20. Her deney sonunda
  yeni bağlantıyla kalıcı satır değeri ve koşula uyan satır sayısı doğrulandı.
- `dotnet build --nologo`: restore güncel, 0 uyarı/0 hata. Hedefli isolation-lab
  exit0. `-ProbeFailure` bütün deneylerden sonra beklenen assertion/exit1;
  finally temizliği başarılı. Mevcut concurrency-lab smoke exit0: sıralı tek
  kayıt, kontrollü kapasite aşımı ve kayıp artış beklenen kusurları gösterdi.
- İki isolation koşusunun DB'leri finally içinde; smoke'un bu koşuda oluşturduğu
  DB ise mevcut satır/tablo temizliği sonrasında ayrıca kaldırıldı ve DB_ID ile
  yokluğu doğrulandı. Başka bağlantı zorla sonlandırılmadı; uygulama açılmadı.
- Full/HTTP, migration/model-snapshot komutları çalıştırılmadı: üretim/schema
  değişmedi. Deadlock, performans, SNAPSHOT yazma çatışması ve crash recovery
  deneyleri yapılmadı. Önceki günlerin sonuçları yeniden doğrulandı sayılmadı.
- README ve `docs/day39-isolation.md`: dirty/non-repeatable/phantom ayrımı,
  RCSI–SNAPSHOT ile rowversion farkı, kilit/sürüm saklama maliyeti, izin ve
  koordinasyon sınırları. Üretim mimarisi seçilmedi; yeni ADR yok.
- Commit: `test: demonstrate SQL Server isolation behavior`; gerçek hash/push
  sonucu son raporda (kendi hash'i için ek commit yok).


## Gün 40 — Blocking, deadlock ve write skew (12 Kasım 2026)

- `scripts/LockingLabChecks.cs` ve tek `locking-lab` modu eklendi. Gün39 SQL/DMV
  yardımcıları yalnız test assembly'sinde internal erişimle yeniden kullanıldı.
  Üretim kodu/model, paket, migration ve app/lab ayarı değişmedi; yeni ADR yok.
- Yeni day40 GUID DB'si ve Resources/Marks/Departments/Advisors tabloları. Ayrı
  unpooled bağlantılar, parametreli SQL, sınırlı TCS koordinasyonu; hata halinde
  eşini iptal etme, bütün task'ları bekleme ve finally cleanup.
- Blocking gerçek LCK_M_X ile görüldü, A commit sonrası B tamamlandı: X=2,Y=0.
  LOCK_TIMEOUT 1000 ile gerçek1222; transaction açık kaldı, açık rollback önceden
  yazılan işareti kaldırdı; @@TRANCOUNT=0, yalnız A'nın X=1 yazması kalıcı.
- A X→Y / B Y→X: ilk kaynaklar alınmadan ikinciye geçilmiyor. Gerçek1205 alan
  tek kurban, tek commit; yeni bağlantı iki kaynakta yalnız kazananın katkısını ve
  yalnız kazananın işaretini gördü. Kurban normal koşuda B, probda A; kimliği sabit
  varsayılmadı. Timeout/cancellation deadlock başarısı sayılmadı.
- Tutarlı X→Y: B ilk kilitte beklediği sırada A tamamlandı; iki commit, X=2,Y=2.
- SNAPSHOT write skew: iki okuma2/2; farklı satırlarda gerçek rowversion koşullu
  UPDATE'ler ikisi de1 satır etkiledi ve commit oldu; aktif=0 (beklenen kusur).
  Ayrı düzeltilmiş deney RC + ortak bölüm UPDLOCK,HOLDLOCK: LCK_M_U gözlemi,
  kilit sonrası sayılar2/1, yalnız bir pasifleştirme, kalıcı aktif=1.
- Build restore dahil0 uyarı/0 hata. İlk hedefli koşuda XACT_STATE smallint/CLR int
  uyuşmazlığı ile hata alındı; sorgu açık CAST AS int yapıldı. Son locking-lab
  exit0; ProbeFailure bütün deneylerden sonra beklenen assertion/exit1. Paylaşılan
  yardımcılar için isolation-lab smoke exit0. Full/browser/HTTP tekrarlanmadı.
- Başarısız ilk koşu dahil üç day40 DB'si ve smoke day39 DB'si finally içinde
  kaldırıldı; DB_ID yokluğu doğrulandı. Başka bağlantı zorla sonlandırılmadı.
  Yeni sunucu/browser süreci açılmadı; test çıktısı/sır Git'e alınmadı.
- README ve day40-locking.md: blocking/1205/1222, lock sırası, çok-satırlı kural,
  rowversion sınırı ve retry maliyetleri. Deadlock graph, performans, crash/ağ
  kaybı ve üretim danışman atama yarışı test edilmedi. Önceki günler topluca
  yeniden doğrulanmış sayılmaz; bu karşılaştırma üretim çözümü değildir.
- Commit: `test: demonstrate deadlocks and write skew`; gerçek hash/push sonucu
  son raporda. Kendi hash'ini yazmak için ek commit üretilmez.


## Gün 41 — Kalıcı enrollment idempotency
- POST /enrollments için zorunlu UUID Idempotency-Key; mevcut cookie/Student
  sahipliği/CSRF ve güncel DB rol kontrolü replay öncesinde korunur.
- EnrollmentCreationReceipt: hesap+işlem+anahtar UNIQUE, semantik SHA-256,
  ilk201 JSON/yerel Location ve sabit24 saat. Claim INSERT, mevcut create
  koordinasyonu ve sonuç UPDATE aynı transaction içinde; yalnız başarı commit.
  İş reddi/DB hatası claim dahil rollback; çakışma sonrası yeni context okuması.
- Aynı içerik ilk yanıtı tekrar verir; farklı içerik409 idempotency_key_reused,
  süresi dolmuş mevcut anahtar409 idempotency_key_expired. Batch/submit değişmedi.
- Restore/build: başarılı, 0 uyarı/0 hata. Hedefli idempotency iki koşu ve core
  başarılı. Gerçek HTTPS sıralı/paralel replay; SQL UNIQUE yarışının iki ayrı
  context ile kontrollü kaybı; sonuç yazma hatasında tam rollback; iş reddinde
  anahtarın tüketilmemesi; expiry, hesap izolasyonu, CSRF ve erişim kaldırılması
  doğrulandı. Restart sonrası replay ve kaynak güncellenince ilk yanıt/güncel
  detay ayrımı doğrulandı. Gerçek ağ kaybı enjekte edilmedi.
- Full ilk koşuda eski şema tablosu sayısı beklentisi, ikinci koşuda Observer
  test hostunun eksik DbContextOptions DI kaydı nedeniyle durdu. Dar düzeltmeler
  sonrası hedefli Observer ve son full tamamen başarılı (exit0). Test kapsamı
  azaltılmadı. Full: akademik kurallar, state, gerçek UNIQUE/FK ve batch rollback,
  Observer commit/hata/iptal, catalog/rowversion, Identity/accounts/cookie/JWT,
  refresh/reset, roles/resource-access/auth-abuse kontrollerini içerdi.
- EF aracıyla AddEnrollmentCreationReceipts üretildi; Up/Down ve SQL incelendi.
  Yalnız yeni tablo/index/FK/CHECK eklenir. İzole eski migration yükseltmesinde
  veri koruma, ikinci update ve model uyumu başarılı. Doğrulanmış app DB'ye,
  uygulama süreci/aktif DB bağlantısı yokken uygulandı. Eski tabloların sıralı
  içerik özetleri öncesi/sonrası eşit; ikinci update no-op; model/snapshot uyumlu.
  Lab DB'ye dokunulmadı. Down uygulanmadı.
- Koşuların fixture/süreç temizliği tamamlandı; bu çalışmanın oluşturduğu yedi
  izole test DB'si zorla bağlantı kesmeden kaldırıldı, yoklukları doğrulandı.
  Geçici migration SQL'i kaldırıldı. Secret/test çıktısı Git'e alınmadı.
- README, day41-idempotency.md ve ADR0041: transaction, kalıcı yanıt, saklama
  maliyeti ve yetki sınırı. Expired kayıtlar otomatik silinmez; erken silme
  anahtar tekrar kullanım korumasını kaldırır. DB pending şekline izin verir,
  tamamlanmamış sonucu commit etmeme uygulama invariant'ıdır. Exactly-once,
  ağ teslimi, crash recovery ve ownership TOCTOU çözümü iddiası yok.
  Ayrı browser/OIDC paketleri bu gün yeniden çalıştırılmadı.
- Commit: feat: make enrollment creation idempotent. Gerçek hash/push sonucu
  son raporda; kendi hash'ini yazmak için ek commit oluşturulmaz.


## Gün 42 — Bekleme listesi ve deterministik sıra
- WaitlistEntry ayrı model: DB sequence QueueNumber, Waiting/Cancelled/Placed/
  Ineligible, kapanış zamanı/nedeni. NO ACTION FK, sıra UNIQUE, Waiting için
  öğrenci+açılış filtered UNIQUE ve kapanış/pozitif sıra CHECK'leri.
- Cookie-only Student sahipliğiyle katılım, kendi başvuru/pozisyonunu okuma ve
  iptal. POST CSRF;401/403/ortak 404. Draft izinli; Submitted/Approved/Rejected,
  boş kontenjan ve ikinci Waiting 409. İptal koşullu UPDATE; tekrar Cancelled 204
  ilk kapanışı değiştirmez. Yeniden katılım yeni satır/numara; yerleştirme yok.
- Pozisyon aynı açılıştaki Waiting QueueNumber sırasından tek SQL projection
  içindeki COUNT ile hesaplanır; başka öğrenci bilgisi açılmaz. Numara boşlukları
  geçerli; tıklama/commit zamanı sırası garantisi değildir.
- Restore/build başarılı, 0 uyarı/0 hata. Hedefli waitlist bir koşuda exit0:
  izole eski migration yükseltmesi/profil koruma, ikinci update/model uyumu;
  gerçek HTTPS paralel aynı başvuruda tek Waiting, farklı öğrencilerin sıra/
  pozisyonları, iptal/tekrar/yeni katılım, rol/sahiplik/CSRF, boş açılış ve bütün
  enrollment durumlarının ret/izinleri; gerçek SQL FK/filtered UNIQUE/CHECK.
  Yeni context'te enrollment durumları ve OccupiedSeats değişmedi.
- Full bir koşuda exit0. Yeni yollar geçerli JWT bearer ile 401; cookie-only
  şema ayrımı korundu. Akademik/state/idempotency/rowversion/batch rollback,
  Observer, hesap/cookie/JWT/refresh/reset/roles/resource-access/auth-abuse
  regresyonları geçti. Browser/OIDC ve concurrency laboratuvarları ayrıca
  yeniden çalıştırılmadı; bu günün kapsamında tam eski test tekrarı iddiası yok.
- EF aracıyla 20261113123141_AddWaitlistEntries üretildi; Up/Down ve SQL incelendi.
  Yalnız yeni tablo/sequence/index/kısıtlar. Doğrulanmış app DB'ye uygulama süreci
  ve aktif bağlantı yokken uygulandı. Eski tabloların sıralı içerik özetleri
  öncesi/sonrası eşit. İkinci update no-op, model/snapshot uyumlu.
  Down uygulanmadı; lab DB'ye dokunulmadı.
- Fixture'lar finally ile temizlendi; HTTPS süreçleri/sertifika alanları kapandı.
  Bu günün iki izole test DB'si zorla bağlantı kesmeden kaldırıldı; yokluk
  doğrulandı. Geçici migration SQL'i silindi. Secret/test çıktısı commit edilmedi.
- README, day42-waitlist.md ve ADR0042 güncellendi. Kontenjan/enrollment kontrolü
  ile INSERT arasındaki yarış ve sahiplik TOCTOU çözülmedi. FIFO yer garantisi
  değildir; Placed/Ineligible yalnız modelde gelecekteki kapanış durumlarıdır.
  Sıradaki 43. gün: bekleme listesi/kontenjan işlemlerinin koordinasyonu.
- Commit: feat: add ordered student waitlist entries; hash/push son raporda.
